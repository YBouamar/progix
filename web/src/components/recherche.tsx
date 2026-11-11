"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import * as Dialog from "@radix-ui/react-dialog";
import { Clock, CornerDownLeft, Plus, Search } from "lucide-react";
import { api } from "@/lib/api";
import { peutGererCommercial, useAuth } from "@/lib/auth";
import { menuPour } from "@/lib/nav";
import { cn } from "@/lib/utils";

type Resultat = { cle: string; groupe: string; titre: string; detail?: string; href: string; icone?: "page" | "action" | "recent"; chemin?: string };
type Liste<T> = { items: T[] };

const CLE_RECENTS = "progix-recents";
const MAX_RECENTS = 6;

function lireRecents(): Resultat[] {
  try { return JSON.parse(localStorage.getItem(CLE_RECENTS) ?? "[]") as Resultat[]; } catch { return []; }
}

/** Retient les dernières fiches ouvertes depuis la recherche, sur cet appareil seulement. */
function retenir(r: Resultat) {
  if (r.icone === "page" || r.icone === "action") return;
  try {
    const suite = [{ ...r, groupe: "Ouverts récemment", icone: "recent" as const }, ...lireRecents().filter((x) => x.href !== r.href)].slice(0, MAX_RECENTS);
    localStorage.setItem(CLE_RECENTS, JSON.stringify(suite));
  } catch { /* stockage indisponible : la recherche fonctionne sans l'historique */ }
}

const sansAccent = (s: string) => s.normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase();

/**
 * Recherche globale : pages du menu, projets, devis, clients et partenaires, selon le rôle.
 * S'ouvre avec Ctrl+K (ou Cmd+K), ou la touche « / » hors d'un champ de saisie.
 */
export function Recherche({ open, onOpenChange }: { open: boolean; onOpenChange: (o: boolean) => void }) {
  const { user } = useAuth();
  const router = useRouter();
  const [saisie, setSaisie] = useState("");
  const [trouves, setTrouves] = useState<Resultat[]>([]);
  const [recents, setRecents] = useState<Resultat[]>([]);
  const [enCours, setEnCours] = useState(false);
  const [actif, setActif] = useState(0);
  const liste = useRef<HTMLDivElement>(null);

  const menu = useMemo(() => (user ? menuPour(user) : []), [user]);
  const ids = useMemo(() => new Set(menu.map((m) => m.id)), [menu]);

  useEffect(() => {
    const touche = (e: KeyboardEvent) => {
      const cible = e.target as HTMLElement | null;
      const enSaisie = !!cible && (cible.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(cible.tagName));
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") { e.preventDefault(); onOpenChange(true); }
      else if (e.key === "/" && !enSaisie && !e.ctrlKey && !e.metaKey && !e.altKey) { e.preventDefault(); onOpenChange(true); }
    };
    window.addEventListener("keydown", touche);
    return () => window.removeEventListener("keydown", touche);
  }, [onOpenChange]);

  useEffect(() => {
    if (open) { setSaisie(""); setTrouves([]); setActif(0); setRecents(lireRecents()); }
  }, [open]);

  // Recherche dans les données à partir de deux caractères, après une courte pause de frappe.
  useEffect(() => {
    const q = saisie.trim();
    if (!open || q.length < 2) { setTrouves([]); setEnCours(false); return; }
    let annule = false;
    setEnCours(true);
    const t = setTimeout(async () => {
      const p = `q=${encodeURIComponent(q)}&page=1&pageSize=5`;
      const [projets, devis, partenaires] = await Promise.all([
        ids.has("projets") ? api<Liste<{ id: string; code: string; libelle: string; client: string; statutLabel: string }>>(`/projets?${p}&supprimes=false`).catch(() => null) : null,
        ids.has("devis") ? api<Liste<{ id: string; numero: string; projet: string; client: string; etatLabel: string }>>(`/devis?${p}`).catch(() => null) : null,
        ids.has("clients") ? api<Liste<{ id: string; nom: string; typeLabel: string; ville: string | null }>>(`/partenaires?${p}`).catch(() => null) : null,
      ]);
      if (annule) return;
      setTrouves([
        ...(projets?.items ?? []).map((x) => ({ cle: `p${x.id}`, groupe: "Projets", titre: `${x.code}  ${x.libelle}`, detail: `${x.client}, ${x.statutLabel.toLowerCase()}`, href: `/projets/${x.id}` })),
        ...(devis?.items ?? []).map((x) => ({ cle: `d${x.id}`, groupe: "Devis", titre: `${x.numero}  ${x.projet}`, detail: `${x.client}, ${x.etatLabel.toLowerCase()}`, href: `/devis/${x.id}` })),
        ...(partenaires?.items ?? []).map((x) => ({ cle: `c${x.id}`, groupe: "Clients et partenaires", titre: x.nom, detail: [x.typeLabel, x.ville].filter(Boolean).join(", "), href: `/clients?fiche=${x.id}` })),
      ]);
      setEnCours(false);
    }, 250);
    return () => { annule = true; clearTimeout(t); };
  }, [saisie, open, ids]);

  const resultats = useMemo(() => {
    const q = sansAccent(saisie.trim());
    const pages: Resultat[] = menu.filter((m) => !q || sansAccent(m.label).includes(q))
      .map((m) => ({ cle: `m${m.id}`, groupe: "Aller à", titre: m.label, href: m.href, icone: "page", chemin: m.icon }));
    const actions: Resultat[] = peutGererCommercial(user) && (!q || "nouveau projet".includes(q))
      ? [{ cle: "a-projet", groupe: "Actions", titre: "Nouveau projet", href: "/projets?nouveau=1", icone: "action" }] : [];
    return q ? [...trouves, ...actions, ...pages] : [...recents, ...actions, ...pages];
  }, [saisie, menu, user, trouves, recents]);

  useEffect(() => { setActif(0); }, [saisie, trouves.length]);
  useEffect(() => { liste.current?.querySelector<HTMLElement>("[data-actif=true]")?.scrollIntoView({ block: "nearest" }); }, [actif]);

  const ouvrir = useCallback((r: Resultat) => {
    retenir(r);
    onOpenChange(false);
    router.push(r.href);
  }, [onOpenChange, router]);

  function clavier(e: React.KeyboardEvent) {
    if (e.key === "ArrowDown") { e.preventDefault(); setActif((a) => Math.min(a + 1, resultats.length - 1)); }
    else if (e.key === "ArrowUp") { e.preventDefault(); setActif((a) => Math.max(a - 1, 0)); }
    else if (e.key === "Enter" && resultats[actif]) { e.preventDefault(); ouvrir(resultats[actif]); }
  }

  const q = saisie.trim();
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-[60] bg-texte/40" />
        <Dialog.Content onKeyDown={clavier}
          className="apparition fixed left-1/2 top-[12vh] z-[60] flex max-h-[70vh] w-[calc(100%-2rem)] max-w-xl -translate-x-1/2 flex-col overflow-hidden rounded-xl bg-white shadow-2xl focus:outline-none">
          <Dialog.Title className="sr-only">Recherche</Dialog.Title>
          <Dialog.Description className="sr-only">Cherchez un projet, un devis, un client ou une page.</Dialog.Description>
          <div className="flex items-center gap-3 border-b border-bordure px-4">
            <Search size={18} className="shrink-0 text-attenue" aria-hidden />
            <input autoFocus value={saisie} onChange={(e) => setSaisie(e.target.value)} aria-label="Recherche"
              placeholder="Code de projet, numéro de devis, client, page..."
              className="h-14 w-full bg-transparent text-[15px] text-texte placeholder:text-attenue focus:outline-none" />
            <kbd className="shrink-0 rounded border border-bordure-forte px-1.5 py-0.5 font-sans text-[11px] text-secondaire">Échap</kbd>
          </div>
          <div ref={liste} className="min-h-0 flex-1 overflow-y-auto p-2">
            {resultats.map((r, i) => (
              <div key={r.cle}>
                {(i === 0 || resultats[i - 1].groupe !== r.groupe) && (
                  <div className="px-3 pb-1 pt-3 text-[12px] font-semibold text-attenue">{r.groupe}</div>
                )}
                <button type="button" data-actif={i === actif} onMouseMove={() => setActif(i)} onClick={() => ouvrir(r)}
                  className={cn("flex w-full cursor-pointer items-center gap-3 rounded-lg px-3 py-2.5 text-left", i === actif && "bg-neutre-fond")}>
                  <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-md bg-fond text-secondaire">
                    {r.chemin ? (
                      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden><path d={r.chemin} /></svg>
                    ) : r.icone === "action" ? <Plus size={15} aria-hidden /> : r.icone === "recent" ? <Clock size={15} aria-hidden /> : <Search size={14} aria-hidden />}
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate whitespace-pre font-semibold">{r.titre}</span>
                    {r.detail && <span className="block truncate text-[12px] text-secondaire">{r.detail}</span>}
                  </span>
                  {i === actif && <CornerDownLeft size={14} className="shrink-0 text-attenue" aria-hidden />}
                </button>
              </div>
            ))}
            {q.length >= 2 && enCours && trouves.length === 0 && <div className="px-3 py-3 text-[13px] text-secondaire">Recherche en cours...</div>}
            {resultats.length === 0 && !enCours && (
              <div className="px-3 py-8 text-center text-[13px] text-secondaire">Rien ne correspond à « {q} ». Essayez un code de projet, un numéro de devis ou un nom de client.</div>
            )}
          </div>
          <div className="flex items-center gap-4 border-t border-bordure bg-fond px-4 py-2 text-[12px] text-secondaire max-sm:hidden">
            <span><kbd className="font-sans font-semibold">↑ ↓</kbd> parcourir</span>
            <span><kbd className="font-sans font-semibold">Entrée</kbd> ouvrir</span>
            <span className="ml-auto"><kbd className="font-sans font-semibold">Ctrl K</kbd> ou <kbd className="font-sans font-semibold">/</kbd> pour rouvrir</span>
          </div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}

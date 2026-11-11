"use client";

import { Suspense, useCallback, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { Search, Users, Wallet, X } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn, formatDate, formatSurface } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input, Select } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";
import { Sheet } from "@/components/ui/sheet";
import { AttenteFiche, AttenteLignes } from "@/components/ui/attente";
import { SuiviDalle } from "../projets/[id]/suivi";
import { DepensesProjet } from "../projets/[id]/depenses";

type Etat = "a_affecter" | "preparation" | "a_realiser" | "commande" | "terminee";
type Ligne = {
  dalleId: string; projetId: string; projetCode: string; projetLibelle: string; ville: string | null; bloque: boolean;
  reference: string; niveauLabel: string; surface: number; etat: Etat; etatLabel: string;
  etapesFaites: number; etape: number; etapeLabel: string; attente: string | null; chefs: { userId: string; nom: string }[];
};
type Personne = { id: string; nom: string };
type Liste = {
  items: Ligne[]; total: number; page: number; pageSize: number;
  compteurs: { etat: string; label: string; nb: number }[]; chefs: Personne[] | null; peutAffecter: boolean;
};
type Chefs = {
  projetId: string; projetCode: string; projetLibelle: string; peutAffecter: boolean;
  chefs: { userId: string; nom: string; depuis: string }[]; historique: { nom: string; debut: string; fin: string | null }[];
};

const TON = { a_affecter: "orange", preparation: "neutre", a_realiser: "violet", commande: "neutre", terminee: "vert" } as const;

export default function ChantierPage() {
  return <Suspense><Chantier /></Suspense>;
}

/** Menu Chantier : les dalles à suivre sur le terrain, leur étape en cours et les chefs de chantier de leur projet. */
function Chantier() {
  const params = useSearchParams();
  const [etat, setEtat] = useState("");
  const [chef, setChef] = useState("");
  const [saisie, setSaisie] = useState(params.get("q") ?? "");
  const [q, setQ] = useState(params.get("q") ?? "");
  const [page, setPage] = useState(1);
  const [data, setData] = useState<Liste | null>(null);
  const [erreur, setErreur] = useState("");
  // Le tableau de bord ouvre directement le suivi d'une dalle.
  const [suivi, setSuivi] = useState<{ projetId: string; dalleId: string } | null>(() =>
    params.get("projet") && params.get("dalle") ? { projetId: params.get("projet")!, dalleId: params.get("dalle")! } : null);
  const [equipe, setEquipe] = useState<string | null>(null);
  const [depenses, setDepenses] = useState<Ligne | null>(null);

  useEffect(() => {
    const t = setTimeout(() => { setQ((ancien) => { if (ancien !== saisie.trim()) setPage(1); return saisie.trim(); }); }, 300);
    return () => clearTimeout(t);
  }, [saisie]);

  const charger = useCallback(async () => {
    try {
      const p = new URLSearchParams({ page: String(page) });
      if (etat) p.set("etat", etat);
      if (q) p.set("q", q);
      if (chef) p.set("chef", chef);
      setData(await api<Liste>(`/chantier?${p}`));
      setErreur("");
    } catch (e) {
      setErreur(e instanceof ApiError ? e.message : "Chargement impossible.");
    }
  }, [page, etat, q, chef]);

  useEffect(() => { charger(); }, [charger]);

  const compteurs = data?.compteurs ?? [];
  const onglets = [{ etat: "", label: "Toutes", nb: compteurs.reduce((s, c) => s + c.nb, 0) }, ...compteurs];
  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;
  const filtre = (v: string, set: (v: string) => void) => { set(v); setPage(1); };

  return (
    <div>
      <div className="mb-4 flex flex-wrap gap-2">
        {onglets.map((o) => (
          <button key={o.etat} aria-pressed={etat === o.etat} onClick={() => filtre(o.etat, setEtat)}
            className={cn("flex cursor-pointer items-center gap-2 rounded-full border px-3.5 py-1.5 text-[13px] font-semibold",
              etat === o.etat ? "border-marine bg-marine text-white" : "border-bordure-forte bg-white hover:bg-fond")}>
            {o.label}
            <span className={cn("font-display text-[12px]", etat === o.etat ? "text-white/80" : "text-attenue")}>{o.nb}</span>
          </button>
        ))}
      </div>

      <div className="mb-5 flex flex-wrap items-center gap-3">
        <div className="relative w-full max-w-xs">
          <Search size={16} className="pointer-events-none absolute left-3 top-3 text-attenue" />
          <Input className="pl-9" placeholder="Code, libellé ou ville du projet" value={saisie} onChange={(e) => setSaisie(e.target.value)} />
        </div>
        {data?.chefs && (
          <Select className="w-auto" value={chef} onChange={(e) => filtre(e.target.value, setChef)} aria-label="Chef de chantier">
            <option value="">Chef de chantier : tous</option>
            {data.chefs.map((i) => <option key={i.id} value={i.id}>{i.nom}</option>)}
          </Select>
        )}
        {data && !data.peutAffecter && <span className="text-[13px] text-secondaire">Dalles des chantiers où vous êtes affecté.</span>}
      </div>

      {erreur && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}

      <div className="overflow-x-auto rounded-xl border border-bordure bg-white">
        <table className="w-full text-left">
          <thead className="whitespace-nowrap border-b border-bordure bg-fond text-[12px] text-secondaire">
            <tr>
              <th className="px-4 py-3 font-semibold">Dalle</th>
              <th className="px-4 py-3 font-semibold">Projet</th>
              <th className="px-4 py-3 font-semibold">Niveau</th>
              <th className="px-4 py-3 font-semibold">Étape en cours</th>
              <th className="px-4 py-3 font-semibold">Chefs de chantier</th>
              <th className="px-4 py-3 font-semibold">État</th>
              <th className="px-4 py-3" aria-label="Actions" />
            </tr>
          </thead>
          <tbody>
            {data?.items.map((l) => (
              <tr key={l.dalleId} tabIndex={0} onClick={() => setSuivi({ projetId: l.projetId, dalleId: l.dalleId })}
                onKeyDown={(ev) => { if (ev.key === "Enter" && ev.target === ev.currentTarget) setSuivi({ projetId: l.projetId, dalleId: l.dalleId }); }}
                className="cursor-pointer border-b border-bordure last:border-0 hover:bg-fond">
                <td className="whitespace-nowrap px-4 py-3 font-display font-bold text-marine">{l.reference}</td>
                <td className="min-w-[200px] px-4 py-3">
                  <div className="font-semibold">{l.projetLibelle}</div>
                  {l.ville && <div className="text-[13px] text-secondaire">{l.ville}</div>}
                </td>
                <td className="whitespace-nowrap px-4 py-3 text-secondaire">
                  {l.niveauLabel}{l.surface > 0 && <div className="text-[12px]">{formatSurface(l.surface)}</div>}
                </td>
                <td className="min-w-[220px] px-4 py-3">
                  <div className="flex items-center gap-2">
                    <Avancement faites={l.etapesFaites} />
                    <span className="font-display text-[12px] font-bold text-secondaire">{l.etapesFaites}/11</span>
                  </div>
                  <div className="mt-1 font-semibold">{l.etapesFaites >= 11 ? l.etapeLabel : `${l.etape}. ${l.etapeLabel}`}</div>
                  {l.attente && <div className={cn("text-[12px]", l.bloque ? "text-rouge-texte" : "text-secondaire")}>{l.attente}</div>}
                </td>
                <td className="px-4 py-3">{l.chefs.length === 0 ? <span className="text-secondaire">Aucun</span> : l.chefs.map((c) => c.nom).join(", ")}</td>
                <td className="whitespace-nowrap px-4 py-3"><Badge ton={TON[l.etat]}>{l.etatLabel}</Badge></td>
                <td className="whitespace-nowrap px-4 py-3 text-right">
                  <div className="flex justify-end gap-1">
                    <button type="button" title="Chefs de chantier du projet" aria-label={`Chefs de chantier du projet ${l.projetCode}`}
                      onClick={(ev) => { ev.stopPropagation(); setEquipe(l.projetId); }}
                      className="cursor-pointer rounded-lg p-2 text-secondaire hover:bg-neutre-fond hover:text-texte"><Users size={16} /></button>
                    <button type="button" title="Dépenses du chantier" aria-label={`Dépenses du chantier ${l.projetCode}`}
                      onClick={(ev) => { ev.stopPropagation(); setDepenses(l); }}
                      className="cursor-pointer rounded-lg p-2 text-secondaire hover:bg-neutre-fond hover:text-texte"><Wallet size={16} /></button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {data && data.items.length === 0 && (
          <div className="px-6 py-14 text-center">
            <div className="font-display font-bold">Aucune dalle ne correspond</div>
            <div className="text-secondaire">
              {data.peutAffecter ? "Modifiez la recherche ou les filtres." : "Vous n'êtes affecté à aucun chantier pour l'instant : le responsable des chantiers affecte les chefs de chantier aux projets."}
            </div>
          </div>
        )}
        {!data && !erreur && <AttenteLignes />}
      </div>

      {data && (
        <div className="mt-4 flex items-center justify-between gap-3 text-[13px] text-secondaire">
          <span>{data.total} dalle(s)</span>
          {data.total > data.pageSize && (
            <span className="flex items-center gap-3">
              Page {page} sur {pages}
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>Précédent</Button>
              <Button variant="outline" size="sm" disabled={page >= pages} onClick={() => setPage(page + 1)}>Suivant</Button>
            </span>
          )}
        </div>
      )}

      {suivi && <SuiviDalle projetId={suivi.projetId} dalleId={suivi.dalleId} onClose={() => { setSuivi(null); charger(); }} onChange={() => charger()} />}

      {equipe && <ChefsDuProjet projetId={equipe} personnes={data?.chefs ?? []} onClose={() => { setEquipe(null); charger(); }} />}

      {depenses && (
        <Sheet large open onOpenChange={(o) => !o && setDepenses(null)} title={`Dépenses du chantier ${depenses.projetCode}`} description={depenses.projetLibelle}>
          <DepensesProjet projetId={depenses.projetId} />
        </Sheet>
      )}
    </div>
  );
}

/** Onze traits, un par étape : pleins quand l'étape est faite. */
function Avancement({ faites }: { faites: number }) {
  return (
    <div className="flex items-center gap-0.5" aria-hidden>
      {Array.from({ length: 11 }, (_, i) => <span key={i} className={cn("h-2 w-2.5 rounded-sm", i < faites ? "bg-[#1e9e58]" : "bg-bordure")} />)}
    </div>
  );
}

function ChefsDuProjet({ projetId, personnes, onClose }: { projetId: string; personnes: Personne[]; onClose: () => void }) {
  const [d, setD] = useState<Chefs | null>(null);
  const [erreur, setErreur] = useState("");
  const [envoi, setEnvoi] = useState(false);

  useEffect(() => {
    api<Chefs>(`/chantier/projets/${projetId}/chefs`).then(setD).catch((e) => setErreur(e instanceof ApiError ? e.message : "Chargement impossible."));
  }, [projetId]);

  async function agir(chemin: string, corps?: object) {
    setEnvoi(true); setErreur("");
    try { setD(await api<Chefs>(`/chantier/projets/${projetId}/${chemin}`, { method: "POST", body: JSON.stringify(corps ?? {}) })); }
    catch (e) { setErreur(e instanceof ApiError ? e.message : "Action impossible."); }
    finally { setEnvoi(false); }
  }

  const libres = d ? personnes.filter((i) => !d.chefs.some((x) => x.userId === i.id)) : [];

  return (
    <Modal open onOpenChange={(o) => !o && onClose()} title={d ? `Chefs de chantier du projet ${d.projetCode}` : "Chefs de chantier du projet"}
      description={d ? `${d.projetLibelle}. Un chef de chantier affecté suit toutes les dalles du projet.` : undefined}
      footer={<Button variant="outline" onClick={onClose}>Fermer</Button>}>
      {erreur && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}
      {!d ? (!erreur && <AttenteFiche className="border-0 p-0" />) : (
        <div className="space-y-5">
          {d.chefs.length === 0 ? (
            <p className="text-[13px] text-secondaire">
              Aucun chef de chantier n&apos;est affecté. {d.peutAffecter ? "Choisissez-en un ci-dessous." : "Le responsable des chantiers, le chef de projet ou le directeur affecte les chefs de chantier."}
            </p>
          ) : (
            <ul className="flex flex-wrap gap-2">
              {d.chefs.map((i) => (
                <li key={i.userId} className="flex items-center gap-1.5 rounded-full border border-bordure-forte bg-fond py-1 pl-3 pr-1.5 text-[13px] font-semibold">
                  {i.nom}
                  <span className="font-normal text-secondaire">depuis le {formatDate(i.depuis)}</span>
                  {d.peutAffecter ? (
                    <button type="button" disabled={envoi} aria-label={`Retirer ${i.nom} du chantier`} title="Retirer du chantier"
                      onClick={() => agir(`chefs/${i.userId}/retirer`)}
                      className="cursor-pointer rounded-full p-1 text-secondaire hover:bg-rouge-fond hover:text-rouge-texte"><X size={14} /></button>
                  ) : <span className="w-1.5" />}
                </li>
              ))}
            </ul>
          )}
          {d.peutAffecter && libres.length > 0 && (
            <Select className="w-auto" value="" disabled={envoi} aria-label="Affecter un chef de chantier"
              onChange={(ev) => { if (ev.target.value) agir("chefs", { userId: ev.target.value }); }}>
              <option value="">Affecter un chef de chantier</option>
              {libres.map((i) => <option key={i.id} value={i.id}>{i.nom}</option>)}
            </Select>
          )}
          {d.historique.some((h) => h.fin) && (
            <div>
              <h3 className="text-[14px] font-bold">Historique</h3>
              <ul className="mt-1 divide-y divide-bordure text-[13px]">
                {d.historique.map((h, i) => (
                  <li key={i} className="py-1.5">
                    <span className="font-semibold">{h.nom}</span>, du {formatDate(h.debut)} {h.fin ? `au ${formatDate(h.fin)}` : "à aujourd'hui"}
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}
    </Modal>
  );
}

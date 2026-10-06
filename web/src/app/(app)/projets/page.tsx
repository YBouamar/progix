"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Plus, Search } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { peutGererCommercial, useAuth } from "@/lib/auth";
import { cn, formatDate, formatSurface } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input, Select } from "@/components/ui/input";
import { ProjetSheet } from "./projet-sheet";
import { TON_STATUT, type ProjetListe, type Referentiel } from "./types";

const PAGE = 20;
const ANNEE = new Date().getFullYear();

export default function ProjetsPage() {
  const { user } = useAuth();
  const router = useRouter();
  const ecriture = peutGererCommercial(user);
  const [ref, setRef] = useState<Referentiel | null>(null);
  const [statut, setStatut] = useState("");
  const [entite, setEntite] = useState("");
  const [annee, setAnnee] = useState(String(ANNEE));
  const [supprimes, setSupprimes] = useState(false);
  const [saisie, setSaisie] = useState("");
  const [q, setQ] = useState("");
  const [page, setPage] = useState(1);
  const [data, setData] = useState<ProjetListe | null>(null);
  const [erreur, setErreur] = useState("");
  const [creation, setCreation] = useState(false);

  useEffect(() => { api<Referentiel>("/projets/referentiel").then(setRef).catch(() => undefined); }, []);

  useEffect(() => {
    const t = setTimeout(() => { setQ(saisie.trim()); setPage(1); }, 300);
    return () => clearTimeout(t);
  }, [saisie]);

  const charger = useCallback(async () => {
    try {
      const p = new URLSearchParams({ page: String(page), pageSize: String(PAGE), supprimes: String(supprimes) });
      if (statut) p.set("statut", statut);
      if (entite) p.set("entite", entite);
      if (annee) p.set("annee", annee);
      if (q) p.set("q", q);
      setData(await api<ProjetListe>(`/projets?${p}`));
      setErreur("");
    } catch (e) {
      setErreur(e instanceof ApiError ? e.message : "Chargement impossible.");
    }
  }, [page, statut, entite, annee, q, supprimes]);

  useEffect(() => { charger(); }, [charger]);

  const compteurs = data?.compteurs ?? [];
  const onglets = [{ statut: "", label: "Tous", nb: compteurs.reduce((s, c) => s + c.nb, 0) }, ...compteurs];
  const pages = data ? Math.max(1, Math.ceil(data.total / PAGE)) : 1;
  const filtre = (v: string, set: (v: string) => void) => { set(v); setPage(1); };

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <div className="flex flex-wrap gap-2">
          {onglets.map((o) => (
            <button key={o.statut} aria-pressed={statut === o.statut} onClick={() => filtre(o.statut, setStatut)}
              className={cn("flex cursor-pointer items-center gap-2 rounded-full border px-3.5 py-1.5 text-[13px] font-semibold",
                statut === o.statut ? "border-marine bg-marine text-white" : "border-bordure-forte bg-white hover:bg-fond")}>
              {o.label}
              <span className={cn("font-display text-[12px]", statut === o.statut ? "text-white/80" : "text-attenue")}>{o.nb}</span>
            </button>
          ))}
        </div>
        {ecriture && <Button className="ml-auto" onClick={() => setCreation(true)}><Plus size={16} />Nouveau projet</Button>}
      </div>

      <div className="mb-5 flex flex-wrap items-center gap-3">
        <div className="relative w-full max-w-xs">
          <Search size={16} className="pointer-events-none absolute left-3 top-3 text-attenue" />
          <Input className="pl-9" placeholder="Code, libellé, client ou ville" value={saisie} onChange={(e) => setSaisie(e.target.value)} />
        </div>
        <Select className="w-auto" value={annee} onChange={(e) => filtre(e.target.value, setAnnee)} aria-label="Période">
          <option value={ANNEE}>Période : {ANNEE}</option>
          <option value={ANNEE - 1}>{ANNEE - 1}</option>
          <option value="">Toutes</option>
        </Select>
        <Select className="w-auto" value={entite} onChange={(e) => filtre(e.target.value, setEntite)} aria-label="Entité">
          <option value="">Entité : toutes</option>
          {ref?.entites.map((e) => <option key={e.code} value={e.code}>{e.code}</option>)}
        </Select>
        <label className="ml-auto flex cursor-pointer items-center gap-2 text-[13px] font-semibold text-secondaire">
          <input type="checkbox" className="h-4 w-4 accent-marine" checked={supprimes}
            onChange={(e) => { setSupprimes(e.target.checked); setPage(1); }} />
          Projets supprimés
        </label>
      </div>

      {erreur && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}

      <div className="overflow-hidden rounded-xl border border-bordure bg-white">
        <table className="w-full text-left">
          <thead className="border-b border-bordure bg-fond text-[12px] uppercase tracking-wide text-secondaire">
            <tr>
              <th className="px-5 py-3 font-semibold">Code</th>
              <th className="px-5 py-3 font-semibold">Libellé et client</th>
              <th className="px-5 py-3 font-semibold">Ville</th>
              <th className="px-5 py-3 font-semibold">Créé le</th>
              <th className="px-5 py-3 font-semibold">Entité</th>
              <th className="px-5 py-3 font-semibold">Type</th>
              <th className="px-5 py-3 font-semibold">Ingénieur</th>
              <th className="px-5 py-3 font-semibold">Statut</th>
              <th className="px-5 py-3 text-right font-semibold">Dalles</th>
              <th className="px-5 py-3 text-right font-semibold">Surface</th>
            </tr>
          </thead>
          <tbody>
            {data?.items.map((p) => (
              <tr key={p.id} onClick={() => router.push(`/projets/${p.id}`)}
                className={cn("cursor-pointer border-b border-bordure last:border-0 hover:bg-fond", p.supprime && "bg-fond text-secondaire")}>
                <td className={cn("whitespace-nowrap px-5 py-3 font-display font-bold", p.supprime ? "text-secondaire" : "text-marine")}>{p.code}</td>
                <td className="px-5 py-3">
                  <div className="font-semibold">{p.libelle}</div>
                  <div className="text-[13px] text-secondaire">{p.supprime ? p.suppressionInfo : p.client}</div>
                </td>
                <td className="px-5 py-3 text-secondaire">{p.ville}</td>
                <td className="whitespace-nowrap px-5 py-3 font-display text-secondaire">{formatDate(p.dateCreation)}</td>
                <td className="px-5 py-3 font-display">{p.entite}</td>
                <td className="px-5 py-3 text-secondaire">{p.typeLabel}</td>
                <td className="px-5 py-3 text-secondaire">{p.ingenieurs || "Non assigné"}</td>
                <td className="whitespace-nowrap px-5 py-3">
                  <Badge ton={TON_STATUT[p.statut] ?? "neutre"}>{p.statutLabel}</Badge>
                  {p.bloque && <Badge ton="rouge" className="ml-1.5">Bloqué</Badge>}
                </td>
                <td className="whitespace-nowrap px-5 py-3 text-right font-display">{p.dallesRealisees} sur {p.nbDalles}</td>
                <td className="whitespace-nowrap px-5 py-3 text-right font-display">{formatSurface(p.surface)}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {data && data.items.length === 0 && (
          <div className="px-6 py-14 text-center">
            <div className="font-display font-bold">Aucun projet ne correspond</div>
            <div className="text-secondaire">Modifiez la recherche ou les filtres.</div>
          </div>
        )}
        {!data && !erreur && <div className="px-6 py-14 text-center text-secondaire">Chargement...</div>}
      </div>

      {data && (
        <div className="mt-4 flex items-center justify-between gap-3 text-[13px] text-secondaire">
          <span>{data.total} {supprimes ? "projet(s) supprimé(s), en lecture seule" : "projet(s)"}</span>
          {data.total > PAGE && (
            <span className="flex items-center gap-3">
              Page {page} sur {pages}
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>Précédent</Button>
              <Button variant="outline" size="sm" disabled={page >= pages} onClick={() => setPage(page + 1)}>Suivant</Button>
            </span>
          )}
        </div>
      )}

      {creation && ref && (
        <ProjetSheet projet={null} referentiel={ref} onClose={() => setCreation(false)}
          onSaved={(p) => router.push(`/projets/${p.id}`)} />
      )}
    </div>
  );
}

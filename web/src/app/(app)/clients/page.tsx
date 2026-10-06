"use client";

import { useCallback, useEffect, useState } from "react";
import { Plus, Search } from "lucide-react";
import { api, ApiError, type Paged } from "@/lib/api";
import { peutGererCommercial, useAuth } from "@/lib/auth";
import { cn } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { PartenaireSheet } from "./partenaire-sheet";
import type { Compteur, PartenaireListItem } from "./types";

const PAGE = 20;

export default function ClientsPage() {
  const { user } = useAuth();
  const ecriture = peutGererCommercial(user);
  const [compteurs, setCompteurs] = useState<Compteur[]>([]);
  const [type, setType] = useState("");
  const [saisie, setSaisie] = useState("");
  const [q, setQ] = useState("");
  const [page, setPage] = useState(1);
  const [data, setData] = useState<Paged<PartenaireListItem> | null>(null);
  const [erreur, setErreur] = useState("");
  const [fiche, setFiche] = useState<{ id: string | null } | null>(null);

  useEffect(() => {
    const t = setTimeout(() => { setQ(saisie.trim()); setPage(1); }, 300);
    return () => clearTimeout(t);
  }, [saisie]);

  const charger = useCallback(async () => {
    try {
      const p = new URLSearchParams({ page: String(page), pageSize: String(PAGE) });
      if (type) p.set("type", type);
      if (q) p.set("q", q);
      const [liste, c] = await Promise.all([
        api<Paged<PartenaireListItem>>(`/partenaires?${p}`),
        api<Compteur[]>("/partenaires/compteurs"),
      ]);
      setData(liste);
      setCompteurs(c);
      setErreur("");
    } catch (e) {
      setErreur(e instanceof ApiError ? e.message : "Chargement impossible.");
    }
  }, [page, type, q]);

  useEffect(() => { charger(); }, [charger]);

  const total = compteurs.reduce((s, c) => s + c.nb, 0);
  const onglets = [{ type: "", label: "Tous", nb: total }, ...compteurs];
  const pages = data ? Math.max(1, Math.ceil(data.total / PAGE)) : 1;

  return (
    <div>
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <div className="flex flex-wrap gap-1 rounded-xl border border-bordure bg-white p-1">
          {onglets.map((o) => (
            <button key={o.type} onClick={() => { setType(o.type); setPage(1); }}
              className={cn("flex cursor-pointer items-center gap-2 rounded-lg px-3 py-1.5 text-[13px] font-semibold",
                type === o.type ? "bg-marine text-white" : "text-secondaire hover:bg-neutre-fond")}>
              {o.label}
              <span className={cn("rounded-full px-1.5 font-display text-[12px]", type === o.type ? "bg-white/20" : "bg-neutre-fond")}>{o.nb}</span>
            </button>
          ))}
        </div>
        <div className="relative ml-auto w-full max-w-xs">
          <Search size={16} className="pointer-events-none absolute left-3 top-3 text-attenue" />
          <Input className="pl-9" placeholder="Nom, ICE, CIN, ville" value={saisie} onChange={(e) => setSaisie(e.target.value)} />
        </div>
        {ecriture && (
          <Button onClick={() => setFiche({ id: null })}><Plus size={16} />Nouveau</Button>
        )}
      </div>

      {erreur && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}

      <div className="overflow-hidden rounded-xl border border-bordure bg-white">
        <table className="w-full text-left">
          <thead className="border-b border-bordure bg-fond text-[12px] uppercase tracking-wide text-secondaire">
            <tr>
              <th className="px-5 py-3 font-semibold">Nom ou raison sociale</th>
              <th className="px-5 py-3 font-semibold">Type</th>
              <th className="px-5 py-3 font-semibold">Catégorie</th>
              <th className="px-5 py-3 font-semibold">Ville</th>
              <th className="px-5 py-3 font-semibold">Téléphone</th>
              <th className="px-5 py-3 text-right font-semibold">Contacts</th>
            </tr>
          </thead>
          <tbody>
            {data?.items.map((r) => (
              <tr key={r.id} onClick={() => setFiche({ id: r.id })}
                className="cursor-pointer border-b border-bordure last:border-0 hover:bg-fond">
                <td className="px-5 py-3">
                  <div className="font-semibold">{r.nom}</div>
                  <div className="font-display text-[12px] text-attenue">
                    {r.identifiant ? `${r.nature === "physique" ? "CIN" : "ICE"} ${r.identifiant}` : r.nature === "autre" ? "" : "Identifiant non renseigné"}
                  </div>
                </td>
                <td className="px-5 py-3">
                  <Badge ton={r.type === "client_final" ? "violet" : "neutre"}>{r.typeLabel}</Badge>
                  {r.exonereTva && <Badge ton="orange" className="ml-1.5">Exonéré de TVA</Badge>}
                </td>
                <td className="px-5 py-3 text-secondaire">{r.nature === "morale" ? "Personne morale" : r.nature === "physique" ? "Personne physique" : "Autre"}</td>
                <td className="px-5 py-3 text-secondaire">{r.ville}</td>
                <td className="px-5 py-3 font-display text-secondaire">{r.telephone}</td>
                <td className="px-5 py-3 text-right font-display">{r.nbContacts}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {data && data.items.length === 0 && (
          <div className="px-6 py-14 text-center">
            <div className="font-display font-bold">Aucun partenaire ne correspond</div>
            <div className="text-secondaire">Modifiez la recherche ou le type sélectionné.</div>
          </div>
        )}
        {!data && !erreur && <div className="px-6 py-14 text-center text-secondaire">Chargement...</div>}
      </div>

      {data && data.total > PAGE && (
        <div className="mt-4 flex items-center justify-end gap-3 text-[13px] text-secondaire">
          <span>Page {page} sur {pages} · {data.total} résultats</span>
          <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>Précédent</Button>
          <Button variant="outline" size="sm" disabled={page >= pages} onClick={() => setPage(page + 1)}>Suivant</Button>
        </div>
      )}

      {fiche && (
        <PartenaireSheet id={fiche.id} ecriture={ecriture} types={compteurs}
          onClose={() => setFiche(null)} onSaved={() => { setFiche(null); charger(); }} />
      )}
    </div>
  );
}

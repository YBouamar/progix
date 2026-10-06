"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Search } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn, formatDH } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { TON_ETAT, type DevisListe } from "./types";

const PAGE = 20;

export default function DevisPage() {
  const router = useRouter();
  const [etat, setEtat] = useState("");
  const [saisie, setSaisie] = useState("");
  const [q, setQ] = useState("");
  const [page, setPage] = useState(1);
  const [data, setData] = useState<DevisListe | null>(null);
  const [erreur, setErreur] = useState("");

  useEffect(() => {
    const t = setTimeout(() => { setQ(saisie.trim()); setPage(1); }, 300);
    return () => clearTimeout(t);
  }, [saisie]);

  const charger = useCallback(async () => {
    try {
      const p = new URLSearchParams({ page: String(page), pageSize: String(PAGE) });
      if (etat) p.set("etat", etat);
      if (q) p.set("q", q);
      setData(await api<DevisListe>(`/devis?${p}`));
      setErreur("");
    } catch (e) {
      setErreur(e instanceof ApiError ? e.message : "Chargement impossible.");
    }
  }, [page, etat, q]);

  useEffect(() => { charger(); }, [charger]);

  const compteurs = data?.compteurs ?? [];
  const onglets = [{ etat: "", label: "Tous", nb: compteurs.reduce((s, c) => s + c.nb, 0) }, ...compteurs];
  const pages = data ? Math.max(1, Math.ceil(data.total / PAGE)) : 1;

  return (
    <div>
      <div className="mb-4 flex flex-wrap gap-2">
        {onglets.map((o) => (
          <button key={o.etat} aria-pressed={etat === o.etat} onClick={() => { setEtat(o.etat); setPage(1); }}
            className={cn("flex cursor-pointer items-center gap-2 rounded-full border px-3.5 py-1.5 text-[13px] font-semibold",
              etat === o.etat ? "border-marine bg-marine text-white" : "border-bordure-forte bg-white hover:bg-fond")}>
            {o.label}
            <span className={cn("font-display text-[12px]", etat === o.etat ? "text-white/80" : "text-attenue")}>{o.nb}</span>
          </button>
        ))}
      </div>
      <div className="mb-5 flex items-center gap-3">
        <div className="relative w-full max-w-xs">
          <Search size={16} className="pointer-events-none absolute left-3 top-3 text-attenue" />
          <Input className="pl-9" placeholder="Numéro, projet" value={saisie} onChange={(e) => setSaisie(e.target.value)} />
        </div>
        <span className="ml-auto text-[13px] text-secondaire">Un devis se crée depuis la fiche du projet.</span>
      </div>

      {erreur && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}

      <div className="overflow-hidden rounded-xl border border-bordure bg-white">
        <table className="w-full text-left">
          <thead className="border-b border-bordure bg-fond text-[12px] uppercase tracking-wide text-secondaire">
            <tr>
              <th className="px-5 py-3 font-semibold">Devis</th>
              <th className="px-5 py-3 font-semibold">Projet et client</th>
              <th className="px-5 py-3 font-semibold">Commercial</th>
              <th className="px-5 py-3 font-semibold">État</th>
              <th className="px-5 py-3 font-semibold">Échéance</th>
              <th className="px-5 py-3 text-right font-semibold">Montant TTC</th>
            </tr>
          </thead>
          <tbody>
            {data?.items.map((d) => (
              <tr key={d.id} onClick={() => router.push(`/devis/${d.id}`)} className="cursor-pointer border-b border-bordure last:border-0 hover:bg-fond">
                <td className="whitespace-nowrap px-5 py-3">
                  <div className={cn("font-display font-bold text-marine", d.etat === "remplace" && "line-through")}>{d.numero}</div>
                  <div className="text-[12px] text-attenue">{d.entite} · Version {d.version}</div>
                </td>
                <td className="px-5 py-3">
                  <div className="font-semibold">{d.projet}</div>
                  <div className="text-[13px] text-secondaire">{d.client}</div>
                </td>
                <td className="px-5 py-3 text-secondaire">{d.commercial ?? "Non renseigné"}</td>
                <td className="px-5 py-3"><Badge ton={TON_ETAT[d.etat] ?? "neutre"}>{d.etatLabel}</Badge></td>
                <td className="whitespace-nowrap px-5 py-3 text-secondaire">
                  {d.info}{d.alerte && <span className={d.alerte.startsWith("expire") ? "font-semibold text-rouge-texte" : "font-semibold text-orange-texte"}> · {d.alerte}</span>}
                </td>
                <td className="whitespace-nowrap px-5 py-3 text-right font-display font-bold">{formatDH(d.totalTTC)}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {data && data.items.length === 0 && (
          <div className="px-6 py-14 text-center">
            <div className="font-display font-bold">Aucun devis ne correspond</div>
            <div className="text-secondaire">Modifiez la recherche ou l&apos;état sélectionné.</div>
          </div>
        )}
      </div>

      {data && (
        <div className="mt-4 flex items-center justify-between gap-3 text-[13px] text-secondaire">
          <span>{data.total} devis</span>
          {data.total > PAGE && (
            <span className="flex items-center gap-3">
              Page {page} sur {pages}
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>Précédent</Button>
              <Button variant="outline" size="sm" disabled={page >= pages} onClick={() => setPage(page + 1)}>Suivant</Button>
            </span>
          )}
        </div>
      )}
    </div>
  );
}

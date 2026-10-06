"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { api } from "@/lib/api";
import { formatDate, formatNombre } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";

type Commande = { dalleId: string; projetId: string; reference: string; projet: string; entite: string; demandee: string; etat: string; etatLabel: string };
type Commandes = { compteurs: { etat: string; label: string; nb: number }[]; commandes: Commande[] };
type Stock = { entite: string; articles: { code: string; article: string; restant: number; seuil: number; bas: boolean }[] };
const TON: Record<string, "neutre" | "orange" | "vert"> = { attente: "neutre", validee: "orange", fabrication: "orange", livree: "orange", recue: "vert" };

export function TableauLogistique() {
  const [c, setC] = useState<Commandes | null>(null);
  const [stock, setStock] = useState<Stock[]>([]);
  useEffect(() => {
    api<Commandes>("/logistique/commandes").then(setC).catch(() => setC(null));
    Promise.all(["APT", "BBR"].map((e) => api<Stock>(`/logistique/stock?entite=${e}`).catch(() => null))).then((r) => setStock(r.filter((x): x is Stock => !!x)));
  }, []);
  const bas = stock.flatMap((s) => s.articles.filter((a) => a.bas).map((a) => ({ ...a, entite: s.entite })));
  const aTraiter = (c?.commandes ?? []).filter((x) => x.etat !== "recue");

  return (
    <div>
      <div className="mb-5 rounded-xl border border-bordure bg-white p-5">
        <div className="flex items-center justify-between gap-3"><div className="font-display font-bold">Commandes usine</div><div className="text-[13px] text-secondaire">{c?.commandes.length ?? 0} commande(s)</div></div>
        <div className="mt-4 grid gap-3 sm:grid-cols-3 xl:grid-cols-5">
          {c?.compteurs.map((e) => (
            <Link key={e.etat} href="/logistique" className="rounded-lg border border-bordure px-4 py-3 hover:border-marine">
              <Badge ton={TON[e.etat] ?? "neutre"}>{e.label}</Badge><div className="mt-2 font-display text-2xl font-bold">{e.nb}</div>
            </Link>
          ))}
        </div>
      </div>

      <div className="mb-5 overflow-x-auto rounded-xl border border-bordure bg-white">
        <div className="flex items-center justify-between gap-3 border-b border-bordure px-5 py-3">
          <div className="font-display font-bold">Stock bas</div><div className="text-[13px] text-secondaire">Articles sous leur seuil d&apos;alerte</div>
        </div>
        {bas.length === 0 ? <div className="px-5 py-8 text-center text-[13px] text-secondaire">Aucun article sous son seuil.</div> : (
          <table className="w-full text-left text-[13px]">
            <thead className="border-b border-bordure bg-fond text-[12px] uppercase tracking-wide text-secondaire">
              <tr><th className="px-5 py-2.5 font-semibold">Article</th><th className="px-5 py-2.5 font-semibold">Entité</th><th className="px-5 py-2.5 text-right font-semibold">Restant</th><th className="px-5 py-2.5 text-right font-semibold">Seuil</th><th className="px-5 py-2.5 font-semibold">Niveau</th></tr>
            </thead>
            <tbody>
              {bas.map((a) => (
                <tr key={`${a.entite}-${a.code}`} className="border-b border-bordure last:border-0">
                  <td className="px-5 py-2.5 font-semibold">{a.article}</td><td className="px-5 py-2.5">{a.entite}</td>
                  <td className="px-5 py-2.5 text-right font-display font-bold text-rouge-texte">{formatNombre(a.restant)}</td><td className="px-5 py-2.5 text-right font-display">{formatNombre(a.seuil)}</td>
                  <td className="px-5 py-2.5"><span className="block h-2 w-32 overflow-hidden rounded-full bg-fond"><span className="block h-full rounded-full bg-rouge-texte" style={{ width: `${Math.max(0, Math.min(100, a.seuil ? a.restant / a.seuil * 100 : 0))}%` }} /></span></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <div className="overflow-x-auto rounded-xl border border-bordure bg-white">
        <div className="flex items-center justify-between gap-3 border-b border-bordure px-5 py-3">
          <div className="font-display font-bold">Commandes à traiter</div><Link href="/logistique" className="text-[13px] font-semibold text-marine hover:underline">Ouvrir la logistique</Link>
        </div>
        {aTraiter.length === 0 ? <div className="px-5 py-8 text-center text-[13px] text-secondaire">Aucune commande à traiter.</div> : (
          <table className="w-full text-left text-[13px]">
            <thead className="border-b border-bordure bg-fond text-[12px] uppercase tracking-wide text-secondaire">
              <tr><th className="px-5 py-2.5 font-semibold">Dalle</th><th className="px-5 py-2.5 font-semibold">Projet</th><th className="px-5 py-2.5 font-semibold">Demandée le</th><th className="px-5 py-2.5 font-semibold">État</th></tr>
            </thead>
            <tbody>
              {aTraiter.map((x) => (
                <tr key={x.dalleId} className="border-b border-bordure last:border-0">
                  <td className="whitespace-nowrap px-5 py-2.5"><Link href={`/projets/${x.projetId}`} className="font-display font-bold text-marine hover:underline">{x.reference}</Link></td>
                  <td className="px-5 py-2.5">{x.projet}</td><td className="whitespace-nowrap px-5 py-2.5 font-display text-secondaire">{formatDate(x.demandee)}</td>
                  <td className="px-5 py-2.5"><Badge ton={TON[x.etat] ?? "neutre"}>{x.etatLabel}</Badge></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}

"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { api } from "@/lib/api";
import { cn, formatDate, formatDH } from "@/lib/utils";
import { periodes, type DevisTdb, type Finances } from "./donnees";

type Ligne = { id: string; ref: string; client: string; info: string; montant: number; href: string };

export function TableauCommercial() {
  const mois = useMemo(() => periodes()[0], []);
  const [f, setF] = useState<Finances | null>(null);
  const [d, setD] = useState<DevisTdb | null>(null);
  useEffect(() => {
    api<Finances>(`/tableau-de-bord/finances?${new URLSearchParams({ debut: mois.debut, fin: mois.fin })}`).then(setF).catch(() => setF(null));
    api<DevisTdb>("/devis/tableau-de-bord").then(setD).catch(() => setD(null));
  }, [mois]);

  const cartes: [string, string, string][] = f && d ? [
    [`Chiffre d'affaires, ${mois.label}`, formatDH(f.periode.chiffreAffaires), "Factures émises, TTC après avoirs"],
    ["Devis envoyés en attente", `${d.enAttente} devis`, `Dont ${d.aRelancer.length} à relancer et ${d.expirent.length} qui expirent`],
    ["Devis validés ce mois", `${d.validesMois} devis`, `Sur ${d.envoyesMois} devis envoyés ce mois`],
    ["Paiements en retard", formatDH(f.retards.total), `${f.retards.nb} facture(s) échue(s)`],
  ] : [];
  const j = (n: number) => `${n} jour${n > 1 ? "s" : ""}`;
  const blocs: { titre: string; regle: string; c1: string; c3: string; cta: string; rouge: boolean; lignes: Ligne[] }[] = [
    { titre: "Devis à relancer", regle: "Envoyés depuis 7 jours ou plus, sans réponse", c1: "Devis", c3: "Envoyé le", cta: "Ouvrir", rouge: false,
      lignes: (d?.aRelancer ?? []).map((x) => ({ id: x.id, ref: x.numero, client: x.client, info: `${x.envoyeLe ? formatDate(x.envoyeLe) : ""} · ${j(x.joursDepuisEnvoi)}`, montant: x.totalTTC, href: `/devis/${x.id}` })) },
    { titre: "Devis qui expirent", regle: "Échéance dans 7 jours au plus, d'après la durée de validité du devis", c1: "Devis", c3: "Échéance", cta: "Ouvrir", rouge: true,
      lignes: (d?.expirent ?? []).map((x) => ({ id: x.id, ref: x.numero, client: x.client, info: `${formatDate(x.echeance)} · ${x.joursAvantExpiration <= 0 ? "aujourd'hui" : `dans ${j(x.joursAvantExpiration)}`}`, montant: x.totalTTC, href: `/devis/${x.id}` })) },
    { titre: "Paiements en retard", regle: "Factures échues, non soldées", c1: "Facture", c3: "Échéance", cta: "Voir", rouge: true,
      lignes: (f?.retards.factures ?? []).map((x) => ({ id: x.id, ref: x.numero, client: x.client, info: `${formatDate(x.echeance)} · ${j(x.jours)} de retard`, montant: x.reste, href: "/facturation" })) },
  ];

  return (
    <div>
      <div className="mb-5 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {cartes.map(([label, v, how]) => (
          <div key={label} className="rounded-xl border border-bordure bg-white p-5">
            <div className="text-[13px] text-secondaire first-letter:uppercase">{label}</div>
            <div className="mt-1 font-display text-xl font-bold">{v}</div>
            <div className="mt-1 text-[12px] text-attenue">{how}</div>
          </div>
        ))}
      </div>
      <div className="space-y-5">
        {blocs.map((b) => (
          <div key={b.titre} className="overflow-x-auto rounded-xl border border-bordure bg-white">
            <div className="flex flex-wrap items-center gap-3 border-b border-bordure px-5 py-3">
              <span className={cn("h-2.5 w-2.5 rounded-full", b.rouge ? "bg-rouge-texte" : "bg-orange-texte")} />
              <span className="font-display font-bold">{b.titre}</span>
              <span className={cn("rounded-full px-2 py-0.5 font-display text-[12px] font-bold", b.rouge ? "bg-rouge-fond text-rouge-texte" : "bg-orange-fond text-orange-texte")}>{b.lignes.length}</span>
              <span className="text-[13px] text-secondaire">{b.regle}</span>
            </div>
            {b.lignes.length === 0 ? <div className="px-5 py-6 text-center text-[13px] text-secondaire">Rien à signaler.</div> : (
              <table className="w-full text-left text-[13px]">
                <thead className="border-b border-bordure bg-fond text-[12px] uppercase tracking-wide text-secondaire">
                  <tr><th className="px-5 py-2.5 font-semibold">{b.c1}</th><th className="px-5 py-2.5 font-semibold">Client</th><th className="px-5 py-2.5 font-semibold">{b.c3}</th><th className="px-5 py-2.5 text-right font-semibold">Montant TTC</th><th className="px-5 py-2.5" /></tr>
                </thead>
                <tbody>
                  {b.lignes.map((l) => (
                    <tr key={l.id} className="border-b border-bordure last:border-0">
                      <td className="whitespace-nowrap px-5 py-2.5 font-display font-bold text-marine">{l.ref}</td><td className="px-5 py-2.5 font-semibold">{l.client}</td>
                      <td className="whitespace-nowrap px-5 py-2.5 text-secondaire">{l.info}</td><td className="whitespace-nowrap px-5 py-2.5 text-right font-display font-bold">{formatDH(l.montant)}</td>
                      <td className="px-5 py-2.5 text-right"><Link href={l.href} className="font-semibold text-marine hover:underline">{b.cta}</Link></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

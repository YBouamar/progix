"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { cn, formatDH } from "@/lib/utils";

type Situation = { commande: number; facture: number; avoirs: number; encaisse: number; effetsEnAttente: number; resteDu: number; resteAFacturer: number; resteSurFactures: number };

/** Situation financière du projet : reste dû = commandé (devis validés, TTC) moins avoirs moins encaissé. */
export function SituationProjet({ projetId }: { projetId: string }) {
  const [s, setS] = useState<Situation | null>(null);
  useEffect(() => { api<Situation>(`/factures/situation?projetId=${projetId}`).then(setS).catch(() => setS(null)); }, [projetId]);
  if (!s) return null;

  const cases: [string, number, string][] = [
    ["Commandé, TTC", s.commande, ""],
    ["Facturé, TTC", s.facture, ""],
    ["Avoirs", s.avoirs, ""],
    ["Encaissé", s.encaisse, "text-vert-texte"],
    ["Reste dû", s.resteDu, s.resteDu > 0 ? "text-orange-texte" : ""],
  ];
  return (
    <section className="mt-6 rounded-xl border border-bordure bg-white">
      <div className="border-b border-bordure px-6 py-4">
        <h2 className="font-display text-[15px] font-bold">Situation financière</h2>
        <div className="text-[13px] text-secondaire">Reste dû = commandé (devis validés, TTC) moins avoirs moins encaissé.</div>
      </div>
      <div className="grid gap-4 px-6 py-4 sm:grid-cols-5">
        {cases.map(([label, montant, ton]) => (
          <div key={label}>
            <div className="text-[12px] font-semibold text-secondaire">{label}</div>
            <div className={cn("font-display text-[15px] font-bold", ton)}>{formatDH(montant)}</div>
          </div>
        ))}
      </div>
      <div className="border-t border-bordure px-6 py-3 text-[12px] text-secondaire">
        Reste à facturer : {formatDH(s.resteAFacturer)} · reste sur les factures émises : {formatDH(s.resteSurFactures)}
        {s.effetsEnAttente > 0 && ` · effets en attente d'échéance : ${formatDH(s.effetsEnAttente)}`}
        {s.commande === 0 && " · aucun devis validé."}
      </div>
    </section>
  );
}

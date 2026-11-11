"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { cn, formatDH } from "@/lib/utils";
import { BarresHorizontales, Colonnes, COULEURS_SERIES } from "@/components/graphes";
import { Select } from "@/components/ui/input";

type Paiements = {
  annee: number; annees?: number[]; moisCourant?: number | null;
  modes: { mode: string; label: string; encaisse: number; nombre: number; enAttente: number; rembourse: number; annules: number }[];
  mois: { mois: number; parMode: Record<string, number> }[];
};

const MOIS = ["Janv.", "Févr.", "Mars", "Avr.", "Mai", "Juin", "Juil.", "Août", "Sept.", "Oct.", "Nov.", "Déc."];
const MOIS_LONGS = ["Janvier", "Février", "Mars", "Avril", "Mai", "Juin", "Juillet", "Août", "Septembre", "Octobre", "Novembre", "Décembre"];

function Carte({ titre, aide, children }: { titre: string; aide?: string; children: React.ReactNode }) {
  return (
    <section className="rounded-xl border border-bordure bg-white px-5 py-4">
      <h3 className="text-[14px] font-bold">{titre}</h3>
      {aide && <p className="mb-3 mt-0.5 text-[12px] text-secondaire">{aide}</p>}
      {children}
    </section>
  );
}

/** État des paiements d'une année, par mode et par mois. N'affiche rien si la personne n'y a pas accès. */
export function EtatPaiementsAnnee() {
  const [annee, setAnnee] = useState<number | null>(null);
  const [p, setP] = useState<Paiements | null>(null);
  useEffect(() => {
    api<Paiements>(`/statistiques/paiements${annee ? `?annee=${annee}` : ""}`).then(setP).catch(() => setP(null));
  }, [annee]);
  if (!p) return null;
  const surligne = p.moisCourant ? p.moisCourant - 1 : null;
  return (
    <div className="mt-8">
      <div className="mb-3 flex flex-wrap items-center gap-3">
        <h2 className="font-display text-[16px] font-bold">État des paiements sur l&apos;année</h2>
        <Select className="w-40" aria-label="Année" value={p.annee} onChange={(e) => setAnnee(Number(e.target.value))}>
          {(p.annees ?? [p.annee]).map((a) => <option key={a} value={a}>Année {a}</option>)}
        </Select>
      </div>
      <div className="grid gap-4 lg:grid-cols-2">
        <EtatPaiements p={p} annee={p.annee} surligne={surligne} libelleMois={(i) => `${MOIS_LONGS[i]} ${p.annee}`} />
      </div>
    </div>
  );
}

/** État des paiements de l'année par mode, et encaissé par mois et par mode. */
function EtatPaiements({ p, annee, surligne, libelleMois }: { p: Paiements; annee: number; surligne: number | null; libelleMois: (i: number) => string }) {
  const couleur = (mode: string) => COULEURS_SERIES[Math.max(0, p.modes.findIndex((m) => m.mode === mode))];
  const total = p.modes.reduce((s, m) => s + m.encaisse, 0);
  return (
    <>
      <Carte titre={`État des paiements par mode, ${annee}`} aide="Encaissé dans l'année (un effet compte à son échéance), remboursements à part.">
        <BarresHorizontales unite="Encaissé" format={formatDH}
          donnees={p.modes.map((m) => ({ id: m.mode, label: m.label, valeur: m.encaisse, couleur: couleur(m.mode), detail: `${m.nombre} règlement(s)${total > 0 ? `, ${Math.round((m.encaisse / total) * 100)} % du total` : ""}` }))} />
        <table className="mt-3 w-full text-[12px]">
          <thead className="text-secondaire">
            <tr><th className="py-1 text-left font-semibold">Mode</th><th className="py-1 text-right font-semibold">Règlements</th><th className="py-1 text-right font-semibold">Effets en attente</th><th className="py-1 text-right font-semibold">Remboursé</th><th className="py-1 text-right font-semibold">Annulés</th></tr>
          </thead>
          <tbody>
            {p.modes.map((m) => (
              <tr key={m.mode} className="border-t border-bordure">
                <td className="py-1"><span className="mr-1.5 inline-block size-2 rounded-sm align-middle" style={{ background: couleur(m.mode) }} />{m.label}</td>
                <td className="py-1 text-right font-display">{m.nombre}</td>
                <td className={cn("py-1 text-right font-display", m.enAttente > 0 && "text-orange-texte")}>{m.enAttente > 0 ? formatDH(m.enAttente) : "-"}</td>
                <td className="py-1 text-right font-display">{m.rembourse > 0 ? formatDH(m.rembourse) : "-"}</td>
                <td className="py-1 text-right font-display">{m.annules || "-"}</td>
              </tr>
            ))}
            <tr className="border-t border-bordure font-semibold"><td className="py-1">Total encaissé</td><td colSpan={4} className="py-1 text-right font-display">{formatDH(total)}</td></tr>
          </tbody>
        </table>
      </Carte>
      <Carte titre={`Encaissé par mois et par mode, ${annee}`} aide="Remboursements non déduits ; détail exact par la vue tableau.">
        <Colonnes categories={MOIS} format={formatDH} surligne={surligne} detailCategorie={libelleMois} tableau
          series={p.modes.map((m) => ({ id: m.mode, label: m.label, couleur: couleur(m.mode), valeurs: p.mois.map((x) => x.parMode[m.mode] ?? 0) }))} />
      </Carte>
    </>
  );
}

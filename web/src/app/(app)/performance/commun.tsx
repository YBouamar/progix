"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { cn, formatNombre } from "@/lib/utils";
import { Select } from "@/components/ui/input";

export type Ligne = { id: string; nom: string; mois: number[]; total: number; surface?: number; enCours?: number };
export type ParAn = { annee: number; total: number; surface: number };
export type Vue = { annee: number; annees: number[]; moisCourant: number | null; dessinateurs: Ligne[] | null; ingenieurs: Ligne[] | null; dessinateursParAn: ParAn[] | null; ingenieursParAn: ParAn[] | null };

export const MOIS = ["Janv.", "Févr.", "Mars", "Avr.", "Mai", "Juin", "Juil.", "Août", "Sept.", "Oct.", "Nov.", "Déc."];
export const MOIS_LONGS = ["Janvier", "Février", "Mars", "Avril", "Mai", "Juin", "Juillet", "Août", "Septembre", "Octobre", "Novembre", "Décembre"];
export const entier = (n: number) => n.toLocaleString("fr-FR", { maximumFractionDigits: 0 });
export const parAn = (l: ParAn[]) => [...l].sort((a, b) => a.annee - b.annee);
export const parMois = (l: Ligne[]) => MOIS.map((_, i) => l.reduce((s, x) => s + x.mois[i], 0));

/** Charge une des deux vues de performance pour l'année choisie. */
export function usePerformance(vue: "dessinateurs" | "ingenieurs") {
  const [annee, setAnnee] = useState<number | null>(null);
  const [v, setV] = useState<Vue | null>(null);
  const [erreur, setErreur] = useState("");
  useEffect(() => {
    api<Vue>(`/statistiques/performance/${vue}${annee ? `?annee=${annee}` : ""}`).then(setV)
      .catch((e) => setErreur(e instanceof ApiError && e.status === 403 ? e.message || "Votre fonction ne donne pas accès à ces statistiques." : "Chargement impossible."));
  }, [vue, annee]);
  return { v, erreur, setAnnee };
}

export function ChoixAnnee({ v, onChange }: { v: Vue; onChange: (a: number) => void }) {
  return (
    <div className="flex flex-wrap items-center gap-3">
      <Select className="w-40" aria-label="Année" value={v.annee} onChange={(e) => onChange(Number(e.target.value))}>
        {v.annees.map((a) => <option key={a} value={a}>Année {a}</option>)}
      </Select>
      <span className="text-[13px] text-secondaire">Les projets supprimés ne sont pas comptés.</span>
    </div>
  );
}

export function Carte({ titre, aide, children, large }: { titre: string; aide?: string; children: React.ReactNode; large?: boolean }) {
  return (
    <section className={cn("rounded-xl border border-bordure bg-white px-5 py-4", large && "lg:col-span-2")}>
      <h3 className="text-[14px] font-bold">{titre}</h3>
      {aide && <p className="mb-3 mt-0.5 text-[12px] text-secondaire">{aide}</p>}
      {children}
    </section>
  );
}

export const sommeSurface = (lignes: Ligne[]) => `${formatNombre(lignes.reduce((s, l) => s + (l.surface ?? 0), 0))} m²`;

export type Extra = { label: string; rendre: (l: Ligne) => string; somme?: (lignes: Ligne[]) => string };

export function Tableau({ titre, aide, lignes, moisCourant, colonneTotal, extra, sommeAnnuelle }: {
  titre: string; aide: string; lignes: Ligne[]; moisCourant: number | null; colonneTotal: string;
  extra: Extra[]; sommeAnnuelle?: boolean;
}) {
  const max = Math.max(1, ...lignes.flatMap((l) => l.mois));
  return (
    <div className="mt-5 rounded-xl border border-bordure bg-white">
      <div className="px-6 py-4">
        <h3 className="text-[15px] font-bold">{titre}</h3>
        <p className="mt-0.5 text-[13px] text-secondaire">{aide}</p>
      </div>
      <div className="overflow-x-auto border-t border-bordure">
        <table className="w-full text-left text-[13px]">
          <thead className="bg-fond text-[11px] text-secondaire">
            <tr>
              <th className="px-6 py-2.5 font-semibold">Nom</th>
              {MOIS.map((m, i) => <th key={m} className={cn("px-2 py-2.5 text-right font-semibold", moisCourant === i + 1 && "text-marine")}>{m}</th>)}
              <th className="border-l border-bordure px-3 py-2.5 text-right font-semibold">{colonneTotal}</th>
              {extra.map((ex) => <th key={ex.label} className="px-6 py-2.5 text-right font-semibold">{ex.label}</th>)}
            </tr>
          </thead>
          <tbody>
            {lignes.length === 0 && <tr><td colSpan={14 + extra.length} className="px-6 py-6 text-center text-secondaire">Aucune personne à afficher.</td></tr>}
            {lignes.map((l) => (
              <tr key={l.id} className="border-t border-bordure">
                <td className="whitespace-nowrap px-6 py-2.5 font-semibold">{l.nom}</td>
                {l.mois.map((n, i) => (
                  <td key={i} className="px-2 py-2.5 text-right">
                    <span className={cn("inline-block min-w-7 rounded-md px-1.5 py-0.5 text-center font-display", n === 0 ? "text-attenue" : "font-bold text-marine")}
                      style={n > 0 ? { backgroundColor: `rgba(27, 42, 107, ${0.06 + 0.16 * (n / max)})` } : undefined}>{n}</span>
                  </td>
                ))}
                <td className="border-l border-bordure px-3 py-2.5 text-right font-display font-bold">{l.total}</td>
                {extra.map((ex) => <td key={ex.label} className="whitespace-nowrap px-6 py-2.5 text-right font-display">{ex.rendre(l)}</td>)}
              </tr>
            ))}
          </tbody>
          {sommeAnnuelle && lignes.length > 0 && (
            <tfoot className="bg-fond font-semibold">
              <tr className="border-t border-bordure">
                <td className="px-6 py-2.5">Total</td>
                {MOIS.map((_, i) => <td key={i} className="px-2 py-2.5 text-right font-display">{lignes.reduce((s, l) => s + l.mois[i], 0)}</td>)}
                <td className="border-l border-bordure px-3 py-2.5 text-right font-display font-bold">{lignes.reduce((s, l) => s + l.total, 0)}</td>
                {extra.map((ex) => <td key={ex.label} className="whitespace-nowrap px-6 py-2.5 text-right font-display">{ex.somme?.(lignes) ?? ""}</td>)}
              </tr>
            </tfoot>
          )}
        </table>
      </div>
    </div>
  );
}

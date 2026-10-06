"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { cn, formatNombre } from "@/lib/utils";
import { Select } from "@/components/ui/input";

type Ligne = { id: string; nom: string; mois: number[]; total: number; surface?: number; enCours?: number };
type Vue = { annee: number; annees: number[]; moisCourant: number | null; dessinateurs: Ligne[] | null; ingenieurs: Ligne[] | null };

const MOIS = ["Janv.", "Févr.", "Mars", "Avr.", "Mai", "Juin", "Juil.", "Août", "Sept.", "Oct.", "Nov.", "Déc."];

/** Statistiques de performance : dalles finies par dessinateur, projets suivis par ingénieur, par mois et sur l'année. */
export default function Performance() {
  const [annee, setAnnee] = useState<number | null>(null);
  const [v, setV] = useState<Vue | null>(null);
  const [erreur, setErreur] = useState("");

  useEffect(() => {
    api<Vue>(`/statistiques/performance${annee ? `?annee=${annee}` : ""}`).then(setV)
      .catch((e) => setErreur(e instanceof ApiError && e.status === 403 ? "Votre rôle ne donne pas accès à ces statistiques." : "Chargement impossible."));
  }, [annee]);

  if (erreur) return <p className="text-rouge-texte">{erreur}</p>;
  if (!v) return <p className="text-secondaire">Chargement...</p>;

  return (
    <div>
      <div className="flex flex-wrap items-center gap-3">
        <Select className="w-40" aria-label="Année" value={v.annee} onChange={(e) => setAnnee(Number(e.target.value))}>
          {v.annees.map((a) => <option key={a} value={a}>Année {a}</option>)}
        </Select>
        <span className="text-[13px] text-secondaire">Les projets supprimés ne sont pas comptés.</span>
      </div>

      {v.dessinateurs && (
        <Tableau titre="Dessinateurs : dalles finies" aide="Une dalle est finie quand l'étape « Plans d'exécution » est validée. Elle compte pour le dessinateur affecté à la dalle, au mois de cette validation."
          lignes={v.dessinateurs} moisCourant={v.moisCourant} colonneTotal={`Total ${v.annee}`}
          extra={["Surface étudiée", (l) => `${formatNombre(l.surface ?? 0)} m²`]} sommeAnnuelle />
      )}
      {v.ingenieurs && (
        <Tableau titre="Ingénieurs : projets suivis" aide="Un projet compte pour un ingénieur chaque mois où il y est affecté. Le total de l'année compte chaque projet une seule fois."
          lignes={v.ingenieurs} moisCourant={v.moisCourant} colonneTotal={`Projets ${v.annee}`}
          extra={["En cours", (l) => String(l.enCours ?? 0)]} />
      )}
    </div>
  );
}

function Tableau({ titre, aide, lignes, moisCourant, colonneTotal, extra, sommeAnnuelle }: {
  titre: string; aide: string; lignes: Ligne[]; moisCourant: number | null; colonneTotal: string;
  extra: [string, (l: Ligne) => string]; sommeAnnuelle?: boolean;
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
          <thead className="bg-fond text-[11px] uppercase tracking-wide text-secondaire">
            <tr>
              <th className="px-6 py-2.5 font-semibold">Nom</th>
              {MOIS.map((m, i) => <th key={m} className={cn("px-2 py-2.5 text-right font-semibold", moisCourant === i + 1 && "text-marine")}>{m}</th>)}
              <th className="border-l border-bordure px-3 py-2.5 text-right font-semibold">{colonneTotal}</th>
              <th className="px-6 py-2.5 text-right font-semibold">{extra[0]}</th>
            </tr>
          </thead>
          <tbody>
            {lignes.length === 0 && <tr><td colSpan={15} className="px-6 py-6 text-center text-secondaire">Aucune personne à afficher.</td></tr>}
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
                <td className="whitespace-nowrap px-6 py-2.5 text-right font-display">{extra[1](l)}</td>
              </tr>
            ))}
          </tbody>
          {sommeAnnuelle && lignes.length > 0 && (
            <tfoot className="bg-fond font-semibold">
              <tr className="border-t border-bordure">
                <td className="px-6 py-2.5">Total</td>
                {MOIS.map((_, i) => <td key={i} className="px-2 py-2.5 text-right font-display">{lignes.reduce((s, l) => s + l.mois[i], 0)}</td>)}
                <td className="border-l border-bordure px-3 py-2.5 text-right font-display font-bold">{lignes.reduce((s, l) => s + l.total, 0)}</td>
                <td className="whitespace-nowrap px-6 py-2.5 text-right font-display">{formatNombre(lignes.reduce((s, l) => s + (l.surface ?? 0), 0))} m²</td>
              </tr>
            </tfoot>
          )}
        </table>
      </div>
    </div>
  );
}

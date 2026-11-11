"use client";

import { formatNombre } from "@/lib/utils";
import { BarresHorizontales, Colonnes, COULEUR_UNIQUE } from "@/components/graphes";
import { AttenteFiche } from "@/components/ui/attente";
import { Carte, ChoixAnnee, entier, MOIS, MOIS_LONGS, parAn, parMois, sommeSurface, Tableau, usePerformance } from "../commun";

/** Performance des ingénieurs : projets et surface suivis, par personne, par mois et par an. */
export default function PerformanceIngenieurs() {
  const { v, erreur, setAnnee } = usePerformance("ingenieurs");
  if (erreur) return <p role="alert" className="text-rouge-texte">{erreur}</p>;
  if (!v || !v.ingenieurs) return <AttenteFiche />;
  const ans = parAn(v.ingenieursParAn ?? []);

  return (
    <div>
      <ChoixAnnee v={v} onChange={setAnnee} />
      <div className="mt-5 grid gap-4 lg:grid-cols-2">
        <Carte titre={`Surface suivie par ingénieur, ${v.annee}`} aide="Surface des dalles des projets suivis dans l'année ; le nombre de projets est dans l'infobulle.">
          <BarresHorizontales unite="Surface suivie" format={(n) => `${entier(n)} m²`} vide="Aucun ingénieur."
            donnees={[...v.ingenieurs].sort((a, b) => (b.surface ?? 0) - (a.surface ?? 0)).map((l) => ({ id: l.id, label: l.nom, valeur: l.surface ?? 0, detail: `${l.total} projet(s), ${l.enCours ?? 0} en cours` }))} />
        </Carte>
        <Carte titre={`Projets suivis par mois, ${v.annee}`} aide="Cumul des ingénieurs : un projet suivi par deux ingénieurs compte deux fois.">
          <Colonnes categories={MOIS} series={[{ id: "projets", label: "Projets suivis", couleur: COULEUR_UNIQUE, valeurs: parMois(v.ingenieurs) }]} format={entier}
            surligne={v.moisCourant ? v.moisCourant - 1 : null} detailCategorie={(i) => `${MOIS_LONGS[i]} ${v.annee}`} />
        </Carte>
        {ans.length > 0 && (
          <Carte titre="Projets suivis par an" aide="Chaque projet compté une fois par an ; surface suivie dans l'infobulle.">
            <Colonnes categories={ans.map((a) => String(a.annee))} format={entier} surligne={ans.findIndex((a) => a.annee === v.annee)}
              series={[{ id: "an", label: "Projets suivis", couleur: COULEUR_UNIQUE, valeurs: ans.map((a) => a.total) }]}
              detailCategorie={(i) => `${ans[i].annee} · ${formatNombre(ans[i].surface)} m²`} />
          </Carte>
        )}
      </div>
      <Tableau titre="Ingénieurs : projets suivis" aide="Un projet compte pour un ingénieur chaque mois où il y est affecté. Le total de l'année compte chaque projet une seule fois. La surface suivie est celle des dalles des mêmes projets."
        lignes={v.ingenieurs} moisCourant={v.moisCourant} colonneTotal={`Projets ${v.annee}`}
        extra={[
          { label: "En cours", rendre: (l) => String(l.enCours ?? 0) },
          { label: "Surface suivie", rendre: (l) => `${formatNombre(l.surface ?? 0)} m²`, somme: sommeSurface },
        ]} sommeAnnuelle />
    </div>
  );
}

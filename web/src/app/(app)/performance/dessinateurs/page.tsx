"use client";

import { formatNombre } from "@/lib/utils";
import { BarresHorizontales, Colonnes, COULEUR_UNIQUE } from "@/components/graphes";
import { AttenteFiche } from "@/components/ui/attente";
import { EvaluationDessinateurs } from "./evaluation";
import { Carte, ChoixAnnee, entier, MOIS, MOIS_LONGS, parAn, parMois, sommeSurface, Tableau, usePerformance } from "../commun";

/** Performance des dessinateurs : dalles finies et surface étudiée, par personne, par mois et par an. */
export default function PerformanceDessinateurs() {
  const { v, erreur, setAnnee } = usePerformance("dessinateurs");
  if (erreur) return <p role="alert" className="text-rouge-texte">{erreur}</p>;
  if (!v || !v.dessinateurs) return <AttenteFiche />;
  const ans = parAn(v.dessinateursParAn ?? []);

  return (
    <div>
      <ChoixAnnee v={v} onChange={setAnnee} />
      <div className="mt-5 grid gap-4 lg:grid-cols-2">
        <Carte titre={`Dalles finies par dessinateur, ${v.annee}`} aide="Plans d'exécution validés dans l'année ; la surface étudiée est dans l'infobulle.">
          <BarresHorizontales unite="Dalles finies" format={entier} vide="Aucun dessinateur."
            donnees={v.dessinateurs.map((l) => ({ id: l.id, label: l.nom, valeur: l.total, detail: `${formatNombre(l.surface ?? 0)} m² étudiés` }))} />
        </Carte>
        <Carte titre={`Dalles finies par mois, ${v.annee}`} aide="Tous dessinateurs confondus.">
          <Colonnes categories={MOIS} series={[{ id: "dalles", label: "Dalles finies", couleur: COULEUR_UNIQUE, valeurs: parMois(v.dessinateurs) }]} format={entier}
            surligne={v.moisCourant ? v.moisCourant - 1 : null} detailCategorie={(i) => `${MOIS_LONGS[i]} ${v.annee}`} />
        </Carte>
        {ans.length > 0 && (
          <Carte titre="Dalles finies par an" aide="Année sélectionnée mise en évidence ; surface étudiée dans l'infobulle.">
            <Colonnes categories={ans.map((a) => String(a.annee))} format={entier} surligne={ans.findIndex((a) => a.annee === v.annee)}
              series={[{ id: "an", label: "Dalles finies", couleur: COULEUR_UNIQUE, valeurs: ans.map((a) => a.total) }]}
              detailCategorie={(i) => `${ans[i].annee} · ${formatNombre(ans[i].surface)} m²`} />
          </Carte>
        )}
      </div>
      <Tableau titre="Dessinateurs : dalles finies" aide="Une dalle est finie quand l'étape « Plans d'exécution » est validée. Elle compte pour le dessinateur affecté à la dalle, au mois de cette validation."
        lignes={v.dessinateurs} moisCourant={v.moisCourant} colonneTotal={`Total ${v.annee}`}
        extra={[{ label: "Surface étudiée", rendre: (l) => `${formatNombre(l.surface ?? 0)} m²`, somme: sommeSurface }]} sommeAnnuelle />
      <EvaluationDessinateurs annee={v.annee} />
    </div>
  );
}

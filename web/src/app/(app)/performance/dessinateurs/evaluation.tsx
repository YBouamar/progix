"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { cn, filtrerDecimal, formatDate, formatDateHeure, lireNombre } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/input";
import { Sheet } from "@/components/ui/sheet";

type Parametres = {
  joursParMois: number; joursReference: number; seuilExcellent: number; seuilBon: number; seuilMoyen: number; moisMinimum: number;
  poidsSurface: number; poidsDossiers: number; objectifSurface: number; objectifDossiers: number; modifiePar: string | null; modifieLe: string | null;
};
type Ligne = {
  id: string; nom: string; partie: boolean; arrivee: string | null; depart: string | null; mois: number | null; jours: number | null;
  nbModif: number; surfaceModif: number; nbExe: number; surfaceExe: number;
  modifParJour: number | null; exeParJour: number | null; surfaceModifParJour: number | null; surfaceExeParJour: number | null;
  dossiersParJour: number | null; surfaceParJour: number | null; surfaceParDossier: number | null; score: number | null; rang: number | null; niveau: string | null;
};
type Evaluation = {
  annee: number; modifiable: boolean; parametres: Parametres; medianeDossiers: number | null; medianeSurface: number | null;
  enCours: Ligne[]; parties: Ligne[]; repartition: { niveau: string; nb: number }[];
};

const nb = (n: number | null | undefined, d = 2) => (n === null || n === undefined ? "" : n.toLocaleString("fr-FR", { minimumFractionDigits: d, maximumFractionDigits: d }).replace(/[  ]/g, " "));
/** Nombre de dossiers : entier le plus souvent, décimal quand un dossier a été partagé. */
const dossiers = (n: number) => nb(n, Number.isInteger(n) ? 0 : 2);
const TON: Record<string, "vert" | "violet" | "orange" | "rouge" | "neutre"> = { Excellent: "vert", Bon: "violet", Moyen: "orange", "À renforcer": "rouge", "Trop récente": "neutre" };

/**
 * Évaluation des dessinateurs, reprise de la feuille « Évaluation » du classeur d'origine : rendement par jour travaillé,
 * score comparé à la médiane des personnes en cours, rang et niveau. Les personnes parties sont listées à part.
 */
export function EvaluationDessinateurs({ annee }: { annee: number }) {
  const [e, setE] = useState<Evaluation | null>(null);
  const [erreur, setErreur] = useState("");
  const [reglages, setReglages] = useState(false);
  useEffect(() => {
    api<Evaluation>(`/statistiques/performance/dessinateurs/evaluation?annee=${annee}`).then((r) => { setE(r); setErreur(""); })
      .catch(() => setErreur("L'évaluation n'a pas pu être chargée."));
  }, [annee]);

  if (erreur) return <p role="alert" className="mt-6 text-rouge-texte">{erreur}</p>;
  if (!e) return null;
  const p = e.parametres;
  const sansDate = [...e.enCours, ...e.parties].filter((l) => l.mois === null).length;

  return (
    <section className="mt-6 rounded-xl border border-bordure bg-white">
      <div className="flex flex-wrap items-start justify-between gap-3 px-5 pt-4">
        <div>
          <h3 className="text-[15px] font-bold">Évaluation des dessinateurs {e.annee} : rendement par jour</h3>
          <p className="mt-0.5 max-w-[90ch] text-[12px] text-secondaire">
            Dossiers EXE et MODIF terminés dans l&apos;année et leurs surfaces, rapportés aux jours travaillés (mois de présence × {nb(p.joursParMois, 0)} jours). Un dossier partagé compte à parts égales pour chacun de ses dessinateurs.
          </p>
        </div>
        {e.modifiable && <Button size="sm" variant="outline" onClick={() => setReglages(true)}>Régler l&apos;évaluation</Button>}
      </div>

      <dl className="grid gap-3 px-5 py-4 sm:grid-cols-2 lg:grid-cols-4">
        <Repere titre="Objectif de surface" valeur={`${nb(p.objectifSurface, 0)} m² par jour`} detail={`${nb(p.objectifSurface * p.joursParMois, 0)} m² par mois`} />
        <Repere titre="Objectif de dossiers" valeur={`${nb(p.objectifDossiers, p.objectifDossiers % 1 ? 2 : 0)} par jour`} detail={`${nb(p.objectifDossiers * p.joursParMois, 0)} par mois`} />
        <Repere titre="Médiane de surface" valeur={e.medianeSurface === null ? "Pas encore de donnée" : `${nb(e.medianeSurface)} m² par jour`} detail="Personnes en cours" />
        <Repere titre="Médiane de dossiers" valeur={e.medianeDossiers === null ? "Pas encore de donnée" : `${nb(e.medianeDossiers)} par jour`} detail="Personnes en cours" />
      </dl>

      {sansDate > 0 && (
        <p role="status" className="mx-5 mb-4 rounded-lg bg-orange-fond px-4 py-2.5 text-[13px] text-orange-texte">
          {sansDate === 1 ? "Il manque une date d'arrivée ou de départ à une personne : son rendement" : `Il manque une date d'arrivée ou de départ à ${sansDate} personnes : leur rendement`} ne peut pas être calculé. Les dates se renseignent sur la fiche de la personne, dans le Paramétrage, onglet Accès.
        </p>
      )}

      <Table titre="Dessinateurs en cours, base de l'évaluation" lignes={e.enCours} objectif={p} vide="Aucun dessinateur en cours cette année." />
      {e.parties.length > 0 && <Table titre="Dessinateurs partis, pour information" lignes={e.parties} objectif={p} partis
        aide="Comparés à la médiane des personnes en cours, et classés entre eux." vide="" />}

      <div className="grid gap-5 border-t border-bordure px-5 py-4 lg:grid-cols-[260px_1fr]">
        <div>
          <h4 className="text-[13px] font-bold">Répartition par niveau, personnes en cours</h4>
          <ul className="mt-2 space-y-1.5 text-[13px]">
            {e.repartition.map((r) => (
              <li key={r.niveau} className="flex items-center justify-between gap-3"><Badge ton={TON[r.niveau]}>{r.niveau}</Badge><span className="font-display font-bold">{r.nb}</span></li>
            ))}
          </ul>
        </div>
        <div className="text-[12px] text-secondaire">
          <h4 className="text-[13px] font-bold text-texte">Comment lire le score et le rang</h4>
          <ul className="mt-2 max-w-[90ch] space-y-1">
            <li>Score = {nb(p.poidsSurface)} × (m² par jour ÷ médiane) + {nb(p.poidsDossiers)} × (dossiers par jour ÷ médiane){p.joursReference !== p.joursParMois ? `, multiplié par ${nb(p.joursReference, 0)} ÷ ${nb(p.joursParMois, 0)}` : ""}. 1,00 est le niveau de la personne médiane.</li>
            <li>Niveau : Excellent à partir de {nb(p.seuilExcellent)}, Bon à partir de {nb(p.seuilBon)}, Moyen à partir de {nb(p.seuilMoyen)}, À renforcer en dessous.</li>
            <li>Rang : 1 pour le meilleur score. En dessous de {nb(p.moisMinimum, p.moisMinimum % 1 ? 1 : 0)} mois de présence, la personne est « Trop récente » : elle a un score mais ni rang ni niveau.</li>
            <li>Mois travaillés : de l&apos;arrivée (ou du 1er janvier) au départ (ou à aujourd&apos;hui), d&apos;après les dates de la fiche de la personne.</li>
            {p.modifiePar && <li>Réglages modifiés par {p.modifiePar} le {formatDateHeure(p.modifieLe)}.</li>}
          </ul>
        </div>
      </div>

      {reglages && <Reglages p={p} annee={e.annee} onClose={() => setReglages(false)} onFait={(r) => { setE(r); setReglages(false); }} />}
    </section>
  );
}

function Repere({ titre, valeur, detail }: { titre: string; valeur: string; detail: string }) {
  return (
    <div className="rounded-lg bg-fond px-4 py-3">
      <dt className="text-[12px] text-secondaire">{titre}</dt>
      <dd className="font-display text-[17px] font-bold">{valeur}</dd>
      <dd className="text-[12px] text-secondaire">{detail}</dd>
    </div>
  );
}

function Table({ titre, aide, lignes, objectif, partis, vide }: { titre: string; aide?: string; lignes: Ligne[]; objectif: Parametres; partis?: boolean; vide: string }) {
  const th = "px-3 py-2 text-right font-semibold";
  const td = "whitespace-nowrap px-3 py-2.5 text-right font-display";
  return (
    <div className="border-t border-bordure">
      <div className="px-5 pt-3">
        <h4 className="text-[13px] font-bold">{titre}</h4>
        {aide && <p className="text-[12px] text-secondaire">{aide}</p>}
      </div>
      <div className="mt-2 overflow-x-auto">
        <table className="w-full min-w-[1180px] text-left text-[13px]">
          <thead className="bg-fond text-[11px] text-secondaire">
            <tr>
              <th className="px-3 py-2 text-right font-semibold">Rang</th>
              <th className="sticky left-0 z-10 bg-fond px-3 py-2 font-semibold">Dessinateur</th>
              {partis && <th className="px-3 py-2 font-semibold">Départ</th>}
              <th className={th}>Mois travaillés</th>
              <th className={th}>Jours</th>
              <th className={cn(th, "border-l border-bordure")}>MODIF</th>
              <th className={th}>Surface MODIF</th>
              <th className={th}>EXE</th>
              <th className={th}>Surface EXE</th>
              <th className={cn(th, "border-l border-bordure")}>Dossiers par jour</th>
              <th className={th}>m² par jour</th>
              <th className={th}>m² par dossier</th>
              <th className={cn(th, "border-l border-bordure")}>Score</th>
              <th className="px-3 py-2 font-semibold">Niveau</th>
            </tr>
          </thead>
          <tbody>
            {lignes.map((l) => (
              <tr key={l.id} className="border-t border-bordure">
                <td className={cn(td, "font-bold")}>{l.rang ?? ""}</td>
                <td className="sticky left-0 z-10 whitespace-nowrap bg-white px-3 py-2.5 font-semibold">{l.nom}</td>
                {partis && <td className="whitespace-nowrap px-3 py-2.5 text-secondaire">{l.depart ? formatDate(l.depart) : "Compte désactivé"}</td>}
                <td className={td}>{l.mois === null ? <span className="font-sans text-orange-texte">{l.partie && !l.depart && l.arrivee ? "Date de départ à renseigner" : "Date d'arrivée à renseigner"}</span> : nb(l.mois, 1)}</td>
                <td className={td}>{nb(l.jours, 0)}</td>
                <td className={cn(td, "border-l border-bordure")}>{dossiers(l.nbModif)}</td>
                <td className={td}>{nb(l.surfaceModif)}</td>
                <td className={td}>{dossiers(l.nbExe)}</td>
                <td className={td}>{nb(l.surfaceExe)}</td>
                <td className={cn(td, "border-l border-bordure", l.dossiersParJour !== null && l.dossiersParJour >= objectif.objectifDossiers && "text-vert-texte")}
                  title={l.dossiersParJour === null ? undefined : `MODIF ${nb(l.modifParJour)} par jour, EXE ${nb(l.exeParJour)} par jour`}>{nb(l.dossiersParJour)}</td>
                <td className={cn(td, l.surfaceParJour !== null && l.surfaceParJour >= objectif.objectifSurface && "text-vert-texte")}
                  title={l.surfaceParJour === null ? undefined : `MODIF ${nb(l.surfaceModifParJour)} m² par jour, EXE ${nb(l.surfaceExeParJour)} m² par jour`}>{nb(l.surfaceParJour)}</td>
                <td className={td}>{nb(l.surfaceParDossier)}</td>
                <td className={cn(td, "border-l border-bordure font-bold")}>{nb(l.score)}</td>
                <td className="whitespace-nowrap px-3 py-2.5">{l.niveau && <Badge ton={TON[l.niveau]}>{l.niveau}</Badge>}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {lignes.length === 0 && vide && <p className="px-5 py-8 text-center text-secondaire">{vide}</p>}
      </div>
      <p className="px-5 py-2 text-[12px] text-attenue">En vert : rendement à l&apos;objectif ou au-dessus. Le détail MODIF et EXE par jour est dans l&apos;infobulle.</p>
    </div>
  );
}

const CHAMPS: [keyof Parametres, string][] = [
  ["joursParMois", "Jours travaillés par mois"], ["joursReference", "Jours par mois de référence"],
  ["objectifSurface", "Objectif de surface (m² par jour)"], ["objectifDossiers", "Objectif de dossiers (par jour)"],
  ["seuilExcellent", "Seuil « Excellent » (score à partir de)"], ["seuilBon", "Seuil « Bon » (score à partir de)"], ["seuilMoyen", "Seuil « Moyen » (score à partir de)"],
  ["moisMinimum", "Mois minimum pour être évalué"], ["poidsSurface", "Poids de la surface dans le score"], ["poidsDossiers", "Poids des dossiers dans le score"],
];

function Reglages({ p, annee, onClose, onFait }: { p: Parametres; annee: number; onClose: () => void; onFait: (e: Evaluation) => void }) {
  const [f, setF] = useState<Record<string, string>>(Object.fromEntries(CHAMPS.map(([k]) => [k, String(p[k]).replace(".", ",")])));
  const [erreur, setErreur] = useState("");
  const [envoi, setEnvoi] = useState(false);
  const enregistrer = async () => {
    setEnvoi(true); setErreur("");
    try {
      onFait(await api<Evaluation>(`/statistiques/performance/dessinateurs/evaluation/parametres?annee=${annee}`,
        { method: "PUT", body: JSON.stringify(Object.fromEntries(CHAMPS.map(([k]) => [k, lireNombre(f[k])]))) }));
    } catch (e) { setErreur(e instanceof ApiError ? e.message : "Enregistrement impossible."); setEnvoi(false); }
  };
  return (
    <Sheet open onOpenChange={(o) => !o && onClose()} title="Régler l'évaluation" description="Ces réglages s'appliquent à toutes les années."
      footer={<><Button variant="outline" onClick={onClose}>Annuler</Button><Button disabled={envoi} onClick={enregistrer}>{envoi ? "Enregistrement..." : "Enregistrer"}</Button></>}>
      {erreur && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}
      <div className="grid gap-3 sm:grid-cols-2">
        {CHAMPS.map(([k, label]) => (
          <Field key={k} label={label}><Input inputMode="decimal" value={f[k]} onChange={(e) => setF({ ...f, [k]: filtrerDecimal(e.target.value) })} /></Field>
        ))}
      </div>
      <p className="mt-4 text-[12px] text-secondaire">Les deux poids font 1 à eux deux. Le score de la personne médiane vaut 1,00 quand les jours par mois égalent les jours de référence.</p>
    </Sheet>
  );
}

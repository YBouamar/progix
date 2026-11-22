"use client";

import { Fragment, use, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { ArrowLeft, CircleAlert } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn, formatDate, formatDateHeure, formatSurface, formatNombre } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/modal";
import { AttenteFiche } from "@/components/ui/attente";

type Tendon = { numero: string; torons: number; sens: string; longueur: number; ancrages: number; modifiee?: boolean; ajoutee?: boolean; avant?: string | null };
type Modification = { le: string; par: string; etat: string; changements: string[] };
const SUIVANT: Record<string, [string, string]> = { a_preparer: ["preparee", "Déclarer préparée"], preparee: ["envoyee", "Déclarer envoyée au chantier"], envoyee: ["recue", "Déclarer reçue au chantier"] };
type Accessoire = { libelle: string; unite: string | null; quantite: number };
type Detail = {
  dalleId: string; projetId: string; reference: string; projet: string; client: string;
  entite: string; entiteLabel: string; adresse: string | null; ville: string | null;
  bloc: string | null; immeuble: string | null; niveau: string; surface: number; detailDalle: string | null;
  demandee: string; demandeePar: string | null;
  validationTechniquePar: string | null; validationTechniqueLe: string | null;
  validationAdministrativePar: string | null; validationAdministrativeLe: string | null;
  fabricationLe: string | null; livreeLe: string | null; recueLe: string | null; suiviPar: string | null;
  etat: string; etatLabel: string;
  tendons: Tendon[]; accessoires: Accessoire[];
  commandeId: string; numero: number; surfaceCommande: number | null; modifications: Modification[];
  annuleeLe: string | null; annuleePar: string | null; motifAnnulation: string | null;
  peutPreparer: boolean; peutRecevoir: boolean; peutAnnuler: boolean;
};

/** Fiche isolée d'une commande usine, ouverte depuis la liste de la logistique, sans passer par la fiche projet. */
export default function FicheCommande({ params }: { params: Promise<{ commandeId: string }> }) {
  const { commandeId } = use(params);
  const [d, setD] = useState<Detail | null>(null);
  const [erreur, setErreur] = useState("");

  const [annulation, setAnnulation] = useState<string | null>(null);
  const [action, setAction] = useState("");
  const charger = () => api<Detail>(`/logistique/commandes/${commandeId}`).then(setD)
    .catch((e) => setErreur(e instanceof ApiError && e.status === 404 ? "Commande introuvable." : "Chargement impossible."));
  useEffect(() => { charger(); // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [commandeId]);

  async function changer(etat: string, motif?: string) {
    setAction("");
    try { await api(`/logistique/commandes/${commandeId}/etat`, { method: "POST", body: JSON.stringify({ etat, motif }) }); setAnnulation(null); await charger(); }
    catch (e) { setAction(e instanceof ApiError ? (e.status === 403 ? "Votre rôle ne permet pas cette action." : e.message) : "Action impossible."); }
  }

  const calcul = useMemo(() => {
    if (!d) return null;
    const parSysteme: Record<number, { tendons: number; gaine: number; ancrages: number }> = {};
    let gaine = 0, torons = 0, x = 0, y = 0;
    for (const t of d.tendons) {
      gaine += t.longueur; torons += t.longueur * t.torons;
      if (t.sens === "Y") y++; else x++;
      parSysteme[t.torons] ??= { tendons: 0, gaine: 0, ancrages: 0 };
      parSysteme[t.torons].tendons++; parSysteme[t.torons].gaine += t.longueur; parSysteme[t.torons].ancrages += t.ancrages;
    }
    return { parSysteme, gaine, torons, x, y };
  }, [d]);

  if (!d) {
    return (
      <div>
        <Retour />
        {erreur ? <div role="alert" className="rounded-xl border border-bordure bg-white p-12 text-center text-secondaire">{erreur}</div> : <AttenteFiche />}
      </div>
    );
  }

  const validations: [string, string][] = [
    ["Validation technique", d.validationTechniqueLe ? `${formatDateHeure(d.validationTechniqueLe)}, par ${d.validationTechniquePar}` : "En attente"],
    ["Validation administrative", d.validationAdministrativeLe ? `${formatDateHeure(d.validationAdministrativeLe)}, par ${d.validationAdministrativePar}` : "En attente"],
  ];
  const suivi: [string, string | null][] = [
    ["Préparée le", d.fabricationLe ? formatDateHeure(d.fabricationLe) : null],
    ["Envoyée au chantier le", d.livreeLe ? formatDateHeure(d.livreeLe) : null],
    ["Reçue au chantier le", d.recueLe ? formatDateHeure(d.recueLe) : null],
    ["Annulée le", d.annuleeLe ? `${formatDateHeure(d.annuleeLe)}, par ${d.annuleePar}. Motif : ${d.motifAnnulation}` : null],
  ];

  return (
    <div>
      <Retour />
      <div className="rounded-xl border border-bordure bg-white p-6">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-display text-[15px] font-bold text-marine">{d.reference}</span>
              <Badge ton={d.etat === "recue" ? "vert" : d.etat === "attente" || d.etat === "annulee" ? "neutre" : "orange"}>{d.etatLabel}</Badge>
              <Badge>{d.entite} · {d.entiteLabel}</Badge>
            </div>
            <h2 className="mt-2 text-2xl font-bold">Commande usine n° {d.numero}{d.numero === 1 ? " (initiale)" : " (complément)"}</h2>
            <div className="mt-1 text-secondaire">
              <Link href={`/projets/${d.projetId}`} className="font-semibold text-marine hover:underline">{d.projet}</Link>
              {" · "}{d.client}
            </div>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            {SUIVANT[d.etat] && (SUIVANT[d.etat][0] === "recue" ? d.peutRecevoir : d.peutPreparer) && <Button onClick={() => changer(SUIVANT[d.etat][0])}>{SUIVANT[d.etat][1]}</Button>}
            {d.peutAnnuler && d.etat !== "annulee" && d.etat !== "recue" && <Button variant="outline" onClick={() => setAnnulation("")}>Annuler la commande</Button>}
          </div>
        </div>
        {action && <div role="alert" className="mt-3 text-[13px] text-rouge-texte">{action}</div>}
      </div>

      <div className="mt-5 grid gap-5 lg:grid-cols-2">
        <div className="rounded-xl border border-bordure bg-white p-6">
          <h3 className="mb-4 text-[15px] font-bold">Dalle</h3>
          <dl className="grid gap-x-6 gap-y-4 sm:grid-cols-2">
            <Info label="Niveau" value={d.niveau} />
            <Info label="Bloc / immeuble" value={[d.bloc, d.immeuble].filter(Boolean).join(" · ") || "Non renseigné"} />
            <div>
              <dt className="text-[12px] font-semibold text-attenue">Surface</dt>
              <dd className="mt-0.5">Dalle : {formatSurface(d.surface)}{d.surfaceCommande != null && d.surfaceCommande !== d.surface && <Alerte />}
                {d.surfaceCommande != null && <><br />Commande : {formatSurface(d.surfaceCommande)}{d.surfaceCommande !== d.surface && <Alerte />}</>}</dd>
            </div>
            <Info label="Adresse du projet" value={[d.adresse, d.ville].filter(Boolean).join(", ") || "Non renseignée"} />
            {d.detailDalle && <Info label="Détail" value={d.detailDalle} className="sm:col-span-2" />}
          </dl>
        </div>

        <div className="rounded-xl border border-bordure bg-white p-6">
          <h3 className="mb-4 text-[15px] font-bold">Validations et suivi</h3>
          <dl className="grid gap-x-6 gap-y-4 sm:grid-cols-2">
            <Info label="Demandée" value={`${formatDate(d.demandee)}, par ${d.demandeePar}`} className="sm:col-span-2" />
            {validations.map(([k, v]) => <Info key={k} label={k} value={v} />)}
            {suivi.filter(([, v]) => v).map(([k, v]) => <Info key={k} label={k} value={v!} />)}
            {d.suiviPar && <Info label="Suivi logistique par" value={d.suiviPar} />}
          </dl>
        </div>
      </div>

      <div className="mt-5 rounded-xl border border-bordure bg-white p-6">
        <div className="mb-4 flex flex-wrap items-baseline justify-between gap-2">
          <h3 className="text-[15px] font-bold">Tendons</h3>
          {calcul && d.tendons.length > 0 && (
            <div className="text-[13px] text-secondaire">
              {d.tendons.length} tendons · {calcul.x} en X, {calcul.y} en Y · {formatNombre(calcul.gaine)} m de gaine · {formatNombre(calcul.torons)} m de torons
            </div>
          )}
        </div>
        <div className="overflow-x-auto rounded-md border border-bordure">
          <table className="w-full text-left text-[13px]">
            <thead className="bg-fond text-[11px] text-secondaire">
              <tr>
                <th className="px-3 py-2 font-semibold">N° tendon</th>
                <th className="px-3 py-2 font-semibold">Système</th>
                <th className="px-3 py-2 font-semibold">Orientation</th>
                <th className="px-3 py-2 text-right font-semibold">Longueur m</th>
                <th className="px-3 py-2 text-right font-semibold">Ancrages</th>
                <th className="px-3 py-2 text-right font-semibold">Torons m</th>
              </tr>
            </thead>
            <tbody>
              {d.tendons.length === 0 && <tr><td colSpan={6} className="px-3 py-6 text-center text-secondaire">Aucun tendon saisi.</td></tr>}
              {d.tendons.map((t, i) => (
                <Fragment key={i}><tr className={cn("border-t border-bordure", (t.modifiee || t.ajoutee) && "bg-orange-fond")}>
                  <td className="px-3 py-1.5 font-display font-bold text-marine">{t.numero}</td>
                  <td className="px-3 py-1.5 font-display">S{t.torons}</td>
                  <td className="px-3 py-1.5">{t.sens}</td>
                  <td className="px-3 py-1.5 text-right font-display">{formatNombre(t.longueur)}</td>
                  <td className="px-3 py-1.5 text-right font-display">{t.ancrages}</td>
                  <td className="px-3 py-1.5 text-right font-display text-secondaire">{formatNombre(t.longueur * t.torons)}</td>
                </tr>
                {(t.modifiee || t.ajoutee) && (
                  <tr className="bg-orange-fond"><td colSpan={6} className="px-3 pb-1.5 text-[12px] text-orange-texte">
                    <b>{t.ajoutee ? "Ligne ajoutée depuis l'envoi de la commande." : "Ligne modifiée depuis l'envoi de la commande."}</b>{t.avant && !t.ajoutee ? ` Avant : ${t.avant}.` : ""}
                  </td></tr>
                )}
                </Fragment>
              ))}
            </tbody>
          </table>
        </div>
        {calcul && Object.keys(calcul.parSysteme).length > 0 && (
          <p className="mt-2 text-[12px] text-attenue">
            Par système : {Object.entries(calcul.parSysteme).map(([t, v]) => `S${t} : ${v.tendons} tendons, ${v.ancrages} ancrages`).join(" · ")}
          </p>
        )}
      </div>

      {d.modifications.length > 0 && (
        <div className="mt-5 rounded-xl border border-bordure bg-white p-6">
          <h3 className="mb-3 text-[15px] font-bold">Historique des modifications</h3>
          <ul className="space-y-3 text-[13px]">
            {d.modifications.map((m, k) => (
              <li key={k}><div className="text-[12px] text-secondaire">{formatDateHeure(m.le)} · {m.par} · commande {m.etat.toLowerCase()}</div>
                <ul className="list-disc pl-5">{m.changements.map((c, j) => <li key={j}>{c}</li>)}</ul></li>
            ))}
          </ul>
        </div>
      )}

      <Modal open={annulation !== null} onOpenChange={(o) => !o && setAnnulation(null)} title="Annuler la commande" description="L'annulation est tracée. Les tendons de cette commande pourront être commandés à nouveau."
        footer={<><Button variant="outline" onClick={() => setAnnulation(null)}>Retour</Button><Button disabled={!(annulation ?? "").trim()} onClick={() => changer("annulee", annulation!.trim())}>Annuler la commande</Button></>}>
        <label className="block text-[13px] font-semibold" htmlFor="motif">Motif de l&apos;annulation</label>
        <textarea id="motif" className="mt-1 w-full rounded-md border border-bordure-forte p-2 text-[13px]" rows={3} value={annulation ?? ""} onChange={(e) => setAnnulation(e.target.value)} />
      </Modal>

      <div className="mt-5 rounded-xl border border-bordure bg-white p-6">
        <h3 className="mb-3 text-[15px] font-bold">Accessoires</h3>
        {d.accessoires.length === 0 ? (
          <div className="text-[13px] text-secondaire">Aucun accessoire dans cette commande.</div>
        ) : (
          <ul className="divide-y divide-bordure text-[13px]">
            {d.accessoires.map((a, i) => (
              <li key={i} className="flex items-center gap-2 py-2">
                <span className="w-16 shrink-0 text-right font-display font-bold">{a.quantite}</span>
                <span className="w-16 shrink-0 text-secondaire">{a.unite}</span>
                <span className="flex-1 font-semibold">{a.libelle}</span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

function Alerte() {
  return <span role="img" aria-label="Surface différente entre la dalle et la commande" title="La surface de la commande diffère de celle de la dalle : celle de la dalle est retenue." className="ml-1 inline-flex align-middle text-orange-texte"><CircleAlert size={15} /></span>;
}

function Info({ label, value, className }: { label: string; value: string; className?: string }) {
  return (
    <div className={className}>
      <dt className="text-[12px] font-semibold text-attenue">{label}</dt>
      <dd className={cn("mt-0.5")}>{value}</dd>
    </div>
  );
}

function Retour() {
  return (
    <Link href="/logistique" className="mb-4 inline-flex items-center gap-1.5 text-[13px] font-semibold text-secondaire hover:text-marine">
      <ArrowLeft size={15} />Toutes les commandes
    </Link>
  );
}

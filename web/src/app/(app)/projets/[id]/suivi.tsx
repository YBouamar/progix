"use client";

import { useEffect, useState } from "react";
import { Check, Lock } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn, formatDateHeure } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Sheet } from "@/components/ui/sheet";
import { VersionsPlans } from "./versions";
import { FicheTension } from "./tension";
import { CommandeUsine } from "./commande";
import { FicheInjection } from "./injection";
import { FicheAutoControle } from "./autocontrole";
import { FicheSignature } from "./fiche-signature";
import { RapportsEcrasement } from "./rapports-ecrasement";
import { Documents } from "@/components/documents";

type Etape = {
  numero: number; label: string; doubleValidation: boolean; validationAvant: boolean; etat: "terminee" | "validation" | "courante" | "a_venir";
  faitPar: string | null; faitLe: string | null; remarque: string | null;
  validationTechniquePar: string | null; validationTechniqueLe: string | null;
  validationAdministrativePar: string | null; validationAdministrativeLe: string | null;
  validationChantier: boolean; validationChantierPar: string | null; validationChantierLe: string | null; peutValiderChantier: boolean;
  peutRealiser: boolean; peutValiderTechnique: boolean; peutValiderAdministratif: boolean; peutDeverrouiller: boolean;
  deverrouillages: { motif: string; par: string; date: string }[];
};
type Vue = { dalle: { id: string; reference: string; niveauLabel: string; etape: string }; suspendu: string | null; laboratoire: boolean; etapes: Etape[];
  rappelPaiement: { factures: number; echues: number; reste: number | null; resteEchu: number | null } | null };

const DH = (v: number) => v.toLocaleString("fr-FR", { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + " DH";
const ZONE = "w-full rounded-lg border border-bordure-forte px-3 py-2 text-sm focus:border-marine focus:outline-none";

export function SuiviDalle({ projetId, dalleId, onClose, onChange }: {
  projetId: string; dalleId: string; onClose: () => void; onChange: (dalle: Vue["dalle"]) => void;
}) {
  const [vue, setVue] = useState<Vue | null>(null);
  const [remarque, setRemarque] = useState("");
  const [motDePasse, setMotDePasse] = useState("");
  const [deverrou, setDeverrou] = useState<{ n: number; motif: string } | null>(null);
  const [erreur, setErreur] = useState("");
  // Étape dont l'action vient d'échouer : le message s'affiche à côté de son bouton, là où l'on regarde.
  const [erreurEtape, setErreurEtape] = useState<number | null>(null);
  const [envoi, setEnvoi] = useState(false);
  const [sel, setSel] = useState<number | null>(null);
  const base = `/projets/${projetId}/dalles/${dalleId}/etapes`;

  useEffect(() => { api<Vue>(base).then(setVue).catch(() => setErreur("Chargement impossible.")); }, [base]);

  async function agir(chemin: string, corps: object) {
    setEnvoi(true); setErreur(""); setErreurEtape(null);
    try {
      const r = await api<Vue>(`${base}/${chemin}`, { method: "POST", body: JSON.stringify(corps) });
      setVue(r); setRemarque(""); setMotDePasse(""); setDeverrou(null); setSel(null); onChange(r.dalle);
    } catch (e) {
      setErreur(e instanceof ApiError ? (e.status === 403 ? "Votre rôle ne permet pas cette action." : e.errors?.motif?.[0] ?? e.message) : "Action impossible.");
      setErreurEtape(parseInt(chemin, 10) || null);
    } finally {
      setEnvoi(false);
    }
  }

  // Une étape à la fois : la fenêtre s'ouvre sur l'étape en cours, et y revient après chaque action réussie.
  const courante = (v: Vue) => v.etapes.find((x) => x.etat === "courante" || x.etat === "validation")?.numero ?? v.etapes.length;
  const e = vue?.etapes.find((x) => x.numero === (sel ?? (vue ? courante(vue) : 0)));
  const resume = (x: Etape) => x.etat === "terminee" ? `Réalisée${x.faitLe ? ` le ${formatDateHeure(x.faitLe).split(" ")[0]}` : ""}`
    : x.etat === "validation" ? "En attente de validation" : x.etat === "courante" ? "En cours" : "À venir";
  const faites = vue?.etapes.filter((x) => x.etat === "terminee").length ?? 0;
  const aDesActions = !!e && (e.peutRealiser || e.peutValiderTechnique || e.peutValiderAdministratif || e.peutValiderChantier || e.peutDeverrouiller || (erreur && erreurEtape === e.numero));

  return (
    <Sheet large plein open onOpenChange={(o) => !o && onClose()} title={vue ? `Suivi · ${vue.dalle.reference}` : "Suivi de chantier"}
      description={vue ? `${vue.dalle.niveauLabel} · ${faites} étape${faites > 1 ? "s" : ""} sur ${vue.etapes.length} · ${vue.dalle.etape}` : "Onze étapes, dans l'ordre strict."}>
      <div className="flex h-full max-md:flex-col">
        <nav aria-label="Étapes de la dalle" className="flex shrink-0 gap-0.5 border-bordure bg-fond p-2 max-md:overflow-x-auto max-md:border-b md:w-72 md:flex-col md:overflow-y-auto md:border-r">
          {vue?.etapes.map((x) => {
            const choisie = x.numero === e?.numero, active = x.etat === "courante" || x.etat === "validation";
            return (
              <button key={x.numero} type="button" aria-current={choisie ? "step" : undefined} onClick={() => { setSel(x.numero); setDeverrou(null); }}
                className={cn("flex min-h-[52px] cursor-pointer items-center gap-2.5 rounded-lg border px-2.5 py-1.5 text-left max-md:shrink-0",
                  choisie ? "border-marine bg-white" : "border-transparent hover:bg-white/70")}>
                <span className={cn("flex h-7 w-7 shrink-0 items-center justify-center rounded-full font-display text-[12px] font-bold",
                  x.etat === "terminee" ? "bg-[#1e9e58] text-white" : active ? "bg-marine text-white" : "bg-bordure text-secondaire")}>
                  {x.etat === "terminee" ? <Check size={14} /> : x.numero}
                </span>
                <span className="min-w-0">
                  <span className={cn("block truncate text-[13.5px]", choisie || active ? "font-bold" : "font-semibold", x.etat === "a_venir" && "text-secondaire")}>{x.label}</span>
                  <span className={cn("block truncate text-[11.5px]", x.etat === "validation" ? "font-semibold text-orange-texte" : "text-secondaire")}>{resume(x)}</span>
                </span>
              </button>
            );
          })}
        </nav>

        <div className="flex min-h-0 min-w-0 flex-1 flex-col">
          <div className="min-h-0 flex-1 overflow-y-auto px-6 py-4">
            {erreur && erreurEtape === null && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}
            {vue?.suspendu && <div className="mb-4 rounded-lg bg-orange-fond px-4 py-3 text-orange-texte">{vue.suspendu}</div>}
            {vue && e && (
              <div key={e.numero}>
                <div className="flex flex-wrap items-center gap-2">
                  <h3 className="text-[17px] font-bold">{e.numero} · {e.label}</h3>
                  <Badge ton={e.etat === "terminee" ? "vert" : e.etat === "a_venir" ? "neutre" : e.etat === "validation" ? "orange" : "violet"}>
                    {e.etat === "terminee" ? "Réalisée" : e.etat === "a_venir" ? "À venir" : e.etat === "validation" ? "En attente de validation" : "En cours"}
                  </Badge>
                  {e.doubleValidation && <Badge ton="violet">{e.validationChantier ? "Triple validation" : "Double validation"}</Badge>}
                  {e.etat === "validation" && e.validationAvant && <Badge ton="orange">{e.validationChantier ? "Trois validations avant la saisie" : "Deux validations avant la saisie"}</Badge>}
                </div>
                {e.etat === "a_venir" && <div className="mt-4 rounded-lg border border-dashed border-bordure-forte px-6 py-8 text-center text-secondaire">Cette étape s&apos;ouvrira quand les précédentes seront validées. Les étapes se réalisent dans l&apos;ordre.</div>}
            {e.faitLe && <div className="text-[13px] text-secondaire">Réalisée par {e.faitPar} le {formatDateHeure(e.faitLe)}</div>}
            {e.remarque && <div className="mt-1 whitespace-pre-line text-[13px]">{e.remarque}</div>}
            {e.doubleValidation && (e.faitLe || (e.validationAvant && e.etat !== "a_venir")) && (
              <ul className="mt-1.5 space-y-0.5 text-[13px]">
                <li className={e.validationTechniqueLe ? "text-vert-texte" : "text-secondaire"}>
                  Validation technique : {e.validationTechniqueLe ? `${e.validationTechniquePar}, le ${formatDateHeure(e.validationTechniqueLe)}` : "en attente du chef de projet"}
                </li>
                <li className={e.validationAdministrativeLe ? "text-vert-texte" : "text-secondaire"}>
                  Validation administrative : {e.validationAdministrativeLe ? `${e.validationAdministrativePar}, le ${formatDateHeure(e.validationAdministrativeLe)}` : "en attente du responsable administratif"}
                </li>
                {e.validationChantier && (
                  <li className={e.validationChantierLe ? "text-vert-texte" : "text-secondaire"}>
                    Validation du responsable des chantiers : {e.validationChantierLe ? `${e.validationChantierPar}, le ${formatDateHeure(e.validationChantierLe)}` : "en attente du responsable des chantiers"}
                  </li>
                )}
              </ul>
            )}
            {e.deverrouillages.map((t, i) => (
              <div key={i} className="mt-1 text-[12px] text-attenue">Déverrouillée par {t.par} le {formatDateHeure(t.date)}. Motif : {t.motif}</div>
            ))}

            {e.numero === 1 && <VersionsPlans key={vue.dalle.etape} projetId={projetId} dalleId={dalleId} onChange={(dalle) => { onChange(dalle); api<Vue>(base).then(setVue).catch(() => undefined); }} />}

            {[3, 5, 6, 7].includes(e.numero) && e.etat !== "a_venir" && (
              <Documents compact key={`photo-${e.numero}-${e.etat}`} contexte="photo_etape" cibleId={dalleId} categorie={String(e.numero)} titre="Photos" />
            )}
            {e.numero === 8 && e.etat !== "a_venir" && (
              <>
                <div className={cn("mt-2 text-[13px]", vue.laboratoire ? "text-secondaire" : "font-semibold text-rouge-texte")}>
                  {vue.laboratoire ? "Laboratoire du projet renseigné." : "Laboratoire requis : ajoutez-le dans les partenaires du projet."}
                </div>
                <RapportsEcrasement key={`rapport-${e.etat}`} projetId={projetId} dalleId={dalleId} />
              </>
            )}

            {e.numero === 4 && e.etat !== "a_venir" && (
              <CommandeUsine key={e.etat + (e.faitLe ?? "")} projetId={projetId} dalleId={dalleId} />
            )}
            {e.numero === 6 && (e.etat === "courante" || e.etat === "terminee") && (
              <FicheAutoControle key={e.etat} projetId={projetId} dalleId={dalleId} />
            )}
            {e.etat === "terminee" && e.numero === 6 && <FicheSignature dalleId={dalleId} type="auto-controle" quoi="la fiche d'auto-contrôle" />}
            {e.etat === "terminee" && e.numero === 9 && <FicheSignature dalleId={dalleId} type="tension" quoi="la fiche de mise en tension" />}
            {e.etat === "terminee" && e.numero === 10 && <FicheSignature dalleId={dalleId} type="injection" quoi="le rapport d'injection" />}
            {e.numero === 10 && e.etat !== "terminee" && vue.rappelPaiement && (
              <div className="mt-2 rounded-lg bg-orange-fond px-3 py-2 text-[13px] text-orange-texte">
                <b>Rappel de paiement avant injection.</b>{" "}
                {vue.rappelPaiement.factures === 1 ? "Une facture de ce projet n'est pas soldée" : `${vue.rappelPaiement.factures} factures de ce projet ne sont pas soldées`}
                {vue.rappelPaiement.echues > 0 && `, dont ${vue.rappelPaiement.echues} échue${vue.rappelPaiement.echues > 1 ? "s" : ""}`}.
                {vue.rappelPaiement.reste !== null && ` Reste dû : ${DH(vue.rappelPaiement.reste)}${vue.rappelPaiement.resteEchu ? `, dont ${DH(vue.rappelPaiement.resteEchu)} échus` : ""}.`}
                {" Ce rappel n'empêche pas de poursuivre."}
              </div>
            )}
            {e.numero === 10 && (e.etat === "courante" || e.etat === "terminee") && (
              <FicheInjection key={e.etat} projetId={projetId} dalleId={dalleId} />
            )}
            {e.numero === 9 && (e.etat === "courante" || e.etat === "terminee") && (
              <FicheTension key={e.etat} projetId={projetId} dalleId={dalleId} />
            )}

              </div>
            )}
          </div>

          {vue && e && aDesActions && (
            <div className="shrink-0 border-t border-bordure bg-white px-6 py-3">
              {erreur && erreurEtape === e.numero && <div role="alert" className="mb-2 rounded-lg bg-rouge-fond px-4 py-2.5 text-[13px] font-semibold text-rouge-texte">{erreur}</div>}
            {e.peutRealiser && (
              <div className="mt-2">
                <textarea rows={1} className={ZONE} placeholder={e.numero === 9 ? "Remarque (obligatoire si des mesures sont hors tolérance)" : "Remarque (facultative)"} value={remarque} onChange={(ev) => setRemarque(ev.target.value)} />
                {e.numero === 10 && (
                  <label className="mt-2 block">
                    <span className="mb-1 block text-[13px] font-semibold text-secondaire">Votre mot de passe, en signature de la fiche d&apos;injection</span>
                    <input type="password" autoComplete="current-password" className={ZONE} value={motDePasse} onChange={(ev) => setMotDePasse(ev.target.value)} />
                  </label>
                )}
                <Button size="sm" className="mt-2" disabled={envoi || (e.numero === 10 && !motDePasse)} onClick={() => agir(`${e.numero}/realiser`, { remarque, motDePasse })}>
                  {e.doubleValidation && !e.validationAvant ? "Soumettre la commande à validation" : "Valider l'étape"}
                </Button>
              </div>
            )}
            {e.etat === "courante" && !e.peutRealiser && !vue.suspendu && <div className="mt-1 text-[13px] text-secondaire">Étape en cours. Votre rôle ne permet pas de la valider.</div>}
            {(e.peutValiderTechnique || e.peutValiderAdministratif || e.peutValiderChantier) && (
              <div className="mt-2 flex flex-wrap gap-2">
                {e.peutValiderTechnique && <Button size="sm" disabled={envoi} onClick={() => agir(`${e.numero}/valider`, { type: "technique" })}>Validation technique</Button>}
                {e.peutValiderAdministratif && <Button size="sm" disabled={envoi} onClick={() => agir(`${e.numero}/valider`, { type: "administrative" })}>Validation administrative</Button>}
                {e.peutValiderChantier && <Button size="sm" disabled={envoi} onClick={() => agir(`${e.numero}/valider`, { type: "chantier" })}>Validation du responsable des chantiers</Button>}
              </div>
            )}
            {e.peutDeverrouiller && (deverrou?.n === e.numero ? (
              <div className="mt-2">
                <textarea rows={2} autoFocus className={ZONE} placeholder="Motif du déverrouillage" value={deverrou.motif} onChange={(ev) => setDeverrou({ n: e.numero, motif: ev.target.value })} />
                <div className="mt-2 flex gap-2">
                  <Button size="sm" variant="danger" disabled={envoi || !deverrou.motif.trim()} onClick={() => agir(`${e.numero}/deverrouiller`, { motif: deverrou.motif })}>Déverrouiller</Button>
                  <Button size="sm" variant="outline" onClick={() => setDeverrou(null)}>Annuler</Button>
                </div>
              </div>
            ) : (
              <button onClick={() => setDeverrou({ n: e.numero, motif: "" })} className="mt-1.5 inline-flex cursor-pointer items-center gap-1 text-[12px] font-semibold text-secondaire hover:text-rouge-texte">
                <Lock size={12} />Déverrouiller cette étape
              </button>
            ))}
            </div>
          )}
        </div>
      </div>
    </Sheet>
  );
}

"use client";

import { useEffect, useState } from "react";
import { Lock, Plus, Trash2 } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Field, Input, Select } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";

type Module = { code: string; label: string; aide: string; maximum: number; accordable: boolean };
type Fonction = {
  code: string; label: string; origine: boolean; reference: string; referenceLabel: string; utilisateurs: number; verrouillee: boolean;
  droits: Record<string, number>; parDefaut: Record<string, number>; plafonds: Record<string, number>;
};
type Vue = { modules: Module[]; references: { code: string; label: string }[]; fonctions: Fonction[] };

const NIVEAUX = ["Aucun accès", "Lecture", "Écriture"];
const egal = (a: Record<string, number>, b: Record<string, number>) => Object.keys(a).every((k) => a[k] === b[k]);

/**
 * Habilitations : niveau d'accès de chaque fonction par module, et création de fonctions.
 * Réservé au directeur et au chef de projet.
 */
export function HabilitationsParam() {
  const { user } = useAuth();
  const gestion = user?.role === "directeur" || user?.role === "chef_projet";
  const [vue, setVue] = useState<Vue | null>(null);
  const [code, setCode] = useState("");
  const [droits, setDroits] = useState<Record<string, number>>({});
  const [nom, setNom] = useState("");
  const [message, setMessage] = useState<{ ok: boolean; texte: string } | null>(null);
  const [envoi, setEnvoi] = useState(false);
  const [creation, setCreation] = useState(false);
  const [suppression, setSuppression] = useState(false);

  const choisir = (v: Vue, c: string) => {
    const f = v.fonctions.find((x) => x.code === c) ?? v.fonctions.find((x) => !x.verrouillee) ?? v.fonctions[0];
    setCode(f.code); setDroits({ ...f.droits }); setNom(f.label);
  };
  const recevoir = (v: Vue, c: string) => { setVue(v); choisir(v, c); };

  useEffect(() => {
    if (gestion) api<Vue>("/habilitations").then((v) => recevoir(v, "")).catch(() => setMessage({ ok: false, texte: "Chargement des habilitations impossible." }));
    // Chargement unique à l'ouverture de l'écran.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [gestion]);

  if (!gestion) return null;
  const f = vue?.fonctions.find((x) => x.code === code);
  const modifie = !!f && (!egal(droits, f.droits) || nom.trim() !== f.label);

  async function agir(action: () => Promise<Vue>, suivant: string, texte: string) {
    setEnvoi(true); setMessage(null);
    try { recevoir(await action(), suivant); setMessage({ ok: true, texte }); }
    catch (e) { setMessage({ ok: false, texte: e instanceof ApiError ? e.message : "Enregistrement impossible." }); }
    finally { setEnvoi(false); setSuppression(false); }
  }

  return (
    <div className="rounded-xl border border-bordure bg-white p-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-[15px] font-bold">Habilitations par fonction</h2>
          <p className="mt-1 max-w-3xl text-[13px] text-secondaire">
            Pour chaque fonction, réglez l&apos;accès à chaque module : aucun accès, lecture, ou écriture. Le réglage s&apos;applique tout de suite,
            sans reconnexion. Les validations du chantier, les signatures, la suppression et le blocage d&apos;un projet restent attachés aux fonctions d&apos;origine.
          </p>
        </div>
        <Button variant="outline" onClick={() => setCreation(true)}><Plus size={16} />Nouvelle fonction</Button>
      </div>

      {message && <div role={message.ok ? "status" : "alert"} className={cn("mt-4 rounded-lg px-4 py-3 text-[13px]", message.ok ? "bg-vert-fond text-vert-texte" : "bg-rouge-fond text-rouge-texte")}>{message.texte}</div>}

      {vue && f && (
        <div className="mt-5 grid gap-6 lg:grid-cols-[260px_minmax(0,1fr)]">
          <nav aria-label="Fonctions" className="flex gap-1 overflow-x-auto lg:max-h-[640px] lg:flex-col lg:overflow-y-auto">
            {vue.fonctions.map((x) => (
              <button key={x.code} type="button" aria-current={x.code === code ? "true" : undefined}
                onClick={() => { choisir(vue, x.code); setMessage(null); }}
                className={cn("flex shrink-0 cursor-pointer items-center justify-between gap-3 rounded-lg border px-3 py-2 text-left",
                  x.code === code ? "border-marine bg-marine/5" : "border-transparent hover:bg-fond")}>
                <span className="min-w-0">
                  <span className="block truncate font-semibold">{x.label}</span>
                  <span className="block truncate text-[12px] text-secondaire">
                    {x.origine ? "Fonction d'origine" : `Référence : ${x.referenceLabel}`}
                  </span>
                </span>
                <span className="shrink-0 font-display text-[12px] text-attenue" title={`${x.utilisateurs} utilisateur(s)`}>{x.utilisateurs}</span>
              </button>
            ))}
          </nav>

          <div className="min-w-0">
            <div className="flex flex-wrap items-end gap-3">
              {f.origine ? (
                <div>
                  <div className="font-display text-lg font-bold">{f.label}</div>
                  <div className="text-[13px] text-secondaire">Fonction d&apos;origine, {f.utilisateurs} utilisateur{f.utilisateurs > 1 ? "s" : ""}</div>
                </div>
              ) : (
                <>
                  <Field label="Nom de la fonction"><Input className="w-64" value={nom} maxLength={100} onChange={(e) => setNom(e.target.value)} /></Field>
                  <div className="pb-2 text-[13px] text-secondaire">Référence : {f.referenceLabel}, {f.utilisateurs} utilisateur{f.utilisateurs > 1 ? "s" : ""}</div>
                </>
              )}
            </div>

            {f.verrouillee && (
              <p className="mt-4 flex items-center gap-2 rounded-lg bg-fond px-4 py-3 text-[13px] text-secondaire">
                <Lock size={15} aria-hidden />Le directeur garde l&apos;accès à tout : ses droits ne se règlent pas, pour qu&apos;il ne puisse jamais se retrouver enfermé dehors.
              </p>
            )}

            <ul className="mt-4 divide-y divide-bordure rounded-lg border border-bordure">
              {vue.modules.map((m) => {
                const n = droits[m.code] ?? 0, plafond = f.plafonds[m.code] ?? 0;
                return (
                  <li key={m.code} className="flex flex-wrap items-center justify-between gap-x-6 gap-y-2 px-4 py-3">
                    <div className="min-w-[220px] flex-1">
                      <div className="font-semibold">
                        {m.label}
                        {n !== f.parDefaut[m.code] && <span className="ml-2 text-[12px] font-normal text-orange-texte">modifié</span>}
                      </div>
                      <div className="text-[12px] text-secondaire">{m.aide}</div>
                      {!f.verrouillee && !m.accordable && plafond < m.maximum && (
                        <div className="text-[12px] text-attenue">
                          {plafond === 0 ? "Fermé pour cette fonction de référence." : "Limité à la lecture pour cette fonction de référence."}
                        </div>
                      )}
                    </div>
                    <div role="radiogroup" aria-label={`Accès au module ${m.label}`} className="flex shrink-0 overflow-hidden rounded-lg border border-bordure-forte">
                      {NIVEAUX.slice(0, m.maximum + 1).map((label, i) => (
                        <button key={label} type="button" role="radio" aria-checked={n === i} disabled={f.verrouillee || i > plafond}
                          onClick={() => setDroits({ ...droits, [m.code]: i })}
                          title={i > plafond && !f.verrouillee ? "Ce niveau dépend de la fonction de référence" : undefined}
                          className={cn("border-l border-bordure-forte px-3 py-1.5 text-[13px] font-semibold first:border-l-0",
                            n === i ? (i === 0 ? "bg-neutre-fond text-texte" : i === 1 ? "bg-violet-fond text-violet-texte" : "bg-marine text-white") : "bg-white text-secondaire",
                            f.verrouillee || i > plafond ? "cursor-not-allowed opacity-45" : n !== i && "cursor-pointer hover:bg-fond")}>
                          {label}
                        </button>
                      ))}
                    </div>
                  </li>
                );
              })}
            </ul>

            {!f.verrouillee && (
              <div className="mt-4 flex flex-wrap items-center gap-3">
                <Button disabled={!modifie || envoi || nom.trim().length < 2}
                  onClick={() => agir(() => api<Vue>(`/habilitations/${f.code}`, { method: "PUT", body: JSON.stringify({ label: f.origine ? null : nom.trim(), droits }) }), f.code, "Habilitations enregistrées. Elles s'appliquent dès maintenant.")}>
                  Enregistrer
                </Button>
                <Button variant="outline" disabled={egal(droits, f.parDefaut) || envoi} onClick={() => setDroits({ ...f.parDefaut })}>
                  Remettre les niveaux {f.origine ? "d'origine" : "de la fonction de référence"}
                </Button>
                {!f.origine && (
                  <Button variant="ghost" className="ml-auto text-rouge-texte" disabled={envoi} onClick={() => setSuppression(true)}><Trash2 size={15} />Supprimer la fonction</Button>
                )}
              </div>
            )}
          </div>
        </div>
      )}

      {creation && vue && (
        <NouvelleFonction references={vue.references} onClose={() => setCreation(false)}
          onCree={(v, label) => { setCreation(false); recevoir(v, v.fonctions.find((x) => !x.origine && x.label === label)?.code ?? ""); setMessage({ ok: true, texte: `Fonction « ${label} » créée. Réglez ses accès, puis donnez-la à un utilisateur depuis sa fiche.` }); }} />
      )}

      <Modal open={suppression} onOpenChange={setSuppression} title={`Supprimer la fonction « ${f?.label ?? ""} »`}
        description="Une fonction se supprime tant qu'aucun utilisateur ne la porte. Cette action ne peut pas être annulée."
        footer={
          <>
            <Button variant="outline" onClick={() => setSuppression(false)}>Annuler</Button>
            <Button variant="danger" disabled={envoi} onClick={() => f && agir(() => api<Vue>(`/habilitations/${f.code}`, { method: "DELETE" }), "", "Fonction supprimée.")}>Supprimer</Button>
          </>
        } />
    </div>
  );
}

function NouvelleFonction({ references, onClose, onCree }: { references: { code: string; label: string }[]; onClose: () => void; onCree: (v: Vue, label: string) => void }) {
  const [label, setLabel] = useState("");
  const [reference, setReference] = useState("");
  const [erreur, setErreur] = useState("");
  const [envoi, setEnvoi] = useState(false);

  async function creer() {
    setEnvoi(true); setErreur("");
    try { onCree(await api<Vue>("/habilitations", { method: "POST", body: JSON.stringify({ label: label.trim(), reference }) }), label.trim()); }
    catch (e) { setErreur(e instanceof ApiError ? e.message : "Création impossible."); setEnvoi(false); }
  }

  return (
    <Modal open onOpenChange={(o) => !o && onClose()} title="Nouvelle fonction"
      description="La fonction de référence donne à la nouvelle fonction ses validations, ses signatures et son tableau de bord. Vous réglez ensuite ses accès module par module."
      footer={
        <>
          <Button variant="outline" onClick={onClose}>Annuler</Button>
          <Button disabled={envoi || label.trim().length < 2 || !reference} onClick={creer}>Créer la fonction</Button>
        </>
      }>
      <div className="space-y-4">
        <Field label="Nom de la fonction"><Input autoFocus value={label} maxLength={100} placeholder="Par exemple : Ingénieur stagiaire" onChange={(e) => setLabel(e.target.value)} /></Field>
        <Field label="Fonction de référence">
          <Select value={reference} onChange={(e) => setReference(e.target.value)}>
            <option value="">Choisir une fonction</option>
            {references.map((r) => <option key={r.code} value={r.code}>{r.label}</option>)}
          </Select>
        </Field>
        {erreur && <div role="alert" className="rounded-lg bg-rouge-fond px-4 py-3 text-[13px] text-rouge-texte">{erreur}</div>}
      </div>
    </Modal>
  );
}

"use client";

import { useCallback, useEffect, useState } from "react";
import { Plus, Trash2 } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn, formatDate, formatSurface } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field, Input, Select } from "@/components/ui/input";
import { Sheet } from "@/components/ui/sheet";
import { SignatureDoc } from "@/components/signature-doc";

type Courrier = { id: string; numero: string; ancienNumero: string | null; type: string; typeLabel: string; dateCourrier: string; objet: string; modification: boolean; nbLignes: number; emisPar: string; signePar: string | null; signeLe: string | null; annule: boolean; motifAnnulation: string | null; annulePar: string | null };
type Dalle = { id: string; reference: string; emplacement: string; surface: number; realisee: boolean; conforme: boolean | null };
type Vue = {
  lectureSeule: boolean; peutAnnuler: boolean; peutConformite: boolean; types: { code: string; label: string }[]; courriers: Courrier[];
  preparation: { objet: string; entreprise: string | null; bct: string | null; bet: string | null; ville: string | null; dalles: Dalle[] };
};
type Ligne = { dalleId: string; numeroPlan: string; designation: string; nombre: string };
const iso = (d: Date) => d.toLocaleDateString("fr-CA");

/** Bordereaux de plans et attestations de conformité du projet. */
export function CourriersProjet({ projetId }: { projetId: string }) {
  const [v, setV] = useState<Vue | null>(null);
  const [creation, setCreation] = useState(false);
  const [aAnnuler, setAAnnuler] = useState<string | null>(null);
  const [motif, setMotif] = useState("");
  const [erreur, setErreur] = useState("");
  const charger = useCallback(() => { api<Vue>(`/projets/${projetId}/courriers`).then(setV).catch(() => setV(null)); }, [projetId]);
  useEffect(() => { charger(); }, [charger]);
  if (!v) return null;
  const annuler = async () => {
    setErreur("");
    try { await api(`/courriers/${aAnnuler}/annuler`, { method: "POST", body: JSON.stringify({ motif }) }); setAAnnuler(null); charger(); }
    catch (e) { setErreur(e instanceof ApiError ? e.message : "Annulation impossible."); }
  };

  return (
    <section className="mt-6 rounded-xl border border-bordure bg-white">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-bordure px-6 py-4">
        <div>
          <h2 className="font-display text-[15px] font-bold">Bordereaux et attestations</h2>
          <div className="text-[13px] text-secondaire">Envoi des plans au bureau de contrôle, dépôt au bureau d&apos;études, attestation de conformité des dalles réalisées.</div>
        </div>
        {!v.lectureSeule && <Button variant="outline" onClick={() => setCreation(true)}><Plus size={16} />Nouveau courrier</Button>}
      </div>
      {v.courriers.length === 0 ? <div className="px-6 py-8 text-center text-[13px] text-secondaire">Aucun courrier pour ce projet.</div> : (
        <ul className="divide-y divide-bordure">
          {v.courriers.map((c) => (
            <li key={c.id} className="px-6 py-4 text-[13px]">
              <div className="flex flex-wrap items-center gap-2">
                <span className={cn("font-display font-bold text-marine", c.annule && "text-attenue line-through")}>{c.numero}</span>
                <Badge ton={c.type === "attestation" ? "vert" : "neutre"}>{c.typeLabel}</Badge>
                {c.modification && <Badge ton="orange">Modification</Badge>}
                <span className="text-secondaire">· <span className="font-display">{formatDate(c.dateCourrier)}</span> · {c.nbLignes} {c.type === "attestation" ? "dalle(s)" : "plan(s)"}</span>
                {c.annule && <Badge ton="rouge">Annulé</Badge>}
                {!c.annule && v.peutAnnuler && <button className="ml-auto cursor-pointer text-[12px] font-semibold text-rouge-texte hover:underline" onClick={() => { setAAnnuler(c.id); setMotif(""); setErreur(""); }}>Annuler</button>}
              </div>
              <div className="text-[12px] text-attenue">{c.objet} · établi par {c.emisPar}{c.ancienNumero ? ` · ancien numéro ${c.ancienNumero}` : ""}{c.annule ? ` · annulé par ${c.annulePar} : ${c.motifAnnulation}` : ""}</div>
              {(!c.annule || c.signePar) && <SignatureDoc<Courrier> signePar={c.signePar} signeLe={c.signeLe} quoi="le courrier" url={`/courriers/${c.id}`} onSigne={() => charger()} />}
              {aAnnuler === c.id && (
                <div className="mt-2">
                  <div className="flex items-end gap-2">
                    <Field label="Motif de l'annulation" required className="flex-1"><Input autoFocus value={motif} onChange={(e) => setMotif(e.target.value)} /></Field>
                    <Button variant="outline" onClick={() => setAAnnuler(null)}>Fermer</Button>
                    <Button disabled={!motif.trim()} onClick={annuler}>Confirmer</Button>
                  </div>
                  {erreur && <div role="alert" className="mt-2 rounded-lg bg-rouge-fond px-3 py-2 text-[12px] text-rouge-texte">{erreur}</div>}
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
      {creation && <Creation projetId={projetId} v={v} recharger={charger} onClose={() => setCreation(false)} onFait={() => { setCreation(false); charger(); }} />}
    </section>
  );
}

function Creation({ projetId, v, recharger, onClose, onFait }: { projetId: string; v: Vue; recharger: () => void; onClose: () => void; onFait: () => void }) {
  const p = v.preparation;
  const [type, setType] = useState("bordereau_bct");
  const [date, setDate] = useState(iso(new Date()));
  const [lieu, setLieu] = useState("Casablanca");
  const [objet, setObjet] = useState(p.objet);
  const [entreprise, setEntreprise] = useState(p.entreprise ?? "");
  const [modification, setModification] = useState(false);
  const [lignes, setLignes] = useState<Ligne[]>([]);
  const [exclues, setExclues] = useState<string[]>([]);
  const [remarque, setRemarque] = useState<{ id: string; texte: string } | null>(null);
  // Seules les dalles réalisées et déclarées conformes entrent dans l'attestation.
  const attestees = p.dalles.filter((d) => d.realisee && d.conforme === true && !exclues.includes(d.id)).map((d) => d.id);
  const declarer = async (d: Dalle, conforme: boolean, texte?: string) => {
    setErreur("");
    try { await api(`/projets/${projetId}/dalles/${d.id}/conformite`, { method: "PUT", body: JSON.stringify({ conforme, remarque: texte ?? null }) }); setRemarque(null); recharger(); }
    catch (e) { setErreur(e instanceof ApiError ? e.message : "Enregistrement impossible."); }
  };
  const [erreur, setErreur] = useState("");
  const [envoi, setEnvoi] = useState(false);
  const attestation = type === "attestation";
  const manque = type === "bordereau_bet" ? (!p.bet ? "Aucun bureau d'études n'est rattaché au projet." : "") : (!p.bct ? "Aucun bureau de contrôle n'est rattaché au projet." : "");
  const maj = (i: number, x: Partial<Ligne>) => setLignes(lignes.map((l, j) => (j === i ? { ...l, ...x } : l)));
  const ajouter = (dalleId: string) => { const d = p.dalles.find((x) => x.id === dalleId); setLignes([...lignes, { dalleId, numeroPlan: "", designation: d?.emplacement ?? "", nombre: "" }]); };

  const enregistrer = async () => {
    setEnvoi(true); setErreur("");
    const corps = {
      type, dateCourrier: date, lieu, objet, entreprise, modification,
      lignes: attestation ? attestees.map((id) => ({ dalleId: id, nombre: 0 })) : lignes.map((l) => ({ dalleId: l.dalleId || null, numeroPlan: l.numeroPlan, designation: l.designation, nombre: parseInt(l.nombre, 10) || 0 })),
    };
    try { await api(`/projets/${projetId}/courriers`, { method: "POST", body: JSON.stringify(corps) }); onFait(); }
    catch (e) { setErreur(e instanceof ApiError ? e.message : "Enregistrement impossible."); setEnvoi(false); }
  };
  const pret = !manque && (attestation ? attestees.length > 0 : lignes.length > 0 && lignes.every((l) => l.numeroPlan.trim() && parseInt(l.nombre, 10) > 0));

  return (
    <Sheet open onOpenChange={(o) => !o && onClose()} title="Nouveau courrier" description="Le numéro est attribué à l'enregistrement. Le courrier se signe ensuite avant impression.">
      <div className="grid grid-cols-2 gap-3">
        <Field label="Type" required className="col-span-2">
          <Select value={type} onChange={(e) => setType(e.target.value)}>{v.types.map((t) => <option key={t.code} value={t.code}>{t.label}</option>)}</Select>
        </Field>
        <Field label="Date" required><Input type="date" value={date} onChange={(e) => setDate(e.target.value)} /></Field>
        <Field label="Fait à"><Input value={lieu} onChange={(e) => setLieu(e.target.value)} /></Field>
        <Field label="Objet" required className="col-span-2"><Input value={objet} onChange={(e) => setObjet(e.target.value)} /></Field>
        <Field label={attestation ? "Entreprise de construction" : "ETP, entreprise"} className="col-span-2"><Input value={entreprise} onChange={(e) => setEntreprise(e.target.value)} /></Field>
      </div>
      <div className="mt-3 rounded-lg bg-fond px-4 py-3 text-[13px] text-secondaire">
        {type !== "bordereau_bet" && <div>Bureau de contrôle : <span className="font-semibold text-texte">{p.bct ?? "non renseigné"}</span></div>}
        {!attestation && <div>Bureau d&apos;études : <span className="font-semibold text-texte">{p.bet ?? "non renseigné"}</span></div>}
        {!attestation && <div>Projet, ville : <span className="font-semibold text-texte">{p.ville ?? "non renseignée"}</span></div>}
        <div className="text-[12px] text-attenue">Ces informations viennent de la fiche du projet et de ses partenaires.</div>
      </div>
      {manque && <div role="alert" className="mt-3 rounded-lg bg-rouge-fond px-4 py-3 text-[13px] text-rouge-texte">{manque} Ajoutez-le dans les partenaires du projet.</div>}

      {attestation ? (
        <div className="mt-4">
          <div className="mb-2 font-semibold">Dalles attestées</div>
          <ul className="divide-y divide-bordure rounded-lg border border-bordure">
            {p.dalles.map((d) => (
              <li key={d.id} className="px-3 py-2.5 text-[13px]">
                <div className="flex flex-wrap items-center gap-3">
                  <input type="checkbox" className="h-4 w-4 accent-marine" aria-label={`Inclure ${d.reference}`} disabled={!(d.realisee && d.conforme === true)} checked={attestees.includes(d.id)}
                    onChange={(e) => setExclues(e.target.checked ? exclues.filter((x) => x !== d.id) : [...exclues, d.id])} />
                  <span className="font-display font-bold">{d.reference}</span><span className="flex-1 text-secondaire">{d.emplacement}</span>
                  {!d.realisee ? <span className="text-[12px] text-attenue">non réalisée</span> : (
                    <span className="flex overflow-hidden rounded-full border border-bordure-forte text-[12px] font-semibold" role="group" aria-label={`Conformité de ${d.reference}`}>
                      <button disabled={!v.peutConformite} aria-pressed={d.conforme === true} onClick={() => declarer(d, true)}
                        className={cn("px-3 py-1", d.conforme === true ? "bg-vert-fond text-vert-texte" : "bg-white text-secondaire", v.peutConformite && "cursor-pointer")}>Conforme</button>
                      <button disabled={!v.peutConformite} aria-pressed={d.conforme === false} onClick={() => setRemarque({ id: d.id, texte: "" })}
                        className={cn("border-l border-bordure-forte px-3 py-1", d.conforme === false ? "bg-rouge-fond text-rouge-texte" : "bg-white text-secondaire", v.peutConformite && "cursor-pointer")}>Non conforme</button>
                    </span>
                  )}
                </div>
                {remarque?.id === d.id && (
                  <div className="mt-2 flex items-end gap-2">
                    <Field label="Pourquoi la dalle n'est pas conforme" required className="flex-1"><Input autoFocus value={remarque.texte} onChange={(e) => setRemarque({ id: d.id, texte: e.target.value })} /></Field>
                    <Button variant="outline" onClick={() => setRemarque(null)}>Fermer</Button>
                    <Button disabled={!remarque.texte.trim()} onClick={() => declarer(d, false, remarque.texte)}>Confirmer</Button>
                  </div>
                )}
              </li>
            ))}
          </ul>
          <p className="mt-2 text-[12px] text-attenue">Une dalle se déclare conforme ou non conforme une fois ses 11 étapes faites. L&apos;attestation ne peut contenir que des dalles conformes.{!v.peutConformite ? " L'avis de conformité revient au directeur, au chef de projet et aux ingénieurs." : ""}</p>
        </div>
      ) : (
        <div className="mt-4">
          <label className="mb-3 flex cursor-pointer items-center gap-2 text-[13px]"><input type="checkbox" className="h-4 w-4 accent-marine" checked={modification} onChange={(e) => setModification(e.target.checked)} />Plans modifiés (mention « MODIFICATION »)</label>
          <div className="mb-2 font-semibold">Plans transmis</div>
          <div className="space-y-3">
            {lignes.map((l, i) => {
              const d = p.dalles.find((x) => x.id === l.dalleId);
              return (
                <div key={i} className="rounded-lg border border-bordure p-3">
                  <div className="mb-2 flex items-center justify-between gap-2 text-[13px]">
                    <span><span className="font-display font-bold">{d?.reference ?? "Plan hors dalle"}</span>{d && type === "bordereau_bct" ? ` · ${formatSurface(d.surface)}` : ""}</span>
                    <Button variant="ghost" size="icon" aria-label="Retirer ce plan" onClick={() => setLignes(lignes.filter((_, j) => j !== i))}><Trash2 size={15} /></Button>
                  </div>
                  <div className="grid grid-cols-3 gap-2">
                    <Field label="Numéro de plan" required className="col-span-2"><Input placeholder="MK4-337KD-0526B" value={l.numeroPlan} onChange={(e) => maj(i, { numeroPlan: e.target.value })} /></Field>
                    <Field label="Exemplaires" required><Input inputMode="numeric" value={l.nombre} onChange={(e) => maj(i, { nombre: e.target.value })} /></Field>
                    <Field label="Désignation" className="col-span-3"><Input value={l.designation} onChange={(e) => maj(i, { designation: e.target.value })} /></Field>
                  </div>
                </div>
              );
            })}
          </div>
          <Select className="mt-3" aria-label="Ajouter un plan" value="" onChange={(e) => { if (e.target.value) ajouter(e.target.value === "libre" ? "" : e.target.value); }}>
            <option value="">Ajouter le plan d&apos;une dalle...</option>
            {p.dalles.map((d) => <option key={d.id} value={d.id}>{d.reference} · {d.emplacement}</option>)}
            <option value="libre">Plan sans dalle</option>
          </Select>
        </div>
      )}
      {erreur && <div role="alert" className="mt-3 rounded-lg bg-rouge-fond px-4 py-3 text-[13px] text-rouge-texte">{erreur}</div>}
      <div className="mt-4 flex justify-end gap-2"><Button variant="outline" onClick={onClose}>Fermer</Button><Button disabled={envoi || !pret || !objet.trim()} onClick={enregistrer}>Enregistrer le courrier</Button></div>
    </Sheet>
  );
}

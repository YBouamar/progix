"use client";

import { useEffect, useState } from "react";
import { Plus } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn, filtrerDecimal, formatDate, formatDH, formatNombre, lireNombre } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field, Input, Select } from "@/components/ui/input";
import { Sheet } from "@/components/ui/sheet";

export type DallePaiement = {
  dalleId: string; reference: string; emplacement: string; montantTTC: number; avoirs: number; du: number;
  paye: number; effetsEnAttente: number; reste: number; aCouvrir: number; tropPercu: number; taux: number | null;
};
type Paiement = {
  id: string; datePaiement: string; mode: string; modeLabel: string; montant: number; reference: string | null; banque: string | null;
  echeanceEffet: string | null; enAttente: boolean; remboursement: boolean; numeroRecu: string | null; saisiPar: string;
  annule: boolean; motifAnnulation: string | null; annulePar: string | null; dalles: { dalleId: string; reference: string; montant: number }[];
};
export type VuePaiements = {
  peutSaisir: boolean; peutAnnuler: boolean; plafondEspeces: number;
  totaux: { montantTTC: number; avoirs: number; paye: number; effetsEnAttente: number; reste: number; taux: number | null };
  dalles: DallePaiement[]; horsDevis: string[]; paiements: Paiement[];
};
type Especes = { plafond: number; deja: number; disponible: number };

const iso = (d: Date) => d.toISOString().slice(0, 10);
const enSaisie = (n: number) => (n > 0 ? String(Math.round(n * 100) / 100).replace(".", ",") : "");

/** Barre et pourcentage du taux de paiement d'une dalle (payé sur le montant TTC du devis). */
export function TauxPaiement({ taux, className }: { taux: number | null; className?: string }) {
  if (taux === null) return <span className={cn("text-[12px] text-attenue", className)}>Hors devis</span>;
  const ton = taux >= 100 ? "bg-[#1e9e58]" : taux > 0 ? "bg-marine" : "bg-bordure";
  return (
    <span className={cn("inline-flex items-center gap-2", className)} title={`${formatNombre(taux)} % du montant TTC du devis payé`}>
      <span className="h-1.5 w-16 overflow-hidden rounded-sm bg-bordure" aria-hidden>
        <span className={cn("block h-full", ton)} style={{ width: `${Math.min(100, Math.max(0, taux))}%` }} />
      </span>
      <span className={cn("font-display text-[13px] font-semibold", taux >= 100 && "text-vert-texte")}>{taux.toLocaleString("fr-FR", { maximumFractionDigits: 1 })} %</span>
    </span>
  );
}

/** Paiements du projet suivis par dalle : taux de paiement, saisie ventilée, reçus, annulation, remboursement d'un trop-perçu. */
export function PaiementsProjet({ projetId, onChange }: { projetId: string; onChange?: () => void }) {
  const [vue, setVue] = useState<VuePaiements | null>(null);
  const [saisie, setSaisie] = useState(false);
  const [rembourser, setRembourser] = useState<DallePaiement | null>(null);
  const [aAnnuler, setAAnnuler] = useState<string | null>(null);
  const [motif, setMotif] = useState("");
  const [erreur, setErreur] = useState("");

  const charger = () => api<VuePaiements>(`/projets/${projetId}/paiements`).then(setVue).catch(() => setVue(null));
  useEffect(() => { charger(); }, [projetId]); // eslint-disable-line react-hooks/exhaustive-deps
  const fait = (v: VuePaiements) => { setVue(v); setSaisie(false); setRembourser(null); onChange?.(); };

  async function annuler() {
    setErreur("");
    try {
      await api(`/encaissements/${aAnnuler}/annuler`, { method: "POST", body: JSON.stringify({ motif }) });
      setAAnnuler(null); setMotif("");
      await charger(); onChange?.();
    } catch (e) { setErreur(e instanceof ApiError ? e.message : "Annulation impossible."); }
  }

  if (!vue) return null;
  const t = vue.totaux;
  return (
    <section className="mt-6 rounded-xl border border-bordure bg-white">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-bordure px-6 py-4">
        <div>
          <h2 className="font-display text-[15px] font-bold">Paiements par dalle</h2>
          <div className="text-[13px] text-secondaire">Taux de paiement = payé sur le montant TTC de la dalle au devis validé. Les factures sont réglées par imputation des paiements de leurs dalles.</div>
        </div>
        {vue.peutSaisir && vue.dalles.length > 0 && <Button size="sm" onClick={() => setSaisie(true)}><Plus size={14} />Saisir un paiement</Button>}
      </div>

      {vue.dalles.length === 0 ? (
        <div className="px-6 py-8 text-center text-secondaire">Aucune dalle chiffrée dans un devis validé : pas de paiement à suivre.</div>
      ) : (
        <div className="overflow-x-auto"><table className="w-full text-left text-[13px]">
          <thead className="border-b border-bordure bg-fond text-[12px] uppercase tracking-wide text-secondaire">
            <tr>
              <th className="px-5 py-2.5 font-semibold">Dalle</th>
              <th className="px-5 py-2.5 text-right font-semibold">Devis, TTC</th>
              <th className="px-5 py-2.5 text-right font-semibold">Payé</th>
              <th className="px-5 py-2.5 font-semibold">Taux</th>
              <th className="px-5 py-2.5 text-right font-semibold">Reste</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {vue.dalles.map((d) => (
              <tr key={d.dalleId} className="border-b border-bordure">
                <td className="px-5 py-2.5"><div className="font-display font-bold text-marine">{d.reference}</div><div className="text-[12px] text-secondaire">{d.emplacement}</div></td>
                <td className="whitespace-nowrap px-5 py-2.5 text-right font-display">
                  {formatDH(d.montantTTC)}
                  {d.avoirs > 0 && <div className="text-[12px] text-secondaire">avoirs − {formatDH(d.avoirs)}</div>}
                </td>
                <td className="whitespace-nowrap px-5 py-2.5 text-right font-display font-semibold text-vert-texte">
                  {formatDH(d.paye)}
                  {d.effetsEnAttente > 0 && <div className="text-[12px] font-normal text-orange-texte">+ {formatDH(d.effetsEnAttente)} en effets</div>}
                </td>
                <td className="px-5 py-2.5"><TauxPaiement taux={d.taux} /></td>
                <td className={cn("whitespace-nowrap px-5 py-2.5 text-right font-display", d.reste > 0 ? "" : "text-vert-texte")}>
                  {d.tropPercu > 0 ? <span className="text-rouge-texte">Trop-perçu {formatDH(d.tropPercu)}</span> : formatDH(Math.max(0, d.reste))}
                </td>
                <td className="px-3 py-2 text-right">
                  {vue.peutSaisir && d.tropPercu > 0 && <Button variant="outline" size="sm" onClick={() => setRembourser(d)}>Rembourser</Button>}
                </td>
              </tr>
            ))}
            <tr className="bg-marine/10">
              <td className="px-5 py-2.5 font-semibold">Total projet</td>
              <td className="whitespace-nowrap px-5 py-2.5 text-right font-display font-bold">{formatDH(t.montantTTC)}</td>
              <td className="whitespace-nowrap px-5 py-2.5 text-right font-display font-bold text-vert-texte">{formatDH(t.paye)}</td>
              <td className="px-5 py-2.5"><TauxPaiement taux={t.taux} /></td>
              <td className="whitespace-nowrap px-5 py-2.5 text-right font-display font-bold">{formatDH(Math.max(0, t.reste))}</td>
              <td />
            </tr>
          </tbody>
        </table></div>
      )}
      {vue.horsDevis.length > 0 && <p className="px-6 pt-3 text-[12px] text-secondaire">Hors de tout devis validé, sans taux : {vue.horsDevis.join(", ")}.</p>}

      <div className="px-6 py-4">
        <h3 className="mb-2 text-[14px] font-bold">Règlements reçus</h3>
        {erreur && <div role="alert" className="mb-3 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}
        {vue.paiements.length === 0 ? <p className="text-[13px] text-secondaire">Aucun règlement enregistré.</p> : (
          <ul className="divide-y divide-bordure rounded-lg border border-bordure">
            {vue.paiements.map((e) => (
              <li key={e.id} className="px-3 py-2.5 text-[13px]">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div className={cn(e.annule && "text-attenue line-through")}>
                    <span className="font-display">{formatDate(e.datePaiement)}</span> · <span className="font-semibold">{e.remboursement ? `Remboursement au client, ${e.modeLabel.toLowerCase()}` : e.modeLabel}</span>
                    {e.reference ? ` · ${e.reference}` : ""}{e.banque ? ` · ${e.banque}` : ""}
                    {e.echeanceEffet ? ` · ${e.enAttente ? "en attente, encaissé à l'échéance du" : "encaissé à l'échéance du"} ${formatDate(e.echeanceEffet)}` : ""}
                  </div>
                  <div className="flex items-center gap-3">
                    <span className={cn("whitespace-nowrap font-display font-bold", e.annule && "text-attenue line-through", e.enAttente && "text-orange-texte")}>{formatDH(e.remboursement ? -e.montant : e.montant)}</span>
                    {e.numeroRecu && <a href={`/api/encaissements/${e.id}/recu`} target="_blank" rel="noreferrer" className="text-[12px] font-semibold text-marine hover:underline">Imprimer le reçu</a>}
                    {!e.annule && vue.peutAnnuler && <button className="cursor-pointer text-[12px] font-semibold text-rouge-texte hover:underline" onClick={() => { setAAnnuler(e.id); setMotif(""); }}>Annuler</button>}
                  </div>
                </div>
                <div className="mt-0.5 text-[12px] text-secondaire">
                  {e.dalles.length > 0 ? e.dalles.map((x) => `${x.reference} : ${formatDH(x.montant)}`).join(" · ") : "Règlement d'avant le suivi par dalle, imputé à sa facture"}
                </div>
                <div className="text-[12px] text-attenue">
                  {e.numeroRecu ? `${e.numeroRecu} · signé par` : "Saisi par"} {e.saisiPar}{e.annule ? ` · annulé par ${e.annulePar} : ${e.motifAnnulation}` : ""}
                </div>
                {aAnnuler === e.id && (
                  <div className="mt-2 flex items-end gap-2">
                    <Field label="Motif de l'annulation" required className="flex-1"><Input autoFocus value={motif} onChange={(ev) => setMotif(ev.target.value)} /></Field>
                    <Button variant="outline" onClick={() => setAAnnuler(null)}>Fermer</Button>
                    <Button disabled={!motif.trim()} onClick={annuler}>Confirmer</Button>
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>

      {saisie && <SaisiePaiement projetId={projetId} vue={vue} onClose={() => setSaisie(false)} onFait={fait} />}
      {rembourser && <Remboursement projetId={projetId} dalle={rembourser} onClose={() => setRembourser(null)} onFait={fait} />}
    </section>
  );
}

/** Champs communs : date, mode, référence, banque, échéance d'un effet, plafond des espèces, mot de passe. */
function Reglement({ projetId, remboursement, f, set, especesMontant }: {
  projetId: string; remboursement: boolean; especesMontant: number;
  f: { date: string; mode: string; reference: string; banque: string; echeanceEffet: string; motDePasse: string };
  set: (k: "date" | "mode" | "reference" | "banque" | "echeanceEffet" | "motDePasse", v: string) => void;
}) {
  const [especes, setEspeces] = useState<Especes | null>(null);
  useEffect(() => {
    if (remboursement || f.mode !== "especes" || !f.date) { setEspeces(null); return; }
    api<Especes>(`/projets/${projetId}/especes?date=${f.date}`).then(setEspeces).catch(() => setEspeces(null));
  }, [projetId, remboursement, f.mode, f.date]);
  const libelleRef = f.mode === "cheque" ? "Numéro du chèque" : f.mode === "effet" ? "Numéro de l'effet" : "Référence du virement";
  const depasse = !!especes && especesMontant > especes.disponible;
  return (
    <>
      <div className="grid grid-cols-2 gap-3">
        <Field label={remboursement ? "Date du remboursement" : "Date du paiement"}><Input type="date" max={iso(new Date())} value={f.date} onChange={(e) => set("date", e.target.value)} /></Field>
        <Field label="Mode">
          <Select value={f.mode} onChange={(e) => set("mode", e.target.value)}>
            <option value="virement">Virement</option><option value="cheque">Chèque</option><option value="especes">Espèces</option>{!remboursement && <option value="effet">Effet</option>}
          </Select>
        </Field>
        {f.mode !== "especes" && <Field label={libelleRef} required={f.mode === "cheque" || f.mode === "effet"}><Input value={f.reference} onChange={(e) => set("reference", e.target.value)} /></Field>}
        {f.mode === "effet" && <Field label="Échéance de l'effet" required><Input type="date" value={f.echeanceEffet} onChange={(e) => set("echeanceEffet", e.target.value)} /></Field>}
        {f.mode !== "especes" && <Field label="Banque" className={f.mode === "effet" ? "col-span-2" : ""}><Input value={f.banque} onChange={(e) => set("banque", e.target.value)} /></Field>}
      </div>
      {especes && (
        <div className={cn("mt-3 rounded-lg px-4 py-3 text-[13px]", depasse ? "bg-rouge-fond text-rouge-texte" : "bg-orange-fond text-orange-texte")}>
          <div className="font-semibold">{depasse ? "Plafond espèces dépassé, encaissement refusé." : `Plafond espèces : ${formatDH(especes.plafond)} par client et par mois.`}</div>
          Ce client a déjà réglé {formatDH(especes.deja)} en espèces ce mois. Reste possible en espèces : {formatDH(especes.disponible)}.
        </div>
      )}
      <div className="mt-3">
        <Field label="Votre mot de passe" required><Input type="password" autoComplete="current-password" value={f.motDePasse} onChange={(e) => set("motDePasse", e.target.value)} /></Field>
        <p className="mt-1 text-[12px] text-attenue">Il signe électroniquement le reçu {remboursement ? "de remboursement" : "de paiement"}, qui porte votre nom, la date et un QR code de vérification.</p>
      </div>
    </>
  );
}

const nouveauReglement = () => ({ date: iso(new Date()), mode: "virement", reference: "", banque: "", echeanceEffet: "", motDePasse: "" });

/** Saisie d'un paiement réparti entre les dalles du projet ; un seul reçu pour le paiement. */
export function SaisiePaiement({ projetId, vue: initiale, titre, onClose, onFait }: {
  projetId: string; vue?: VuePaiements; titre?: string; onClose: () => void; onFait: (v: VuePaiements) => void;
}) {
  const [vue, setVue] = useState<VuePaiements | null>(initiale ?? null);
  const [f, setF] = useState(nouveauReglement);
  const [montants, setMontants] = useState<Record<string, string>>({});
  const [erreur, setErreur] = useState("");
  const [envoi, setEnvoi] = useState(false);
  useEffect(() => { if (!initiale) api<VuePaiements>(`/projets/${projetId}/paiements`).then(setVue).catch(() => setErreur("Chargement impossible.")); }, [projetId, initiale]);

  const ouvertes = (vue?.dalles ?? []).filter((d) => d.aCouvrir > 0);
  const total = ouvertes.reduce((s, d) => s + (lireNombre(montants[d.dalleId] ?? "") || 0), 0);
  const set = (k: keyof typeof f, v: string) => setF((x) => ({ ...x, [k]: v }));

  async function enregistrer() {
    setEnvoi(true); setErreur("");
    try {
      const r = await api<{ recuId: string; vue: VuePaiements }>(`/projets/${projetId}/paiements`, {
        method: "POST",
        body: JSON.stringify({
          datePaiement: f.date, mode: f.mode, reference: f.reference, banque: f.banque, echeanceEffet: f.mode === "effet" && f.echeanceEffet ? f.echeanceEffet : null, motDePasse: f.motDePasse,
          dalles: ouvertes.map((d) => ({ dalleId: d.dalleId, montant: lireNombre(montants[d.dalleId] ?? "") || 0 })).filter((x) => x.montant > 0),
        }),
      });
      window.open(`/api/encaissements/${r.recuId}/recu`, "_blank", "noopener");
      onFait(r.vue);
    } catch (e) { setErreur(e instanceof ApiError ? e.message : "Enregistrement impossible."); setEnvoi(false); }
  }

  return (
    <Sheet open large onOpenChange={(o) => !o && onClose()} title={titre ?? "Saisir un paiement"} description="Répartissez le montant reçu entre les dalles : un seul reçu est émis pour le paiement."
      footer={
        <>
          <Button variant="outline" onClick={onClose}>Annuler</Button>
          <Button disabled={envoi || total <= 0 || !f.motDePasse} onClick={enregistrer}>{envoi ? "Enregistrement..." : `Enregistrer ${total > 0 ? formatDH(total) : "le paiement"}`}</Button>
        </>
      }>
      {erreur && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}
      {vue && ouvertes.length === 0 ? <p className="text-secondaire">Toutes les dalles chiffrées de ce projet sont entièrement payées.</p> : (
        <>
          <table className="mb-4 w-full text-left text-[13px]">
            <thead className="border-b border-bordure text-[12px] uppercase tracking-wide text-secondaire">
              <tr><th className="py-2 font-semibold">Dalle</th><th className="py-2 font-semibold">Taux</th><th className="py-2 text-right font-semibold">Reste à payer</th><th className="w-40 py-2 text-right font-semibold">Montant affecté</th></tr>
            </thead>
            <tbody>
              {ouvertes.map((d) => (
                <tr key={d.dalleId} className="border-b border-bordure">
                  <td className="py-2"><div className="font-display font-bold text-marine">{d.reference}</div><div className="text-[12px] text-secondaire">{d.emplacement}</div></td>
                  <td className="py-2"><TauxPaiement taux={d.taux} /></td>
                  <td className="py-2 text-right font-display">
                    <button type="button" className="cursor-pointer hover:underline" title="Affecter tout le reste" onClick={() => setMontants((m) => ({ ...m, [d.dalleId]: enSaisie(d.aCouvrir) }))}>{formatDH(d.aCouvrir)}</button>
                  </td>
                  <td className="py-1.5 pl-3">
                    <Input className="text-right" inputMode="decimal" placeholder="0,00" aria-label={`Montant affecté à la dalle ${d.reference}`}
                      value={montants[d.dalleId] ?? ""} onChange={(e) => setMontants((m) => ({ ...m, [d.dalleId]: filtrerDecimal(e.target.value) }))} />
                  </td>
                </tr>
              ))}
              <tr><td colSpan={3} className="py-2 text-right font-semibold">Montant du paiement</td><td className="py-2 text-right font-display text-[15px] font-bold">{formatDH(total)}</td></tr>
            </tbody>
          </table>
          <Reglement projetId={projetId} remboursement={false} f={f} set={set} especesMontant={total} />
        </>
      )}
    </Sheet>
  );
}

/** Remboursement au client d'un trop-perçu sur une dalle. */
function Remboursement({ projetId, dalle, onClose, onFait }: { projetId: string; dalle: DallePaiement; onClose: () => void; onFait: (v: VuePaiements) => void }) {
  const [f, setF] = useState(nouveauReglement);
  const [montant, setMontant] = useState(enSaisie(dalle.tropPercu));
  const [erreur, setErreur] = useState("");
  const [envoi, setEnvoi] = useState(false);
  const set = (k: keyof typeof f, v: string) => setF((x) => ({ ...x, [k]: v }));
  async function enregistrer() {
    setEnvoi(true); setErreur("");
    try {
      const r = await api<{ recuId: string; vue: VuePaiements }>(`/projets/${projetId}/remboursements`, {
        method: "POST", body: JSON.stringify({ datePaiement: f.date, mode: f.mode, reference: f.reference, banque: f.banque, motDePasse: f.motDePasse, dalleId: dalle.dalleId, montant: lireNombre(montant) }),
      });
      window.open(`/api/encaissements/${r.recuId}/recu`, "_blank", "noopener");
      onFait(r.vue);
    } catch (e) { setErreur(e instanceof ApiError ? e.message : "Enregistrement impossible."); setEnvoi(false); }
  }
  return (
    <Sheet open onOpenChange={(o) => !o && onClose()} title="Rembourser un trop-perçu" description={`${dalle.reference} · trop-perçu de ${formatDH(dalle.tropPercu)}`}
      footer={<><Button variant="outline" onClick={onClose}>Annuler</Button><Button disabled={envoi || !f.motDePasse || !(lireNombre(montant) > 0)} onClick={enregistrer}>Enregistrer le remboursement</Button></>}>
      {erreur && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}
      <Field label="Montant remboursé, en DH" className="mb-3"><Input inputMode="decimal" value={montant} onChange={(e) => setMontant(filtrerDecimal(e.target.value))} /></Field>
      <Reglement projetId={projetId} remboursement f={f} set={set} especesMontant={0} />
      <p className="mt-3 text-[12px] text-attenue"><Badge>RMB</Badge> Le remboursement a sa propre série de reçus.</p>
    </Sheet>
  );
}

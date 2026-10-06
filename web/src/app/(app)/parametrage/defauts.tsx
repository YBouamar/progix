"use client";

import { useEffect, useState } from "react";
import { Plus, Trash2 } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn, lireNombre } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/input";

type Echeance = { label: string; pourcentage: string; retenue: boolean };
type Devis = { dureeValidite: number; modalites: string | null; modifiable: boolean; echeances: { label: string; pourcentage: number; retenue: boolean }[] };
type Coulis = { cimentType: string | null; cimentKg: number | null; cimentTemperature: number | null; eau: string | null; eauLitres: number | null; adjuvantType: string | null; adjuvantGrammes: number | null; modifiable: boolean };
type Message = { ok: boolean; texte: string } | null;
const fr = (n: number | null) => (n === null || n === undefined ? "" : String(n).replace(".", ","));
const nombre = (s: string) => (s.trim() === "" ? null : lireNombre(s));

function Retour({ message }: { message: Message }) {
  return message ? <span role={message.ok ? "status" : "alert"} className={cn("text-[13px]", message.ok ? "text-vert-texte" : "text-rouge-texte")}>{message.texte}</span> : null;
}

/** Valeurs proposées à la création d'un devis. */
export function DevisParam() {
  const [v, setV] = useState<Devis | null>(null);
  const [duree, setDuree] = useState("");
  const [modalites, setModalites] = useState("");
  const [echeances, setEcheances] = useState<Echeance[]>([]);
  const [message, setMessage] = useState<Message>(null);
  const charger = (r: Devis) => { setV(r); setDuree(String(r.dureeValidite)); setModalites(r.modalites ?? ""); setEcheances(r.echeances.map((e) => ({ label: e.label, pourcentage: fr(e.pourcentage), retenue: e.retenue }))); };
  useEffect(() => { api<Devis>("/parametres/devis").then(charger).catch(() => undefined); }, []);
  if (!v) return null;
  const maj = (i: number, x: Partial<Echeance>) => { setEcheances(echeances.map((e, j) => (j === i ? { ...e, ...x } : e))); setMessage(null); };
  const total = echeances.reduce((s, e) => s + lireNombre(e.pourcentage), 0);
  const enregistrer = async () => {
    try {
      charger(await api<Devis>("/parametres/devis", { method: "PUT", body: JSON.stringify({ dureeValidite: parseInt(duree, 10) || 0, modalites, echeances: echeances.map((e) => ({ label: e.label, pourcentage: lireNombre(e.pourcentage), retenue: e.retenue })) }) }));
      setMessage({ ok: true, texte: "Valeurs enregistrées." });
    } catch (e) { setMessage({ ok: false, texte: e instanceof ApiError ? e.message : "Enregistrement impossible." }); }
  };
  return (
    <div className="mt-5 rounded-xl border border-bordure bg-white p-6">
      <h2 className="text-[15px] font-bold">Devis : valeurs proposées</h2>
      <p className="mt-1 text-[13px] text-secondaire">Reprises à la création de chaque devis, où elles restent modifiables. Les devis déjà créés ne changent pas.</p>
      <div className="mt-4 max-w-xs"><Field label="Durée de validité, en jours"><Input inputMode="numeric" disabled={!v.modifiable} value={duree} onChange={(e) => { setDuree(e.target.value); setMessage(null); }} /></Field></div>
      <div className="mt-4 text-[13px] font-semibold text-secondaire">Modalités de paiement</div>
      <div className="mt-2 space-y-2">
        {echeances.map((e, i) => (
          <div key={i} className="flex flex-wrap items-center gap-3">
            <Input className="h-9 min-w-56 flex-1" disabled={!v.modifiable} aria-label={`Libellé de l'échéance ${i + 1}`} value={e.label} onChange={(x) => maj(i, { label: x.target.value })} />
            <Input className="h-9 w-20 shrink-0 text-right" inputMode="decimal" disabled={!v.modifiable} aria-label={`Pourcentage de l'échéance ${i + 1}`} value={e.pourcentage} onChange={(x) => maj(i, { pourcentage: x.target.value })} />
            <span className="text-[13px] text-secondaire">%</span>
            <label className="flex shrink-0 cursor-pointer items-center gap-2 text-[13px] text-secondaire">
              <input type="checkbox" className="h-4 w-4 accent-marine" disabled={!v.modifiable} checked={e.retenue} onChange={(x) => maj(i, { retenue: x.target.checked })} />Retenue de garantie
            </label>
            {v.modifiable && <Button variant="ghost" size="icon" aria-label={`Supprimer l'échéance ${i + 1}`} onClick={() => setEcheances(echeances.filter((_, j) => j !== i))}><Trash2 size={15} /></Button>}
          </div>
        ))}
      </div>
      <div className={cn("mt-2 text-[12px]", total > 100 ? "font-semibold text-rouge-texte" : "text-attenue")}>Total : {total.toLocaleString("fr-FR")} %{total > 100 ? ", au-delà de 100 %" : ""}</div>
      <div className="mt-4">
        <Field label="Conditions particulières, une par ligne">
          <textarea rows={4} disabled={!v.modifiable} className="w-full rounded-lg border border-bordure-forte bg-white px-3 py-2 text-[14px] focus:border-marine focus:outline-none disabled:bg-fond" value={modalites} onChange={(e) => { setModalites(e.target.value); setMessage(null); }} />
        </Field>
      </div>
      {v.modifiable ? (
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <Button variant="outline" onClick={() => setEcheances([...echeances, { label: "", pourcentage: "", retenue: false }])}><Plus size={15} />Ajouter une échéance</Button>
          <Button onClick={enregistrer}>Enregistrer</Button><Retour message={message} />
        </div>
      ) : <p className="mt-4 text-[13px] text-secondaire">Consultation seule : ces valeurs sont réglées par le directeur, le chef de projet ou le responsable administratif.</p>}
    </div>
  );
}

/** Composition du coulis proposée sur une nouvelle fiche d'injection. */
export function CoulisParam() {
  const [v, setV] = useState<Coulis | null>(null);
  const [f, setF] = useState({ cimentType: "", cimentKg: "", cimentTemperature: "", eau: "", eauLitres: "", adjuvantType: "", adjuvantGrammes: "" });
  const [message, setMessage] = useState<Message>(null);
  const charger = (r: Coulis) => { setV(r); setF({ cimentType: r.cimentType ?? "", cimentKg: fr(r.cimentKg), cimentTemperature: fr(r.cimentTemperature), eau: r.eau ?? "", eauLitres: fr(r.eauLitres), adjuvantType: r.adjuvantType ?? "", adjuvantGrammes: fr(r.adjuvantGrammes) }); };
  useEffect(() => { api<Coulis>("/parametres/coulis").then(charger).catch(() => undefined); }, []);
  if (!v) return null;
  const enregistrer = async () => {
    try {
      charger(await api<Coulis>("/parametres/coulis", { method: "PUT", body: JSON.stringify({ cimentType: f.cimentType, cimentKg: nombre(f.cimentKg), cimentTemperature: nombre(f.cimentTemperature), eau: f.eau, eauLitres: nombre(f.eauLitres), adjuvantType: f.adjuvantType, adjuvantGrammes: nombre(f.adjuvantGrammes) }) }));
      setMessage({ ok: true, texte: "Composition enregistrée." });
    } catch (e) { setMessage({ ok: false, texte: e instanceof ApiError ? e.message : "Enregistrement impossible." }); }
  };
  const champ = (k: keyof typeof f, label: string, decimal?: boolean) => (
    <Field label={label}><Input disabled={!v.modifiable} inputMode={decimal ? "decimal" : "text"} value={f[k]} onChange={(e) => { setF({ ...f, [k]: e.target.value }); setMessage(null); }} /></Field>
  );
  return (
    <div className="mt-5 rounded-xl border border-bordure bg-white p-6">
      <h2 className="text-[15px] font-bold">Injection : composition du coulis</h2>
      <p className="mt-1 text-[13px] text-secondaire">Composition pour une gâchée, proposée sur chaque nouvelle fiche d&apos;injection, où elle reste modifiable.</p>
      <div className="mt-4 grid gap-4 sm:grid-cols-3">
        {champ("cimentType", "Ciment, type")}{champ("cimentKg", "Ciment, kg", true)}{champ("cimentTemperature", "Température, °C", true)}
        {champ("eau", "Eau")}{champ("eauLitres", "Eau, litres", true)}<span />
        {champ("adjuvantType", "Adjuvant, type")}{champ("adjuvantGrammes", "Adjuvant, grammes", true)}
      </div>
      {v.modifiable ? <div className="mt-4 flex items-center gap-3"><Button onClick={enregistrer}>Enregistrer</Button><Retour message={message} /></div>
        : <p className="mt-4 text-[13px] text-secondaire">Consultation seule : cette composition est réglée par le directeur ou le chef de projet.</p>}
    </div>
  );
}

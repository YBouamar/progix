"use client";

import { useCallback, useEffect, useState } from "react";
import { Plus } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn, formatDate, formatDH } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Field, Input, Select } from "@/components/ui/input";
import { SignatureDoc } from "@/components/signature-doc";

type Contrat = { id: string; numero: string; ancienNumero: string | null; devisNumero: string; dateContrat: string; lieu: string; emisPar: string; signePar: string | null; signeLe: string | null; annule: boolean; motifAnnulation: string | null; annulePar: string | null };
type Vue = { refus: string | null; peutAnnuler: boolean; devis: { id: string; numero: string; version: number; netHT: number }[]; contrats: Contrat[] };
const iso = (d: Date) => d.toLocaleDateString("fr-CA");

/** Contrats du projet : établis sur un devis validé, signés avant impression. */
export function ContratsProjet({ projetId }: { projetId: string }) {
  const [v, setV] = useState<Vue | null>(null);
  const [creation, setCreation] = useState(false);
  const [devisId, setDevisId] = useState("");
  const [date, setDate] = useState(iso(new Date()));
  const [lieu, setLieu] = useState("Casablanca");
  const [erreur, setErreur] = useState("");
  const [aAnnuler, setAAnnuler] = useState<string | null>(null);
  const [motif, setMotif] = useState("");

  const charger = useCallback(() => { api<Vue>(`/projets/${projetId}/contrats`).then(setV).catch(() => setV(null)); }, [projetId]);
  useEffect(() => { charger(); }, [charger]);
  if (!v) return null;

  const agir = async (f: () => Promise<unknown>, apres?: () => void) => {
    setErreur("");
    try { await f(); apres?.(); charger(); } catch (e) { setErreur(e instanceof ApiError ? e.message : "Opération impossible."); }
  };
  const etablir = () => agir(() => api(`/projets/${projetId}/contrats`, { method: "POST", body: JSON.stringify({ devisId: devisId || v.devis[0]?.id || null, dateContrat: date, lieu }) }), () => setCreation(false));

  return (
    <section className="mt-6 rounded-xl border border-bordure bg-white">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-bordure px-6 py-4">
        <div>
          <h2 className="font-display text-[15px] font-bold">Contrats</h2>
          <div className="text-[13px] text-secondaire">Établi sur un devis validé, avec la maîtrise d&apos;œuvre et les modalités de paiement du devis.</div>
        </div>
        {!v.refus && !creation && <Button variant="outline" onClick={() => { setCreation(true); setErreur(""); }}><Plus size={16} />Établir un contrat</Button>}
      </div>
      {v.refus && <div className="border-b border-bordure bg-fond px-6 py-3 text-[13px] text-secondaire">{v.refus}</div>}
      {erreur && <div role="alert" className="mx-6 mt-4 rounded-lg bg-rouge-fond px-4 py-3 text-[13px] text-rouge-texte">{erreur}</div>}
      {creation && (
        <div className="border-b border-bordure px-6 py-4">
          <div className="grid gap-3 sm:grid-cols-3">
            <Field label="Devis validé" required>
              <Select value={devisId || v.devis[0]?.id || ""} onChange={(e) => setDevisId(e.target.value)}>
                {v.devis.map((d) => <option key={d.id} value={d.id}>{d.numero} · version {d.version} · {formatDH(d.netHT)} HT</option>)}
              </Select>
            </Field>
            <Field label="Date du contrat" required><Input type="date" value={date} onChange={(e) => setDate(e.target.value)} /></Field>
            <Field label="Fait à"><Input value={lieu} onChange={(e) => setLieu(e.target.value)} /></Field>
          </div>
          <div className="mt-3 flex justify-end gap-2"><Button variant="outline" onClick={() => setCreation(false)}>Fermer</Button><Button disabled={!date} onClick={etablir}>Établir le contrat</Button></div>
        </div>
      )}
      {v.contrats.length === 0 ? <div className="px-6 py-8 text-center text-[13px] text-secondaire">Aucun contrat pour ce projet.</div> : (
        <ul className="divide-y divide-bordure">
          {v.contrats.map((c) => (
            <li key={c.id} className="px-6 py-4 text-[13px]">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div className={cn(c.annule && "text-attenue line-through")}>
                  <span className="font-display font-bold text-marine">{c.numero}</span> · sur le devis {c.devisNumero} · fait à {c.lieu} le <span className="font-display">{formatDate(c.dateContrat)}</span>
                </div>
                {!c.annule && v.peutAnnuler && <button className="cursor-pointer text-[12px] font-semibold text-rouge-texte hover:underline" onClick={() => { setAAnnuler(c.id); setMotif(""); }}>Annuler</button>}
              </div>
              <div className="text-[12px] text-attenue">Établi par {c.emisPar}{c.ancienNumero ? ` · ancien numéro ${c.ancienNumero}` : ""}{c.annule ? ` · annulé par ${c.annulePar} : ${c.motifAnnulation}` : ""}</div>
              {(!c.annule || c.signePar) && <SignatureDoc<Contrat> signePar={c.signePar} signeLe={c.signeLe} quoi="le contrat" url={`/contrats/${c.id}`} onSigne={() => charger()} />}
              {aAnnuler === c.id && (
                <div className="mt-2 flex items-end gap-2">
                  <Field label="Motif de l'annulation" required className="flex-1"><Input autoFocus value={motif} onChange={(e) => setMotif(e.target.value)} /></Field>
                  <Button variant="outline" onClick={() => setAAnnuler(null)}>Fermer</Button>
                  <Button disabled={!motif.trim()} onClick={() => agir(() => api(`/contrats/${c.id}/annuler`, { method: "POST", body: JSON.stringify({ motif }) }), () => setAAnnuler(null))}>Confirmer</Button>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

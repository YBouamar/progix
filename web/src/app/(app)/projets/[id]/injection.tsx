"use client";

import { useEffect, useState } from "react";
import { Plus, Trash2 } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn, formatDateHeure, lireNombre } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/input";
import { Signature } from "@/components/signature";

type CableVue = { cable: string; systeme?: string | null; sens?: string | null; testAir: boolean; fluidite: number | null; temperature: number | null; pression: number | null; depart: string | null; fin: string | null };
type Vue = {
  modifiable: boolean; verrou: string | null;
  airAmbiant: number | null; malaxeur: string | null; pompe: string | null;
  cimentType: string | null; cimentKg: number | null; cimentTemperature: number | null;
  eau: string | null; eauLitres: number | null; adjuvantType: string | null; adjuvantGrammes: number | null;
  clientNom: string | null; clientSignature: string | null; signePar: string | null; signeLe: string | null; cables: CableVue[];
};
type Cable = { cable: string; testAir: boolean; fluidite: string; temperature: string; pression: string; depart: string; fin: string };
type Form = Record<"airAmbiant" | "malaxeur" | "pompe" | "cimentType" | "cimentKg" | "cimentTemperature" | "eau" | "eauLitres" | "adjuvantType" | "adjuvantGrammes" | "clientNom", string>
  & { clientSignature: string | null; cables: Cable[] };

const fr = (n: number | null) => (n === null || n === undefined ? "" : String(n).replace(".", ","));
const num = (s: string) => (s.trim() === "" ? null : lireNombre(s));
const CELL = "h-8 w-full min-w-0 rounded-md border border-bordure-forte bg-white px-2 text-right font-display text-[13px] focus:border-marine focus:outline-none disabled:border-transparent disabled:bg-transparent";

function versForm(v: Vue): Form {
  return {
    airAmbiant: fr(v.airAmbiant), malaxeur: v.malaxeur ?? "", pompe: v.pompe ?? "",
    cimentType: v.cimentType ?? "", cimentKg: fr(v.cimentKg), cimentTemperature: fr(v.cimentTemperature),
    eau: v.eau ?? "", eauLitres: fr(v.eauLitres), adjuvantType: v.adjuvantType ?? "", adjuvantGrammes: fr(v.adjuvantGrammes),
    clientNom: v.clientNom ?? "", clientSignature: v.clientSignature,
    cables: v.cables.map((c) => ({ cable: c.cable, testAir: c.testAir, fluidite: fr(c.fluidite), temperature: fr(c.temperature), pression: fr(c.pression), depart: c.depart ?? "", fin: c.fin ?? "" })),
  };
}

/** Rapport d'injection, enregistrement ENR-PT-04. */
export function FicheInjection({ projetId, dalleId }: { projetId: string; dalleId: string }) {
  const [vue, setVue] = useState<Vue | null>(null);
  const [f, setF] = useState<Form | null>(null);
  const [message, setMessage] = useState<{ ok: boolean; texte: string } | null>(null);
  const [modifie, setModifie] = useState(false);
  const base = `/projets/${projetId}/dalles/${dalleId}/injection`;

  const charger = (r: Vue) => { setVue(r); setF(versForm(r)); setModifie(false); };
  useEffect(() => { api<Vue>(base).then(charger).catch(() => undefined); }, [base]);

  if (!vue || !f) return null;
  // Système et orientation de chaque tendon, tels que définis dans la commande usine.
  const reperes: Record<string, string> = {};
  for (const c of vue.cables) if (c.systeme) reperes[c.cable.trim().toUpperCase()] = `${c.systeme} · ${c.sens}`;
  const lecture = !vue.modifiable;
  const maj = (patch: Partial<Form>) => { setF({ ...f, ...patch }); setModifie(true); setMessage(null); };
  const majCable = (i: number, patch: Partial<Cable>) => maj({ cables: f.cables.map((c, j) => (j === i ? { ...c, ...patch } : c)) });
  const champ = (k: keyof Omit<Form, "cables" | "clientSignature">, label: string, extra?: React.InputHTMLAttributes<HTMLInputElement>) => (
    <Field label={label}><Input className="h-9" disabled={lecture} value={f[k]} onChange={(e) => maj({ [k]: e.target.value } as Partial<Form>)} {...extra} /></Field>
  );

  async function enregistrer() {
    if (!f) return;
    setMessage(null);
    try {
      charger(await api<Vue>(base, {
        method: "PUT",
        body: JSON.stringify({
          airAmbiant: num(f.airAmbiant), malaxeur: f.malaxeur, pompe: f.pompe, cimentType: f.cimentType, cimentKg: num(f.cimentKg),
          cimentTemperature: num(f.cimentTemperature), eau: f.eau, eauLitres: num(f.eauLitres), adjuvantType: f.adjuvantType,
          adjuvantGrammes: num(f.adjuvantGrammes), clientNom: f.clientNom, clientSignature: f.clientSignature,
          cables: f.cables.map((c) => ({ cable: c.cable, testAir: c.testAir, fluidite: num(c.fluidite), temperature: num(c.temperature), pression: num(c.pression), depart: c.depart, fin: c.fin })),
        }),
      }));
      setMessage({ ok: true, texte: "Fiche enregistrée." });
    } catch (e) {
      setMessage({ ok: false, texte: e instanceof ApiError ? (e.status === 403 ? "Votre rôle ne permet pas de saisir la fiche." : e.message) : "Enregistrement impossible." });
    }
  }

  if (lecture && f.cables.length === 0 && !vue.clientNom) {
    return <div className="mt-2 text-[13px] text-secondaire">{vue.verrou ?? "Aucune fiche d'injection saisie."}</div>;
  }

  return (
    <div className="mt-3 rounded-lg border border-bordure bg-white p-3">
      <div className="text-[13px] font-bold">Rapport d&apos;injection, enregistrement ENR-PT-04</div>

      <div className="mt-3 grid grid-cols-3 gap-3">
        {champ("airAmbiant", "Air ambiant, °C", { inputMode: "decimal" })}
        {champ("malaxeur", "Malaxeur")}
        {champ("pompe", "Pompe")}
      </div>

      <div className="mt-4 text-[12px] font-bold uppercase tracking-wide text-secondaire">Composition du coulis pour une gâchée</div>
      <div className="mt-2 grid grid-cols-3 gap-3">
        {champ("cimentType", "Ciment, type")}
        {champ("cimentKg", "Ciment, kg", { inputMode: "decimal" })}
        {champ("cimentTemperature", "Ciment, température °C", { inputMode: "decimal" })}
        {champ("eau", "Eau")}
        {champ("eauLitres", "Eau, litres", { inputMode: "decimal" })}
        <span />
        {champ("adjuvantType", "Adjuvant n° 1, type")}
        {champ("adjuvantGrammes", "Adjuvant n° 1, grammes", { inputMode: "decimal" })}
      </div>

      <div className="mt-4 text-[12px] font-bold uppercase tracking-wide text-secondaire">Mesures par câble</div>
      <div className="mt-1 overflow-x-auto">
        <table className="w-full text-left text-[13px]">
          <thead className="text-[11px] uppercase tracking-wide text-secondaire">
            <tr>
              <th className="py-1 pr-2 font-semibold">N° du tendon</th>
              <th className="px-1 py-1 font-semibold">Syst.</th>
              <th className="px-1 py-1 text-center font-semibold">Test à l&apos;air</th>
              <th className="px-1 py-1 text-right font-semibold">Fluidité, s</th>
              <th className="px-1 py-1 text-right font-semibold">Temp. coulis, °C</th>
              <th className="px-1 py-1 text-right font-semibold">Pression, bar</th>
              <th className="px-1 py-1 font-semibold">Départ</th>
              <th className="px-1 py-1 font-semibold">Fin</th>
              {!lecture && <th />}
            </tr>
          </thead>
          <tbody>
            {f.cables.map((c, i) => (
              <tr key={i} className="border-t border-bordure">
                <td className="py-1 pr-2"><input className={cn(CELL, "w-16 min-w-16 text-left font-bold text-marine")} disabled={lecture} aria-label={`Câble, ligne ${i + 1}`} value={c.cable} onChange={(e) => majCable(i, { cable: e.target.value })} /></td>
                <td className="whitespace-nowrap px-1 py-1 text-[12px] text-secondaire">{reperes[c.cable.trim().toUpperCase()] ?? ""}</td>
                <td className="px-1 py-1 text-center">
                  <label className="inline-flex items-center gap-1.5">
                    <input type="checkbox" className="h-4 w-4 accent-marine" disabled={lecture} aria-label={`Test à l'air, ${c.cable}`} checked={c.testAir} onChange={(e) => majCable(i, { testAir: e.target.checked })} />
                    <span className={c.testAir ? "font-semibold text-vert-texte" : "text-attenue"}>OK</span>
                  </label>
                </td>
                <td className="w-16 px-1 py-1"><input className={CELL} disabled={lecture} inputMode="decimal" aria-label={`Fluidité, ${c.cable}`} value={c.fluidite} onChange={(e) => majCable(i, { fluidite: e.target.value })} /></td>
                <td className="w-16 px-1 py-1"><input className={CELL} disabled={lecture} inputMode="decimal" aria-label={`Température du coulis, ${c.cable}`} value={c.temperature} onChange={(e) => majCable(i, { temperature: e.target.value })} /></td>
                <td className="w-16 px-1 py-1"><input className={CELL} disabled={lecture} inputMode="decimal" aria-label={`Pression, ${c.cable}`} value={c.pression} onChange={(e) => majCable(i, { pression: e.target.value })} /></td>
                <td className="px-1 py-1"><input type="time" className={cn(CELL, "w-28 min-w-28 text-left")} disabled={lecture} aria-label={`Départ, ${c.cable}`} value={c.depart} onChange={(e) => majCable(i, { depart: e.target.value })} /></td>
                <td className="px-1 py-1"><input type="time" className={cn(CELL, "w-28 min-w-28 text-left")} disabled={lecture} aria-label={`Fin, ${c.cable}`} value={c.fin} onChange={(e) => majCable(i, { fin: e.target.value })} /></td>
                {!lecture && <td><Button variant="ghost" size="icon" aria-label={`Retirer ${c.cable}`} onClick={() => maj({ cables: f.cables.filter((_, j) => j !== i) })}><Trash2 size={14} /></Button></td>}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {!lecture && (
        <Button size="sm" variant="outline" className="mt-2"
          onClick={() => maj({ cables: [...f.cables, { cable: "", testAir: false, fluidite: "", temperature: "", pression: "", depart: "", fin: "" }] })}>
          <Plus size={14} />Ajouter un câble
        </Button>
      )}

      <div className="mt-4 grid gap-4 sm:grid-cols-2">
        <div>
          <div className="text-[12px] font-bold uppercase tracking-wide text-secondaire">Exécution</div>
          <p className="mt-1 text-[13px]">
            {vue.signePar ? `Signée par ${vue.signePar} le ${formatDateHeure(vue.signeLe)}.` : "L'exécutant signe avec son mot de passe à la validation de l'étape."}
          </p>
        </div>
        <div>
          <div className="mb-1 text-[12px] font-bold uppercase tracking-wide text-secondaire">Entreprise cliente</div>
          <Input className="mb-2 h-9" disabled={lecture} placeholder="Nom du signataire" aria-label="Nom du signataire du client" value={f.clientNom} onChange={(e) => maj({ clientNom: e.target.value })} />
          <Signature valeur={f.clientSignature} disabled={lecture} onChange={(v) => maj({ clientSignature: v })} />
        </div>
      </div>

      {!lecture && (
        <div className="mt-3 flex items-center gap-3 border-t border-bordure pt-3">
          <Button size="sm" disabled={!modifie} onClick={enregistrer}>Enregistrer la fiche</Button>
          {message && <span role={message.ok ? "status" : "alert"} className={cn("text-[13px]", message.ok ? "text-vert-texte" : "text-rouge-texte")}>{message.texte}</span>}
          {modifie && !message && <span className="text-[13px] text-secondaire">Modifications non enregistrées</span>}
        </div>
      )}
    </div>
  );
}

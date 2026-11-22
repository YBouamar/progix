"use client";

import { useCallback, useEffect, useState } from "react";
import { CircleAlert } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn, filtrerDecimal, formatNombre, formatSurface, lireNombre } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/modal";

export type ApercuCablage = {
  version: string; importable: boolean; erreurs: string[]; avertissements: string[];
  plans: { nom: string; sens: string; cables: number; reference: string | null; niveau: string | null; surface: number | null }[];
  lignes: { numero: string; torons: number; sens: string; tracee: number; actives: number; surlongueur: number; longueur: number; ancrages: number }[];
  recap: { sens: string; torons: number; cables: number; longueur: number }[];
  surfaceDxf: number | null; surfaceDalle: number; surfaceDifferente: boolean;
};

/**
 * Commande tirée des deux plans de câblage DXF déposés par le dessinateur dans le dossier de plans :
 * aperçu des tendons, recoupement avec le PTTotal, surlongueur, puis import dans la commande (rien n'est enregistré avant « Enregistrer »).
 */
export function ImportCablage({ open, onOpenChange, projetId, dalleId, dejaNumeros, surfaceCommande, onImporter }: {
  open: boolean; onOpenChange: (o: boolean) => void; projetId: string; dalleId: string; dejaNumeros: string[]; surfaceCommande: string;
  onImporter: (a: ApercuCablage, mode: "remplacer" | "completer", surfaceDalleChangee: boolean) => void;
}) {
  const [surlongueur, setSurlongueur] = useState("0");
  const [apercu, setApercu] = useState<ApercuCablage | null>(null);
  const [erreur, setErreur] = useState<string | null>(null);
  const [charge, setCharge] = useState(false);
  const [mode, setMode] = useState<"remplacer" | "completer">("remplacer");
  const [modifierSurface, setModifierSurface] = useState(false);
  const racine = `/projets/${projetId}/dalles/${dalleId}/commandes`;

  const lire = useCallback(async (cm: string) => {
    setCharge(true); setErreur(null);
    try { setApercu(await api<ApercuCablage>(`${racine}/cablage/apercu?surlongueur=${lireNombre(cm) || 0}`)); }
    catch (e) { setApercu(null); setErreur(e instanceof ApiError ? e.message : "Lecture des plans impossible."); }
    finally { setCharge(false); }
  }, [racine]);

  useEffect(() => { if (open) { setModifierSurface(false); lire("0"); setSurlongueur("0"); } }, [open, lire]);

  const doublons = apercu && mode === "completer" ? apercu.lignes.filter((l) => dejaNumeros.includes(l.numero)).length : 0;

  async function importer() {
    if (!apercu) return;
    let changee = false;
    if (modifierSurface && apercu.surfaceDxf) {
      try { await api(`${racine}/surface-dalle`, { method: "PUT", body: JSON.stringify({ surface: apercu.surfaceDxf }) }); changee = true; }
      catch (e) { setErreur(e instanceof ApiError ? e.message : "La surface de la dalle n'a pas pu être modifiée."); return; }
    }
    onImporter(apercu, mode, changee);
  }

  return (
    <Modal open={open} onOpenChange={onOpenChange} large title="Importer depuis les plans ADAPT (DXF)"
      description="Les tendons sont lus dans les plans de câblage X et Y déposés par le dessinateur dans le dossier de plans."
      footer={<>
        <Button variant="outline" onClick={() => onOpenChange(false)}>Annuler</Button>
        <Button disabled={!apercu?.importable || charge} onClick={importer}>Importer {apercu?.importable ? `${apercu.lignes.length} tendons` : ""}</Button>
      </>}>
      <div className="space-y-3 text-[13px]">
        <div className="flex flex-wrap items-end gap-2">
          <div>
            <label className="block font-semibold" htmlFor="surlongueur">Surlongueur par extrémité active (cm)</label>
            <input id="surlongueur" className="mt-1 h-8 w-28 rounded-md border border-bordure-forte px-2 text-right font-display" inputMode="decimal" value={surlongueur}
              onChange={(e) => setSurlongueur(filtrerDecimal(e.target.value))} />
          </div>
          <Button size="sm" variant="outline" disabled={charge} onClick={() => lire(surlongueur)}>Recalculer</Button>
          <span className="text-secondaire">Ajoutée à chaque extrémité active du câble (PTTotal), 0 par défaut.</span>
        </div>

        {erreur && <div role="alert" className="rounded-md bg-rouge-fond px-3 py-2 text-rouge-texte">{erreur}</div>}
        {charge && <div className="text-secondaire">Lecture des plans en cours...</div>}

        {apercu && (
          <>
            <div className="text-secondaire">
              Dossier de plans {apercu.version} : {apercu.plans.map((p) => `${p.nom} (${p.sens}, ${p.cables} câbles${p.niveau ? `, ${p.niveau}` : ""})`).join(" ; ") || "aucun plan DXF déposé"}.
            </div>
            {apercu.erreurs.length > 0 && (
              <ul role="alert" className="list-disc rounded-md bg-rouge-fond py-2 pl-7 pr-3 text-rouge-texte">{apercu.erreurs.map((e, k) => <li key={k}>{e}</li>)}</ul>
            )}
            {apercu.avertissements.length > 0 && (
              <ul className="list-disc rounded-md bg-orange-fond py-2 pl-7 pr-3 text-orange-texte">{apercu.avertissements.map((e, k) => <li key={k}>{e}</li>)}</ul>
            )}

            {apercu.recap.length > 0 && (
              <table className="w-full text-left">
                <thead className="text-[11px] text-secondaire"><tr><th className="py-1 font-semibold">Orientation</th><th className="font-semibold">Système</th><th className="text-right font-semibold">Tendons</th><th className="text-right font-semibold">Longueur commandée</th></tr></thead>
                <tbody>{apercu.recap.map((r) => (
                  <tr key={`${r.sens}${r.torons}`} className="border-t border-bordure"><td className="py-1">{r.sens}</td><td>S{r.torons}</td><td className="text-right font-display">{r.cables}</td><td className="text-right font-display">{formatNombre(r.longueur)} m</td></tr>
                ))}</tbody>
              </table>
            )}

            {apercu.lignes.length > 0 && (
              <details>
                <summary className="cursor-pointer font-semibold">Voir les {apercu.lignes.length} tendons</summary>
                <div className="mt-2 max-h-56 overflow-auto rounded-md border border-bordure">
                  <table className="w-full text-left"><thead className="sticky top-0 bg-fond text-[11px] text-secondaire"><tr>
                    <th className="px-2 py-1 font-semibold">N°</th><th className="font-semibold">Système</th><th className="font-semibold">Sens</th><th className="text-right font-semibold">Tracée</th>
                    <th className="text-right font-semibold">Surlongueur</th><th className="text-right font-semibold">Commandée</th><th className="px-2 text-right font-semibold">Ancrages</th></tr></thead>
                  <tbody>{apercu.lignes.map((l) => (
                    <tr key={l.numero} className="border-t border-bordure"><td className="px-2 py-1 font-bold text-marine">{l.numero}</td><td>S{l.torons}</td><td>{l.sens}</td>
                      <td className="text-right font-display">{formatNombre(l.tracee)}</td><td className="text-right font-display">{formatNombre(l.surlongueur)}</td>
                      <td className="text-right font-display">{formatNombre(l.longueur)}</td><td className="px-2 text-right font-display">{l.ancrages}</td></tr>
                  ))}</tbody></table>
                </div>
              </details>
            )}

            {apercu.surfaceDxf != null && (
              <div className={cn("rounded-md px-3 py-2", apercu.surfaceDifferente ? "bg-orange-fond" : "bg-fond")}>
                Surface lue sur les plans : <b className="font-display">{formatSurface(apercu.surfaceDxf)}</b>. Surface de la dalle : <b className="font-display">{formatSurface(apercu.surfaceDalle)}</b>.
                {apercu.surfaceDifferente ? (
                  <label className="mt-1 flex items-start gap-2 text-orange-texte">
                    <input type="checkbox" className="mt-0.5" checked={modifierSurface} onChange={(e) => setModifierSurface(e.target.checked)} />
                    <span><CircleAlert size={14} className="mr-1 inline" aria-hidden />Les deux surfaces diffèrent. Modifier la surface de la dalle pour reprendre celle des plans ? Sans cela, la surface de la dalle reste celle utilisée pour les statistiques et la facturation.</span>
                  </label>
                ) : <span className="text-secondaire"> Elles sont identiques.</span>}
                {!surfaceCommande && <div className="text-secondaire">La surface de la commande sera préremplie avec celle des plans.</div>}
              </div>
            )}

            {dejaNumeros.length > 0 && (
              <fieldset className="rounded-md border border-bordure px-3 py-2">
                <legend className="px-1 font-semibold">La commande contient déjà des tendons</legend>
                <label className="flex items-center gap-2"><input type="radio" name="mode" checked={mode === "remplacer"} onChange={() => setMode("remplacer")} />Remplacer les tendons de la commande</label>
                <label className="flex items-center gap-2"><input type="radio" name="mode" checked={mode === "completer"} onChange={() => setMode("completer")} />Ajouter seulement les tendons absents</label>
                {doublons > 0 && <div className="mt-1 text-orange-texte">{doublons} tendon{doublons > 1 ? "s" : ""} déjà présent{doublons > 1 ? "s" : ""} ne sera{doublons > 1 ? "ont" : ""} pas repris.</div>}
              </fieldset>
            )}
          </>
        )}
      </div>
    </Modal>
  );
}

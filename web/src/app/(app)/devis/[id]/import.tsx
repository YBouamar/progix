"use client";

import { useRef, useState } from "react";
import { FileDown, Upload } from "lucide-react";
import { cn, formatSurface } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Sheet } from "@/components/ui/sheet";

export type LigneImport = {
  ligne: number; id: string | null; reference: string; niveau: string; surface: number | null; ratioActif: number | null; ratioPassif: number | null;
  systemes: { torons: number; ancrages: number; gaine: number }[]; valide: boolean; message: string;
};
type Resultat = { fichier: string; colonnesIgnorees: string[]; lignes: LigneImport[] };

/** Import Excel des dalles d'un devis : aperçu ligne par ligne, seules les lignes valides sont reportées dans le brouillon. */
export function ImportDevis({ devisId, onClose, onImporter }: { devisId: string; onClose: () => void; onImporter: (lignes: LigneImport[]) => void }) {
  const [r, setR] = useState<Resultat | null>(null);
  const [erreur, setErreur] = useState("");
  const [envoi, setEnvoi] = useState(false);
  const champ = useRef<HTMLInputElement>(null);

  const lire = async (file: File) => {
    setEnvoi(true); setErreur(""); setR(null);
    const corps = new FormData(); corps.append("fichier", file);
    try {
      const res = await fetch(`/api/devis/${devisId}/import`, { method: "POST", body: corps, credentials: "include" });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) throw new Error(data.message ?? "Lecture du fichier impossible.");
      setR(data as Resultat);
    } catch (e) { setErreur(e instanceof Error ? e.message : "Lecture du fichier impossible."); }
    finally { setEnvoi(false); }
  };
  const valides = r?.lignes.filter((l) => l.valide) ?? [];
  const nb = (v: number | null) => (v === null ? "" : v.toLocaleString("fr-FR", { maximumFractionDigits: 2 }));

  return (
    <Sheet open onOpenChange={(o) => !o && onClose()} title="Importer les dalles depuis Excel"
      description="Une ligne par dalle, reconnue par sa référence. Les colonnes des types d'ancrage sont reconnues par leur nom : S4 ancrages, S4 gaine (S1, S2, S3, S4, S7, S9, S12, S15).">
      <div className="flex flex-wrap items-center gap-2">
        <a href={`/api/devis/${devisId}/import/modele`} className="inline-flex items-center gap-2 rounded-lg border border-bordure-forte bg-white px-3.5 py-2 text-[13px] font-semibold hover:bg-fond"><FileDown size={15} />Télécharger le modèle Excel</a>
        <input ref={champ} type="file" accept=".xlsx" className="hidden" onChange={(e) => { const f = e.target.files?.[0]; if (f) lire(f); e.target.value = ""; }} />
        <Button variant="outline" disabled={envoi} onClick={() => champ.current?.click()}><Upload size={15} />{r ? "Changer de fichier" : "Choisir un fichier"}</Button>
      </div>
      <p className="mt-2 text-[12px] text-attenue">Le modèle est prérempli avec les dalles de ce devis. La surface vient de la dalle et ne se modifie pas par l&apos;import.</p>
      {erreur && <div role="alert" className="mt-3 rounded-lg bg-rouge-fond px-4 py-3 text-[13px] text-rouge-texte">{erreur}</div>}
      {r && (
        <>
          <div className="mt-4 text-[13px]"><span className="font-semibold">{r.fichier}</span> · {r.lignes.length} ligne(s) lue(s) · {valides.length} valide(s) · {r.lignes.length - valides.length} à corriger</div>
          {r.colonnesIgnorees.length > 0 && <div className="mt-1 text-[12px] text-orange-texte">Colonnes non reconnues, ignorées : {r.colonnesIgnorees.join(", ")}.</div>}
          <div className="mt-3 overflow-x-auto rounded-lg border border-bordure">
            <table className="w-full text-left text-[13px]">
              <thead className="border-b border-bordure bg-fond text-[11px] uppercase tracking-wide text-secondaire">
                <tr><th className="px-3 py-2 font-semibold">Ligne</th><th className="px-3 py-2 font-semibold">Dalle</th><th className="px-3 py-2 text-right font-semibold">Surface</th><th className="px-3 py-2 font-semibold">Systèmes</th><th className="px-3 py-2 text-right font-semibold">Ratio actif</th><th className="px-3 py-2 font-semibold">Contrôle</th></tr>
              </thead>
              <tbody>
                {r.lignes.map((l) => (
                  <tr key={l.ligne} className={cn("border-b border-bordure last:border-0", !l.valide && "bg-rouge-fond/40")}>
                    <td className="px-3 py-2 font-display">{l.ligne}</td>
                    <td className="px-3 py-2"><div className="font-display font-bold">{l.reference || "?"}</div><div className="text-[12px] text-secondaire">{l.niveau}</div></td>
                    <td className="whitespace-nowrap px-3 py-2 text-right font-display">{l.surface === null ? "" : formatSurface(l.surface)}</td>
                    <td className="px-3 py-2">{l.systemes.map((s) => `S${s.torons}`).join(", ")}</td>
                    <td className="px-3 py-2 text-right font-display">{nb(l.ratioActif)}</td>
                    <td className="px-3 py-2"><Badge ton={l.valide ? "vert" : "rouge"}>{l.valide ? "Valide" : "Erreur"}</Badge>{l.message && <div className="mt-1 text-[12px] text-rouge-texte">{l.message}</div>}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="mt-2 text-[12px] text-attenue">Les lignes en erreur ne sont pas importées. Corrigez le fichier puis rechargez-le, ou saisissez ces dalles à la main dans la grille. L&apos;import remplit le brouillon ; il reste à l&apos;enregistrer.</p>
        </>
      )}
      <div className="mt-4 flex justify-end gap-2">
        <Button variant="outline" onClick={onClose}>Annuler</Button>
        <Button disabled={valides.length === 0} onClick={() => onImporter(valides)}>Importer les {valides.length} ligne(s) valide(s)</Button>
      </div>
    </Sheet>
  );
}

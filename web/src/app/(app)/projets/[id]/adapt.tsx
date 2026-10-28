"use client";

import { useRef, useState } from "react";
import { Upload } from "lucide-react";
import { formatDateHeure, formatNombre, formatSurface } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Sheet } from "@/components/ui/sheet";

export type SystemeAdapt = { torons: number; ancrages: number; gaine: number; longueurGaines: number };
export type Adapt = {
  ratioActif: number; ratioPassif: number; surfacePlancher: number; plancher: string | null;
  fichierPt: string; fichierRebar: string; importeLe: string; importePar: string; systemes: SystemeAdapt[];
};
type DalleAdapt = { id: string; reference: string; surface: number; adapt: Adapt | null };

/** Import des deux exports ADAPT d'une dalle (PTTotal et RebarTotal) et affichage des données lues. */
export function AdaptDalle<T extends DalleAdapt>({ projetId, dalle, peutImporter, onClose, onImporte }: {
  projetId: string; dalle: T; peutImporter: boolean; onClose: () => void; onImporte: (d: T) => void;
}) {
  const [fichiers, setFichiers] = useState<File[]>([]);
  const [erreur, setErreur] = useState("");
  const [avertissements, setAvertissements] = useState<string[]>([]);
  const [importe, setImporte] = useState(false);
  const [envoi, setEnvoi] = useState(false);
  const champ = useRef<HTMLInputElement>(null);
  const a = dalle.adapt;

  async function importer() {
    setEnvoi(true); setErreur(""); setAvertissements([]); setImporte(false);
    const corps = new FormData();
    fichiers.forEach((f) => corps.append("fichiers", f));
    try {
      const res = await fetch(`/api/projets/${projetId}/dalles/${dalle.id}/adapt`, { method: "POST", body: corps, credentials: "include" });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) throw new Error(res.status === 403 ? "Votre rôle ne permet pas d'importer les fichiers ADAPT." : data.message ?? "Lecture des fichiers impossible.");
      setAvertissements(data.avertissements ?? []);
      setFichiers([]);
      setImporte(true);
      onImporte(data.dalle as T);
    } catch (e) { setErreur(e instanceof Error ? e.message : "Lecture des fichiers impossible."); }
    finally { setEnvoi(false); }
  }

  return (
    <Sheet open onOpenChange={(o) => !o && onClose()} title="Fichiers ADAPT" description={dalle.reference}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>Fermer</Button>
          {peutImporter && (
            <Button disabled={envoi || fichiers.length !== 2} onClick={importer}
              title={fichiers.length !== 2 ? "Choisissez d'abord les deux fichiers ADAPT (PTTotal et RebarTotal)" : undefined}>
              {envoi ? "Lecture..." : a ? "Remplacer les données" : "Importer"}
            </Button>
          )}
        </>
      }>
      {erreur && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}
      {importe && (
        <div role="status" className="mb-4 rounded-lg bg-vert-fond px-4 py-3 text-vert-texte">
          Fichiers importés : les données ci-dessous sont enregistrées sur la dalle. Pour les remplacer, choisissez deux nouveaux fichiers.
        </div>
      )}
      {avertissements.length > 0 && (
        <div role="status" className="mb-4 space-y-1 rounded-lg bg-orange-fond px-4 py-3 text-orange-texte">
          {avertissements.map((m) => <p key={m}>{m}</p>)}
        </div>
      )}

      {peutImporter && (
        <div className="rounded-lg border border-dashed border-bordure p-4">
          <p className="text-[13px] text-secondaire">
            Déposez ensemble les deux exports ADAPT-Floor Pro de cette dalle : <strong>PTTotal</strong> (câbles, ratio actif) et <strong>RebarTotal</strong> (ratio passif), au format .rtf ou .rtf.doc.
          </p>
          <input ref={champ} type="file" multiple accept=".doc,.rtf" className="hidden"
            onChange={(e) => {
              // Les deux fichiers se choisissent ensemble ou l'un après l'autre : un fichier seul s'ajoute au précédent.
              const choisis = Array.from(e.target.files ?? []);
              setFichiers((prec) => (choisis.length >= 2 ? choisis : [...prec.filter((p) => p.name !== choisis[0]?.name), ...choisis].slice(-2)));
              setErreur(""); setImporte(false); e.target.value = "";
            }} />
          <Button className="mt-3" variant="outline" size="sm" disabled={envoi} onClick={() => champ.current?.click()}>
            <Upload size={14} />{fichiers.length === 1 ? "Ajouter le second fichier" : fichiers.length ? "Changer les fichiers" : "Choisir les deux fichiers"}
          </Button>
          {fichiers.length > 0 && (
            <ul className="mt-2 text-[13px]">
              {fichiers.map((f) => <li key={f.name}>{f.name}</li>)}
              {fichiers.length === 1 && <li className="text-orange-texte">Ajoutez le second fichier.</li>}
              {fichiers.length > 2 && <li className="text-rouge-texte">Sélectionnez exactement deux fichiers.</li>}
            </ul>
          )}
        </div>
      )}

      {a ? (
        <div className="mt-5">
          <h4 className="text-[14px] font-bold">Données importées</h4>
          {a.plancher && <p className="mt-1 text-[13px] text-secondaire">{a.plancher}</p>}
          <dl className="mt-3 grid grid-cols-3 gap-3 text-[13px]">
            <div className="rounded-lg bg-fond px-3 py-2"><dt className="text-secondaire">Ratio actif</dt><dd className="font-display font-bold">{formatNombre(a.ratioActif)} kg/m²</dd></div>
            <div className="rounded-lg bg-fond px-3 py-2"><dt className="text-secondaire">Ratio passif</dt><dd className="font-display font-bold">{formatNombre(a.ratioPassif)} kg/m²</dd></div>
            <div className="rounded-lg bg-fond px-3 py-2"><dt className="text-secondaire">Plancher ADAPT</dt><dd className="font-display font-bold">{formatSurface(a.surfacePlancher)}</dd></div>
          </dl>
          {Math.round(a.surfacePlancher * 100) !== Math.round(dalle.surface * 100) && (
            <p className="mt-2 text-[13px] text-orange-texte">La surface de la dalle ({formatSurface(dalle.surface)}) diffère de celle du plancher ADAPT.</p>
          )}
          <table className="mt-4 w-full text-left text-[13px]">
            <thead className="border-b border-bordure text-[12px] uppercase tracking-wide text-secondaire">
              <tr>
                <th className="py-2 font-semibold">Type d&apos;ancrage</th>
                <th className="py-2 text-right font-semibold">Ancrages</th>
                <th className="py-2 text-right font-semibold">Gaine (Strand Length)</th>
                <th className="py-2 text-right font-semibold">Tendon/Duct Length</th>
              </tr>
            </thead>
            <tbody>
              {a.systemes.map((s) => (
                <tr key={s.torons} className="border-b border-bordure">
                  <td className="py-2 font-semibold">S{s.torons}</td>
                  <td className="py-2 text-right font-display">{s.ancrages}</td>
                  <td className="py-2 text-right font-display">{formatNombre(s.gaine)} m</td>
                  <td className="py-2 text-right font-display text-secondaire">{formatNombre(s.longueurGaines)} m</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className="mt-3 text-[12px] text-attenue">
            {a.fichierPt} · {a.fichierRebar} · importés le {formatDateHeure(a.importeLe)} par {a.importePar}.
            Un nouveau devis reprend ces données ; un brouillon les reprend par « Reprendre les données ADAPT ».
          </p>
        </div>
      ) : (
        <p className="mt-5 text-secondaire">Aucun fichier ADAPT importé pour cette dalle.</p>
      )}
    </Sheet>
  );
}

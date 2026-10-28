"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { FileDown, Plus, Trash2, Upload } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn, filtrerDecimal, formatNombre, formatSurface, lireNombre } from "@/lib/utils";
import { Button } from "@/components/ui/button";

type TendonVue = { numero: string; torons: number; sens: string; longueur: number; ancrages: number };
type Vue = {
  modifiable: boolean; verrou: string | null; tendons: TendonVue[]; typesAncrage: number[]; surface: number | null; surfaceDalle: number;
  accessoires: { libelle: string; unite: string | null; quantite: number }[]; proposes: { libelle: string; unite: string | null }[];
};
type Ligne = { numero: string; torons: string; sens: string; longueur: string; ancrages: string };
type Accessoire = { libelle: string; unite: string | null; quantite: string };

const versLigne = (t: TendonVue): Ligne => ({ numero: t.numero, torons: String(t.torons), sens: t.sens, longueur: String(t.longueur).replace(".", ","), ancrages: String(t.ancrages) });
const TRAMES: [string, string][] = [["xlsx", "Excel"], ["csv", "CSV"], ["json", "JSON"]];

const CELL = "h-8 w-full min-w-0 rounded-md border border-bordure-forte bg-white px-2 font-display text-[13px] focus:border-marine focus:outline-none disabled:border-transparent disabled:bg-transparent";
const ent = (s: string) => parseInt(s, 10) || 0;

/** Commande usine : un tendon par ligne (numéro, système, orientation, longueur, ancrages), saisi ou importé d'un fichier, et les accessoires. */
export function CommandeUsine({ projetId, dalleId }: { projetId: string; dalleId: string }) {
  const [vue, setVue] = useState<Vue | null>(null);
  const [lignes, setLignes] = useState<Ligne[]>([]);
  const [accessoires, setAccessoires] = useState<Accessoire[]>([]);
  const [surface, setSurface] = useState("");
  const [message, setMessage] = useState<{ ok: boolean; texte: string } | null>(null);
  const [modifie, setModifie] = useState(false);
  const [importe, setImporte] = useState<{ nb: number; erreurs: string[] } | null>(null);
  const fichier = useRef<HTMLInputElement>(null);
  const base = `/projets/${projetId}/dalles/${dalleId}/commande`;

  const charger = (r: Vue) => {
    setVue(r);
    setLignes(r.tendons.map(versLigne));
    setSurface(r.surface ? String(r.surface).replace(".", ",") : "");
    // Tous les accessoires paramétrés sont proposés ; ceux de la commande gardent leur quantité.
    const saisis = r.accessoires.map((a) => ({ libelle: a.libelle, unite: a.unite, quantite: String(a.quantite) }));
    setAccessoires([...saisis, ...r.proposes.filter((p) => !r.accessoires.some((a) => a.libelle === p.libelle)).map((p) => ({ libelle: p.libelle, unite: p.unite, quantite: "" }))]);
    setModifie(false);
  };
  useEffect(() => { api<Vue>(base).then(charger).catch(() => undefined); }, [base]);

  // Quantités calculées : gaine = longueur du tendon, torons = longueur × torons du système, ancrages = ceux du tendon.
  const calcul = useMemo(() => {
    const parSysteme: Record<number, { tendons: number; gaine: number; ancrages: number }> = {};
    let gaine = 0, torons = 0, x = 0, y = 0;
    for (const l of lignes) {
      const t = ent(l.torons), g = lireNombre(l.longueur);
      gaine += g; torons += g * t;
      if (l.sens === "Y") y++; else x++;
      if (!t) continue;
      parSysteme[t] ??= { tendons: 0, gaine: 0, ancrages: 0 };
      parSysteme[t].tendons++; parSysteme[t].gaine += g; parSysteme[t].ancrages += ent(l.ancrages);
    }
    return { parSysteme, gaine, torons, x, y };
  }, [lignes]);

  if (!vue) return null;
  const lecture = !vue.modifiable;
  const touche = () => { setModifie(true); setMessage(null); };
  const majLigne = (i: number, patch: Partial<Ligne>) => { setLignes(lignes.map((l, j) => (j === i ? { ...l, ...patch } : l))); touche(); };
  const majAcc = (i: number, patch: Partial<Accessoire>) => { setAccessoires(accessoires.map((a, j) => (j === i ? { ...a, ...patch } : a))); touche(); };

  async function enregistrer() {
    setMessage(null);
    try {
      charger(await api<Vue>(base, {
        method: "PUT",
        body: JSON.stringify({
          tendons: lignes.map((l) => ({ numero: l.numero, torons: ent(l.torons), sens: l.sens, longueur: lireNombre(l.longueur), ancrages: ent(l.ancrages) })),
          accessoires: accessoires.map((a) => ({ libelle: a.libelle, unite: a.unite, quantite: ent(a.quantite) })),
          surface: lireNombre(surface) || null,
        }),
      }));
      setMessage({ ok: true, texte: "Commande enregistrée." }); setImporte(null);
    } catch (e) {
      setMessage({ ok: false, texte: e instanceof ApiError ? (e.status === 403 ? "Votre rôle ne permet pas de saisir la commande." : e.message) : "Enregistrement impossible." });
    }
  }

  // L'import remplace la liste affichée ; rien n'est enregistré tant que l'on n'a pas cliqué sur « Enregistrer la commande ».
  async function importer(f: File) {
    setMessage(null); setImporte(null);
    const corps = new FormData(); corps.append("fichier", f);
    try {
      const res = await fetch(`/api${base}/import`, { method: "POST", body: corps, credentials: "include" });
      const r = await res.json().catch(() => null);
      if (!res.ok) { setMessage({ ok: false, texte: res.status === 403 ? "Votre rôle ne permet pas de saisir la commande." : r?.message ?? "Import impossible." }); return; }
      if (r.tendons.length > 0) { setLignes(r.tendons.map(versLigne)); setModifie(true); }
      setImporte({ nb: r.tendons.length, erreurs: r.erreurs });
    } catch { setMessage({ ok: false, texte: "Import impossible." }); }
    finally { if (fichier.current) fichier.current.value = ""; }
  }

  if (lecture && lignes.length === 0) return <div className="mt-2 text-[13px] text-secondaire">Aucun tendon saisi dans la commande usine.</div>;

  return (
    <div className="mt-3 rounded-lg border border-bordure bg-white p-3">
      <div className="text-[13px] font-bold">Commande usine</div>
      {lecture && vue.verrou && <div className="mt-0.5 text-[12px] text-secondaire">{vue.verrou}</div>}

      {/* Surface totale de la commande : imprimée sur le bon de livraison et facturée. */}
      <div className="mt-2 flex flex-wrap items-center gap-3">
        <label className="text-[13px] font-semibold" htmlFor={`surface-${dalleId}`}>Surface totale de la commande (m²){!lecture && <span className="text-rouge-texte"> *</span>}</label>
        {lecture ? <span className="font-display font-bold">{vue.surface ? formatSurface(vue.surface) : "Non saisie"}</span> : (
          <input id={`surface-${dalleId}`} className={cn(CELL, "w-32 text-right")} inputMode="decimal" placeholder={String(vue.surfaceDalle).replace(".", ",")}
            value={surface} onChange={(e) => { setSurface(filtrerDecimal(e.target.value)); touche(); }} />
        )}
        <span className="text-[12px] text-secondaire">Reprise sur le bon de livraison et la facture. Surface de la dalle : {formatSurface(vue.surfaceDalle)}.</span>
      </div>

      {!lecture && (
        <div className="mt-2 flex flex-wrap items-center gap-2 rounded-md bg-fond px-3 py-2">
          <input ref={fichier} type="file" accept=".json,.xlsx,.csv" className="hidden" onChange={(e) => e.target.files?.[0] && importer(e.target.files[0])} />
          <Button size="sm" variant="outline" onClick={() => fichier.current?.click()}><Upload size={14} />Importer les tendons</Button>
          <span className="text-[12px] text-secondaire">Fichier JSON, Excel ou CSV. Trame vide :</span>
          {TRAMES.map(([ext, label]) => (
            <a key={ext} href={`/api${base}/trame.${ext}`} className="inline-flex items-center gap-1 text-[12px] font-semibold text-marine underline"><FileDown size={13} />{label}</a>
          ))}
        </div>
      )}
      {importe && (
        <div className={cn("mt-2 rounded-md px-3 py-2 text-[13px]", importe.erreurs.length ? "bg-orange-fond text-orange-texte" : "bg-vert-fond text-vert-texte")}>
          <b>{importe.nb} tendon(s) lus dans le fichier.</b> {importe.nb > 0 ? "Ils remplacent la liste ci-dessous : vérifiez puis enregistrez la commande." : "La liste n'a pas été modifiée."}
          {importe.erreurs.length > 0 && <ul className="mt-1 list-disc pl-5 text-[12px]">{importe.erreurs.map((e, k) => <li key={k}>{e}</li>)}</ul>}
        </div>
      )}

      <div className="mt-2 max-h-[420px] overflow-auto rounded-md border border-bordure">
        <table className="w-full text-left text-[13px]">
          <thead className="sticky top-0 z-10 bg-fond text-[11px] uppercase tracking-wide text-secondaire">
            <tr>
              <th className="px-2 py-1.5 font-semibold">N° tendon</th>
              <th className="px-1 py-1.5 font-semibold">Type d&apos;ancrage</th>
              <th className="px-1 py-1.5 font-semibold">Orientation</th>
              <th className="px-1 py-1.5 text-right font-semibold">Longueur m</th>
              <th className="px-1 py-1.5 text-right font-semibold">Ancrages</th>
              <th className="px-2 py-1.5 text-right font-semibold">Torons m</th>
              {!lecture && <th />}
            </tr>
          </thead>
          <tbody>
            {lignes.length === 0 && <tr><td colSpan={7} className="px-2 py-4 text-center text-secondaire">Aucun tendon : importez un fichier ou ajoutez-les un par un.</td></tr>}
            {lignes.map((l, i) => (
              <tr key={i} className="border-t border-bordure">
                <td className="w-28 px-2 py-1"><input className={cn(CELL, "font-bold text-marine")} disabled={lecture} aria-label={`Numéro du tendon, ligne ${i + 1}`} inputMode="numeric" value={l.numero} onChange={(e) => majLigne(i, { numero: e.target.value.replace(/\D/g, "") })} /></td>
                <td className="w-24 px-1 py-1">
                  {/* Types d'ancrage existants ; un type déjà enregistré hors liste reste affiché. */}
                  <select className={cn(CELL, "text-left")} disabled={lecture} aria-label={`Type d'ancrage, tendon ${l.numero}`} value={l.torons} onChange={(e) => majLigne(i, { torons: e.target.value })}>
                    {[...new Set([...(vue?.typesAncrage ?? []), ...(l.torons ? [Number(l.torons)] : [])])].sort((a, b) => a - b).map((t) => <option key={t} value={String(t)}>S{t}</option>)}
                  </select>
                </td>
                <td className="w-24 px-1 py-1">
                  <select className={CELL} disabled={lecture} aria-label={`Orientation, tendon ${l.numero}`} value={l.sens} onChange={(e) => majLigne(i, { sens: e.target.value })}><option value="X">X</option><option value="Y">Y</option></select>
                </td>
                <td className="w-28 px-1 py-1"><input className={cn(CELL, "text-right")} disabled={lecture} inputMode="decimal" aria-label={`Longueur, tendon ${l.numero}`} value={l.longueur} onChange={(e) => majLigne(i, { longueur: e.target.value })} /></td>
                <td className="w-24 px-1 py-1"><input className={cn(CELL, "text-right")} disabled={lecture} inputMode="numeric" aria-label={`Ancrages, tendon ${l.numero}`} value={l.ancrages} onChange={(e) => majLigne(i, { ancrages: e.target.value })} /></td>
                <td className="whitespace-nowrap px-2 py-1 text-right font-display text-secondaire">{formatNombre(lireNombre(l.longueur) * ent(l.torons))}</td>
                {!lecture && <td className="w-10"><Button variant="ghost" size="icon" aria-label={`Retirer le tendon ${l.numero}`} onClick={() => { setLignes(lignes.filter((_, j) => j !== i)); touche(); }}><Trash2 size={14} /></Button></td>}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {!lecture && (
        <Button size="sm" variant="outline" className="mt-2"
          onClick={() => { const d = lignes[lignes.length - 1]; const n = parseInt(d?.numero ?? "0", 10); setLignes([...lignes, { numero: Number.isNaN(n) ? "" : String(n + 1).padStart(2, "0"), torons: d?.torons ?? "4", sens: d?.sens ?? "X", longueur: "", ancrages: d?.ancrages ?? "" }]); touche(); }}>
          <Plus size={14} />Ajouter un tendon
        </Button>
      )}

      {lignes.length > 0 && (
        <dl className="mt-3 grid grid-cols-2 gap-x-4 gap-y-1 rounded-md bg-fond px-3 py-2 text-[13px] sm:grid-cols-4">
          <div><dt className="text-secondaire">Tendons</dt><dd className="font-display font-bold">{lignes.length} <span className="font-normal text-secondaire">· {calcul.x} en X, {calcul.y} en Y</span></dd></div>
          <div><dt className="text-secondaire">Gaines</dt><dd className="font-display font-bold">{formatNombre(calcul.gaine)} m</dd></div>
          <div><dt className="text-secondaire">Torons</dt><dd className="font-display font-bold">{formatNombre(calcul.torons)} m</dd></div>
          <div>
            <dt className="text-secondaire">Par système</dt>
            <dd className="font-display font-bold">{Object.entries(calcul.parSysteme).map(([t, v]) => `S${t} : ${v.tendons} tendons, ${v.ancrages} ancrages`).join(" · ")}</dd>
          </div>
        </dl>
      )}

      <div className="mt-3 text-[12px] font-bold uppercase tracking-wide text-secondaire">Accessoires</div>
      {accessoires.length === 0 && lecture && <div className="text-[13px] text-attenue">Aucun accessoire.</div>}
      <div className="mt-1 space-y-1.5">
        {accessoires.map((a, i) => (
          <div key={i} className="flex items-center gap-2">
            <input className={cn(CELL, "w-20 text-right")} disabled={lecture} inputMode="numeric" aria-label={`Quantité, ${a.libelle}`} placeholder="0" value={a.quantite} onChange={(e) => majAcc(i, { quantite: e.target.value })} />
            <span className="w-16 shrink-0 text-[13px] text-secondaire">{a.unite}</span>
            <span className={cn("flex-1 text-[13px]", ent(a.quantite) > 0 ? "font-semibold" : "text-secondaire")}>{a.libelle}</span>
            {!lecture && <Button variant="ghost" size="icon" aria-label={`Retirer ${a.libelle}`} onClick={() => { setAccessoires(accessoires.filter((_, j) => j !== i)); touche(); }}><Trash2 size={14} /></Button>}
          </div>
        ))}
      </div>
      {!lecture && (
        <>
          <p className="mt-1.5 text-[12px] text-attenue">Saisissez la quantité des accessoires voulus. Un accessoire laissé à 0 ou retiré n&apos;entre pas dans la commande.</p>
          <div className="mt-3 flex items-center gap-3 border-t border-bordure pt-3">
            <Button size="sm" disabled={!modifie} onClick={enregistrer}>Enregistrer la commande</Button>
            {message && <span role={message.ok ? "status" : "alert"} className={cn("text-[13px]", message.ok ? "text-vert-texte" : "text-rouge-texte")}>{message.texte}</span>}
            {modifie && !message && <span className="text-[13px] text-secondaire">Modifications non enregistrées</span>}
          </div>
        </>
      )}
    </div>
  );
}

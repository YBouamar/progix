"use client";

import { Fragment, useEffect, useMemo, useRef, useState } from "react";
import { CircleAlert, FileDown, FileCog, Plus, Trash2, Upload } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn, filtrerDecimal, formatNombre, formatSurface, lireNombre } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { ImportCablage, type ApercuCablage } from "./cablage";

type TendonVue = { numero: string; torons: number; sens: string; longueur: number; ancrages: number; modifiee?: boolean; ajoutee?: boolean; avant?: string | null };
type Modification = { le: string; par: string; etat: string; changements: string[] };
type Vue = {
  modifiable: boolean; verrou: string | null; tendons: TendonVue[]; typesAncrage: number[]; surface: number | null; surfaceDalle: number;
  accessoires: { libelle: string; unite: string | null; quantite: number }[]; proposes: { libelle: string; unite: string | null }[];
  numero?: number; modifications?: Modification[];
};
/** Réponse de GET /commandes/{id} : la commande soumise, modifiable avec trace. */
type VueCommande = { commande: { numero: number; surface: number | null; tendons: TendonVue[]; accessoires: Vue["accessoires"]; modifications: Modification[] }; modifiable: boolean; typesAncrage: number[]; proposes: Vue["proposes"]; surfaceDalle: number };
type Ligne = { numero: string; torons: string; sens: string; longueur: string; ancrages: string; modifiee?: boolean; ajoutee?: boolean; avant?: string | null };
type Accessoire = { libelle: string; unite: string | null; quantite: string };

const versLigne = (t: TendonVue): Ligne => ({ numero: t.numero, torons: String(t.torons), sens: t.sens, longueur: String(t.longueur).replace(".", ","), ancrages: String(t.ancrages), modifiee: t.modifiee, ajoutee: t.ajoutee, avant: t.avant });
const TRAMES: [string, string][] = [["xlsx", "Excel"], ["csv", "CSV"], ["json", "JSON"]];

const CELL = "h-8 w-full min-w-0 rounded-md border border-bordure-forte bg-white px-2 font-display text-[13px] focus:border-marine focus:outline-none disabled:border-transparent disabled:bg-transparent";
const ent = (s: string) => parseInt(s, 10) || 0;

/** Commande usine : un tendon par ligne (numéro, système, orientation, longueur, ancrages), saisi ou importé d'un fichier, et les accessoires. */
/** Avec `commandeId`, la commande est déjà soumise : ses modifications sont tracées et signalées à l'usine. Avec `modele`, on crée un complément. */
export function CommandeUsine({ projetId, dalleId, commandeId, modele, onChange }: {
  projetId: string; dalleId: string; commandeId?: string; onChange?: () => void;
  modele?: { typesAncrage: number[]; proposes: Vue["proposes"]; surfaceDalle: number };
}) {
  const [vue, setVue] = useState<Vue | null>(null);
  const [lignes, setLignes] = useState<Ligne[]>([]);
  const [accessoires, setAccessoires] = useState<Accessoire[]>([]);
  const [surface, setSurface] = useState("");
  const [message, setMessage] = useState<{ ok: boolean; texte: string } | null>(null);
  const [modifie, setModifie] = useState(false);
  const [importe, setImporte] = useState<{ nb: number; erreurs: string[] } | null>(null);
  const [dxf, setDxf] = useState(false);
  const fichier = useRef<HTMLInputElement>(null);
  const racine = `/projets/${projetId}/dalles/${dalleId}`;
  const base = commandeId ? `${racine}/commandes/${commandeId}` : modele ? `${racine}/commandes` : `${racine}/commande`;

  const charger = (r: Vue) => {
    setVue(r);
    setLignes(r.tendons.map(versLigne));
    setSurface(r.surface ? String(r.surface).replace(".", ",") : "");
    // Tous les accessoires paramétrés sont proposés ; ceux de la commande gardent leur quantité.
    const saisis = r.accessoires.map((a) => ({ libelle: a.libelle, unite: a.unite, quantite: String(a.quantite) }));
    setAccessoires([...saisis, ...r.proposes.filter((p) => !r.accessoires.some((a) => a.libelle === p.libelle)).map((p) => ({ libelle: p.libelle, unite: p.unite, quantite: "" }))]);
    setModifie(false);
  };
  useEffect(() => {
    if (modele) { charger({ modifiable: true, verrou: null, tendons: [], typesAncrage: modele.typesAncrage, surface: null, surfaceDalle: modele.surfaceDalle, accessoires: [], proposes: modele.proposes }); return; }
    if (commandeId) {
      api<VueCommande>(base).then((r) => charger({ modifiable: r.modifiable, verrou: null, tendons: r.commande.tendons, typesAncrage: r.typesAncrage, surface: r.commande.surface, surfaceDalle: r.surfaceDalle,
        accessoires: r.commande.accessoires, proposes: r.proposes, numero: r.commande.numero, modifications: r.commande.modifications })).catch(() => undefined);
      return;
    }
    api<Vue>(base).then(charger).catch(() => undefined);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [base]);

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
      const corps = JSON.stringify({
          tendons: lignes.map((l) => ({ numero: l.numero, torons: ent(l.torons), sens: l.sens, longueur: lireNombre(l.longueur), ancrages: ent(l.ancrages) })),
          accessoires: accessoires.map((a) => ({ libelle: a.libelle, unite: a.unite, quantite: ent(a.quantite) })),
          surface: lireNombre(surface) || null,
      });
      if (modele) { await api(base, { method: "POST", body: corps }); onChange?.(); return; }
      if (commandeId) {
        const r = await api<{ changements: number }>(base, { method: "PUT", body: corps });
        const v = await api<VueCommande>(base);
        charger({ modifiable: v.modifiable, verrou: null, tendons: v.commande.tendons, typesAncrage: v.typesAncrage, surface: v.commande.surface, surfaceDalle: v.surfaceDalle,
          accessoires: v.commande.accessoires, proposes: v.proposes, numero: v.commande.numero, modifications: v.commande.modifications });
        setMessage({ ok: true, texte: r.changements > 0 ? `${r.changements} changement${r.changements > 1 ? "s" : ""} enregistré${r.changements > 1 ? "s" : ""} et signalé${r.changements > 1 ? "s" : ""} à l'usine.` : "Aucun changement." });
        setImporte(null); onChange?.(); return;
      }
      charger(await api<Vue>(base, { method: "PUT", body: corps }));
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

  if (lecture && lignes.length === 0 && !commandeId) return <div className="mt-2 text-[13px] text-secondaire">Aucun tendon saisi dans la commande usine.</div>;

  return (
    <div className="mt-3 rounded-lg border border-bordure bg-white p-3">
      <div className="text-[13px] font-bold">{modele ? "Nouvelle commande complémentaire" : commandeId ? `Commande n° ${vue.numero}${vue.numero === 1 ? " (initiale)" : " (complément)"}` : "Commande usine"}</div>
      {lecture && vue.verrou && <div className="mt-0.5 text-[12px] text-secondaire">{vue.verrou}</div>}

      {/* Surface totale de la commande : imprimée sur le bon de livraison et facturée. */}
      <div className="mt-2 flex flex-wrap items-center gap-3">
        <label className="text-[13px] font-semibold" htmlFor={`surface-${dalleId}`}>Surface totale de la commande (m²){!lecture && <span className="text-rouge-texte"> *</span>}</label>
        {lecture ? <span className="font-display font-bold">{vue.surface ? formatSurface(vue.surface) : "Non saisie"}</span> : (
          <input id={`surface-${dalleId}`} className={cn(CELL, "w-32 text-right")} inputMode="decimal" placeholder={String(vue.surfaceDalle).replace(".", ",")}
            value={surface} onChange={(e) => { setSurface(filtrerDecimal(e.target.value)); touche(); }} />
        )}
        {lireNombre(surface) > 0 && lireNombre(surface) !== vue.surfaceDalle && (
          <span role="img" aria-label="La surface de la commande diffère de celle de la dalle" title="La surface de la commande diffère de celle de la dalle : celle de la dalle est retenue pour la facturation." className="text-orange-texte"><CircleAlert size={15} /></span>
        )}
        <span className="text-[12px] text-secondaire">Surface de la dalle : {formatSurface(vue.surfaceDalle)}. En cas d&apos;écart, celle de la dalle est retenue.</span>
      </div>

      {!lecture && (
        <div className="mt-2 flex flex-wrap items-center gap-2 rounded-md bg-fond px-3 py-2">
          <input ref={fichier} type="file" accept=".json,.xlsx,.csv" className="hidden" onChange={(e) => e.target.files?.[0] && importer(e.target.files[0])} />
          <Button size="sm" variant="outline" onClick={() => setDxf(true)}><FileCog size={14} />Importer depuis les plans ADAPT (DXF)</Button>
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
          <thead className="sticky top-0 z-10 bg-fond text-[11px] text-secondaire">
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
              <Fragment key={i}><tr className={cn("border-t border-bordure", (l.modifiee || l.ajoutee) && "bg-orange-fond")}>
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
              {(l.modifiee || l.ajoutee) && (
                <tr className="bg-orange-fond"><td colSpan={7} className="px-2 pb-1.5 text-[12px] text-orange-texte">
                  <b>{l.ajoutee ? "Ligne ajoutée après la soumission." : "Ligne modifiée après la soumission."}</b>{l.avant && !l.ajoutee ? ` Avant : ${l.avant}.` : ""}
                </td></tr>
              )}
            </Fragment>
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

      {(vue.modifications?.length ?? 0) > 0 && (
        <details className="mt-3 rounded-md border border-bordure px-3 py-2 text-[13px]">
          <summary className="cursor-pointer font-semibold">Historique des modifications ({vue.modifications!.length})</summary>
          <ul className="mt-2 space-y-2">
            {vue.modifications!.map((m, k) => (
              <li key={k}><span className="text-[12px] text-secondaire">{new Date(m.le).toLocaleString("fr-FR")} · {m.par} · commande {m.etat.toLowerCase()}</span>
                <ul className="list-disc pl-5">{m.changements.map((c, j) => <li key={j}>{c}</li>)}</ul></li>
            ))}
          </ul>
        </details>
      )}

      <div className="mt-3 text-[12px] font-bold text-secondaire">Accessoires</div>
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
      <ImportCablage
        open={dxf} onOpenChange={setDxf} projetId={projetId} dalleId={dalleId}
        dejaNumeros={lignes.map((l) => l.numero)} surfaceCommande={surface}
        onImporter={(a: ApercuCablage, mode, surfaceDalleChangee) => {
          const neuves = a.lignes.map((l) => ({ numero: l.numero, torons: String(l.torons), sens: l.sens, longueur: String(l.longueur).replace(".", ","), ancrages: String(l.ancrages) }));
          setLignes(mode === "remplacer" ? neuves : [...lignes, ...neuves.filter((n) => !lignes.some((l) => l.numero === n.numero))]);
          if (a.surfaceDxf && !commandeId && !modele && !surface) setSurface(String(a.surfaceDxf).replace(".", ","));
          if (surfaceDalleChangee && vue) setVue({ ...vue, surfaceDalle: a.surfaceDxf ?? vue.surfaceDalle });
          setModifie(true); setMessage(null); setDxf(false);
        }}
      />
    </div>
  );
}

"use client";

import { useEffect, useMemo, useState } from "react";
import { Plus, Trash2 } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn, lireNombre } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

type Cable = { cable: string; systeme?: string | null; sens?: string | null; longueur: number | null; theorique: number; a25: number | null; a100: number | null; b25: number | null; b100: number | null };
type Vue = { modifiable: boolean; verrou: string | null; proposes?: boolean; mmParMetre: number; borneBasse: number; borneHaute: number; groupeBas: number; groupeHaut: number; forceKn: number | null; cr: string | null; cmp: string | null; cables: Cable[] };
type Ligne = { cable: string; lon: string; theo: string; a25: string; a100: string; b25: string; b100: string };

const fr = (n: number | null) => (n === null || n === undefined ? "" : String(n).replace(".", ","));
const mm = (n: number) => `${n.toLocaleString("fr-FR", { maximumFractionDigits: 1 })} mm`;
const pct = (n: number) => `${n > 0 ? "+" : ""}${n.toLocaleString("fr-FR", { maximumFractionDigits: 1 })} %`;
const CELL = "h-8 w-full min-w-14 rounded-md border border-bordure-forte bg-white px-2 text-right font-display text-[13px] focus:border-marine focus:outline-none disabled:border-transparent disabled:bg-transparent";

/** Fiche de mise en tension : lectures à 25 % et 100 % de chaque côté. Tolérance EN 13670 : ± 15 % de Ao par câble, ± 5 % pour le groupe. */
export function FicheTension({ projetId, dalleId }: { projetId: string; dalleId: string }) {
  const [vue, setVue] = useState<Vue | null>(null);
  const [lignes, setLignes] = useState<Ligne[]>([]);
  const [serie, setSerie] = useState({ nombre: "", lon: "" });
  const [entete, setEntete] = useState({ force: "", cr: "", cmp: "" });
  const [message, setMessage] = useState<{ ok: boolean; texte: string } | null>(null);
  const [modifie, setModifie] = useState(false);
  const base = `/projets/${projetId}/dalles/${dalleId}/tension`;

  const charger = (r: Vue) => {
    setVue(r);
    setEntete({ force: fr(r.forceKn), cr: r.cr ?? "", cmp: r.cmp ?? "" });
    setLignes(r.cables.map((c) => ({ cable: c.cable, lon: fr(c.longueur), theo: fr(c.theorique), a25: fr(c.a25), a100: fr(c.a100), b25: fr(c.b25), b100: fr(c.b100) })));
    // Des câbles repris de la commande usine restent à enregistrer.
    setModifie(!!r.proposes && r.modifiable);
  };
  useEffect(() => { api<Vue>(base).then(charger).catch(() => undefined); }, [base]);

  // Calcul en direct, identique à celui du serveur.
  const calcul = useMemo(() => {
    const bas = vue?.borneBasse ?? 0.85, haut = vue?.borneHaute ?? 1.15;
    const gBas = vue?.groupeBas ?? 0.95, gHaut = vue?.groupeHaut ?? 1.05;
    let theo = 0, mes = 0, hors = 0, manque = 0, n = 0;
    const rows = lignes.map((l) => {
      const t = lireNombre(l.theo);
      const vide = (x: string) => x.trim() === "";
      const dA = vide(l.a25) || vide(l.a100) ? null : lireNombre(l.a100) - lireNombre(l.a25);
      const bVide = vide(l.b25) && vide(l.b100), bComplet = !vide(l.b25) && !vide(l.b100);
      const dB = bComplet ? lireNombre(l.b100) - lireNombre(l.b25) : null;
      const min = Math.round(t * bas * 10) / 10, max = Math.round(t * haut * 10) / 10;
      // Un côté B commencé doit être complet ; laissé vide, il compte pour zéro.
      if (dA === null || t <= 0 || (!bVide && !bComplet)) { manque++; return { dA, dB, min, max, m: null, conforme: null, moy: null as number | null }; }
      const m = dA + (dB ?? 0);
      const conforme = m >= t * bas && m <= t * haut;
      theo += t; mes += m; n++; if (!conforme) hors++;
      return { dA, dB, min, max, m, conforme, moy: dB === null ? dA : Math.round(((dA + dB) / 2) * 10) / 10 };
    });
    const ecart = theo > 0 ? Math.round(((mes - theo) / theo) * 1000) / 10 : null;
    return { rows, theo, mes, ecart, hors, manque, moyenne: n > 0 ? mes / n : null, conforme: theo > 0 ? mes >= theo * gBas && mes <= theo * gHaut : null };
  }, [lignes, vue]);

  // Système et orientation de chaque tendon, tels que définis dans la commande usine.
  const reperes: Record<string, string> = {};
  for (const c of vue?.cables ?? []) if (c.systeme) reperes[c.cable.trim().toUpperCase()] = `${c.systeme} · ${c.sens}`;
  if (!vue) return null;
  const maj = (l: Ligne[]) => { setLignes(l); setModifie(true); setMessage(null); };
  const majLigne = (i: number, patch: Partial<Ligne>) => maj(lignes.map((l, j) => (j === i ? { ...l, ...patch } : l)));
  // Théorique = longueur × allongement par mètre du Paramétrage. La valeur reste modifiable pour reprendre celle des plans.
  const theoPour = (lon: string) => { const l = lireNombre(lon); return l > 0 ? fr(Math.round(l * (vue?.mmParMetre ?? 0) * 10) / 10) : ""; };

  function ajouterSerie() {
    const n = parseInt(serie.nombre, 10);
    if (!(n >= 1 && n <= 200)) return;
    // Numéros simples, à la suite du plus grand numéro déjà présent : 01, 02, 03...
    const debut = Math.max(0, ...lignes.map((l) => parseInt(l.cable, 10) || 0));
    setLignes([...lignes, ...Array.from({ length: n }, (_, i) => ({
      cable: String(debut + i + 1).padStart(2, "0"), lon: serie.lon, theo: theoPour(serie.lon), a25: "", a100: "", b25: "", b100: "",
    }))]);
    setSerie({ ...serie, nombre: "" });
  }

  async function enregistrer() {
    setMessage(null);
    try {
      const vide = (s: string) => (s.trim() === "" ? null : lireNombre(s));
      charger(await api<Vue>(base, {
        method: "PUT",
        body: JSON.stringify({ forceKn: entete.force.trim() === "" ? null : lireNombre(entete.force), cr: entete.cr, cmp: entete.cmp, cables: lignes.map((l) => ({ cable: l.cable, longueur: vide(l.lon), theorique: lireNombre(l.theo), a25: vide(l.a25), a100: vide(l.a100), b25: vide(l.b25), b100: vide(l.b100) })) }),
      }));
      setMessage({ ok: true, texte: "Fiche enregistrée." });
    } catch (e) {
      setMessage({ ok: false, texte: e instanceof ApiError ? (e.status === 403 ? "Votre rôle ne permet pas de saisir la fiche." : e.message) : "Enregistrement impossible." });
    }
  }

  const lecture = !vue.modifiable;

  return (
    <div className="mt-3 rounded-lg border border-bordure bg-white p-3">
      <div className="text-[13px] font-bold">Fiche de mise en tension</div>
      <p className="mt-0.5 text-[12px] text-secondaire">
        De chaque côté, Δ = lecture à 100 % moins lecture à 25 %. Allongement du câble = Δ côté A + Δ côté B ; le côté B reste vide si le câble est tendu d&apos;un seul côté.
        Tolérance EN 13670 : chaque câble entre {fr(vue.borneBasse)} et {fr(vue.borneHaute)} fois l&apos;allongement théorique Ao, le groupe entre {fr(vue.groupeBas)} et {fr(vue.groupeHaut)} fois la somme des Ao. Hors de ces bornes, une investigation est à mener.
        Ao = longueur × {vue.mmParMetre.toLocaleString("fr-FR")} mm par mètre (réglé dans le Paramétrage), modifiable câble par câble.
      </p>

      <div className="mt-3 flex flex-wrap gap-3">
        {([["force", "Force, kN", "decimal"], ["cr", "CR", "text"], ["cmp", "CMP", "text"]] as const).map(([k, label, mode]) => (
          <label key={k} className="text-[12px] font-semibold text-secondaire">{label}
            <input className={cn(CELL, "mt-1 block w-28 text-left")} disabled={lecture} inputMode={mode} value={entete[k]} onChange={(e) => { setEntete({ ...entete, [k]: e.target.value }); setModifie(true); setMessage(null); }} />
          </label>
        ))}
      </div>
      {vue.proposes && <div className="mt-2 rounded-md bg-fond px-3 py-1.5 text-[12px] text-secondaire">Câbles repris de la commande usine. Saisissez les lectures puis enregistrez.</div>}
      {lignes.length > 0 && (
        <div className="mt-3 overflow-x-auto">
          <table className="w-full text-left text-[13px]">
            <thead className="text-[11px] uppercase tracking-wide text-secondaire">
              <tr>
                <th rowSpan={2} className="py-1 pr-2 align-bottom font-semibold">Tendon</th>
                <th rowSpan={2} className="px-1 py-1 align-bottom font-semibold">Syst.</th>
                <th rowSpan={2} className="px-1 py-1 text-right align-bottom font-semibold">Long. m</th>
                <th rowSpan={2} className="px-1 py-1 text-right align-bottom font-semibold">Ao mm</th>
                <th colSpan={3} className="border-b border-l border-bordure px-1 py-1 text-center font-semibold">Côté A</th>
                <th colSpan={3} className="border-b border-l border-bordure px-1 py-1 text-center font-semibold">Côté B</th>
                <th rowSpan={2} className="border-l border-bordure px-2 py-1 text-right align-bottom font-semibold">Total</th>
                <th rowSpan={2} className="px-2 py-1 text-right align-bottom font-semibold">Moyenne</th>
                <th rowSpan={2} className="px-2 py-1 text-right align-bottom font-semibold">{fr(vue.borneBasse)} Ao</th>
                <th rowSpan={2} className="px-2 py-1 text-right align-bottom font-semibold">{fr(vue.borneHaute)} Ao</th>
                <th rowSpan={2} className="px-2 py-1 align-bottom font-semibold">Verdict</th>
                {!lecture && <th rowSpan={2} />}
              </tr>
              <tr>
                {["A", "B"].flatMap((c) => [
                  <th key={`${c}25`} className="border-l border-bordure px-1 py-1 text-right font-semibold">25 %</th>,
                  <th key={`${c}100`} className="px-1 py-1 text-right font-semibold">100 %</th>,
                  <th key={`${c}d`} className="px-1 py-1 text-right font-semibold">Δ</th>,
                ])}
              </tr>
            </thead>
            <tbody>
              {lignes.map((l, i) => {
                const r = calcul.rows[i];
                const lectureCell = (k: "a25" | "a100" | "b25" | "b100", label: string, bord?: boolean) => (
                  <td className={cn("w-16 px-1 py-1", bord && "border-l border-bordure")}><input className={CELL} disabled={lecture} inputMode="decimal" aria-label={`${label}, ${l.cable}`} value={l[k]} onChange={(e) => majLigne(i, { [k]: e.target.value })} /></td>
                );
                const nb = (x: number | null) => (x === null ? "" : x.toLocaleString("fr-FR", { maximumFractionDigits: 1 }));
                return (
                  <tr key={i} className="border-t border-bordure">
                    <td className="py-1 pr-2"><input className={cn(CELL, "w-16 min-w-16 text-left font-bold text-marine")} disabled={lecture} aria-label={`Tendon, ligne ${i + 1}`} value={l.cable} onChange={(e) => majLigne(i, { cable: e.target.value })} /></td>
                    <td className="whitespace-nowrap px-1 py-1 text-[12px] text-secondaire">{reperes[l.cable.trim().toUpperCase()] ?? ""}</td>
                    <td className="w-16 px-1 py-1"><input className={CELL} disabled={lecture} inputMode="decimal" aria-label={`Longueur, ${l.cable}`} value={l.lon} onChange={(e) => majLigne(i, { lon: e.target.value, theo: theoPour(e.target.value) || l.theo })} /></td>
                    <td className="w-16 px-1 py-1"><input className={CELL} disabled={lecture} inputMode="decimal" aria-label={`Ao, ${l.cable}`} value={l.theo} onChange={(e) => majLigne(i, { theo: e.target.value })} /></td>
                    {lectureCell("a25", "Côté A à 25 %", true)}{lectureCell("a100", "Côté A à 100 %")}
                    <td className="whitespace-nowrap px-1 py-1 text-right font-display text-secondaire">{nb(r.dA)}</td>
                    {lectureCell("b25", "Côté B à 25 %", true)}{lectureCell("b100", "Côté B à 100 %")}
                    <td className="whitespace-nowrap px-1 py-1 text-right font-display text-secondaire">{nb(r.dB)}</td>
                    <td className="whitespace-nowrap border-l border-bordure px-2 py-1 text-right font-display font-bold">{nb(r.m)}</td>
                    <td className="whitespace-nowrap px-2 py-1 text-right font-display">{nb(r.moy)}</td>
                    <td className="whitespace-nowrap px-2 py-1 text-right font-display text-secondaire">{nb(r.min)}</td>
                    <td className="whitespace-nowrap px-2 py-1 text-right font-display text-secondaire">{nb(r.max)}</td>
                    <td className="whitespace-nowrap px-2 py-1">
                      {r.conforme === null ? <span className="text-attenue">À mesurer</span> : <Badge ton={r.conforme ? "vert" : "rouge"}>{r.conforme ? "Conforme" : "Hors tolérance"}</Badge>}
                    </td>
                    {!lecture && <td><Button variant="ghost" size="icon" aria-label={`Retirer ${l.cable}`} onClick={() => maj(lignes.filter((_, j) => j !== i))}><Trash2 size={14} /></Button></td>}
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {lignes.length > 0 && (
        <div className={cn("mt-3 rounded-md px-3 py-2 text-[13px]", calcul.conforme === null ? "bg-fond text-secondaire" : calcul.conforme && calcul.hors === 0 ? "bg-vert-fond text-vert-texte" : "bg-rouge-fond text-rouge-texte")}>
          {calcul.ecart === null ? "Groupe : aucune mesure complète." : (
            <>
              <strong>Groupe : {mm(calcul.mes)} mesurés pour {mm(calcul.theo)} théoriques, écart {pct(calcul.ecart)}.</strong>{" "}
              {calcul.moyenne !== null && `Moyenne des allongements : ${mm(calcul.moyenne)}. `}
              {calcul.conforme ? "Groupe conforme." : "Groupe hors tolérance."}
              {calcul.hors > 0 && ` ${calcul.hors} câble(s) hors tolérance.`}
              {calcul.manque > 0 && ` ${calcul.manque} câble(s) à mesurer.`}
            </>
          )}
        </div>
      )}

      {lecture ? (
        lignes.length === 0 && <div className="mt-2 text-[13px] text-secondaire">{vue.verrou ?? "Aucune mesure saisie."}</div>
      ) : (
        <>
          <div className="mt-3 flex flex-wrap items-center gap-2 border-t border-bordure pt-3">
            <span className="text-[12px] font-semibold text-secondaire">Ajouter des câbles</span>
            <Input className="h-8 w-20" inputMode="numeric" aria-label="Nombre de câbles" placeholder="Nombre" value={serie.nombre} onChange={(e) => setSerie({ ...serie, nombre: e.target.value })} />
            <Input className="h-8 w-32" inputMode="decimal" aria-label="Longueur des câbles" placeholder="Longueur m" value={serie.lon} onChange={(e) => setSerie({ ...serie, lon: e.target.value })} />
            <Button size="sm" variant="outline" onClick={ajouterSerie}><Plus size={14} />Ajouter</Button>
          </div>
          <div className="mt-3 flex items-center gap-3">
            <Button size="sm" disabled={!modifie} onClick={enregistrer}>Enregistrer la fiche</Button>
            {message && <span role={message.ok ? "status" : "alert"} className={cn("text-[13px]", message.ok ? "text-vert-texte" : "text-rouge-texte")}>{message.texte}</span>}
            {modifie && !message && <span className="text-[13px] text-secondaire">Modifications non enregistrées</span>}
          </div>
        </>
      )}
    </div>
  );
}

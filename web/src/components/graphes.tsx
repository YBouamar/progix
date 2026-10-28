"use client";

import { useEffect, useRef, useState } from "react";
import { cn } from "@/lib/utils";

/**
 * Graphes maison, sans bibliothèque : barres horizontales (un classement) et colonnes empilées (une évolution).
 * Marques fines, extrémité arrondie à 4 px côté donnée, 2 px d'espace entre segments, grille discrète,
 * infobulle au survol, légende dès deux séries, valeurs écrites en clair et vue tableau pour les couleurs peu contrastées.
 */

/** Ordre fixe des couleurs de catégorie (validé daltonisme) ; une série garde sa couleur quel que soit le filtre. */
export const COULEURS_SERIES = ["#2a78d6", "#eb6834", "#1baf7a", "#eda100"];
/** Série unique : la couleur de la marque. */
export const COULEUR_UNIQUE = "#1b2a6b";

type Bulle = { x: number; y: number; titre: string; lignes: [string, string, string?][] } | null;

function useLargeur<T extends HTMLElement>() {
  const ref = useRef<T>(null);
  const [largeur, setLargeur] = useState(0);
  useEffect(() => {
    if (!ref.current) return;
    const o = new ResizeObserver(([e]) => setLargeur(e.contentRect.width));
    o.observe(ref.current);
    return () => o.disconnect();
  }, []);
  return [ref, largeur] as const;
}

/** Infobulle : valeurs en encre du texte, la pastille de couleur porte l'identité. */
function Infobulle({ bulle }: { bulle: Bulle }) {
  if (!bulle) return null;
  return (
    <div role="tooltip" className="pointer-events-none absolute z-10 min-w-40 -translate-x-1/2 -translate-y-full rounded-lg border border-bordure bg-white px-3 py-2 text-[12px] shadow-md"
      style={{ left: bulle.x, top: bulle.y - 8 }}>
      <div className="mb-1 font-semibold text-texte">{bulle.titre}</div>
      {bulle.lignes.map(([label, valeur, couleur]) => (
        <div key={label} className="flex items-center justify-between gap-4">
          <span className="flex items-center gap-1.5 text-secondaire">{couleur && <span className="size-2 rounded-sm" style={{ background: couleur }} />}{label}</span>
          <span className="font-display font-semibold text-texte">{valeur}</span>
        </div>
      ))}
    </div>
  );
}

/** Maximum « rond » de l'axe (1, 2, 2,5 ou 5 × 10ⁿ) et graduations. */
function echelle(max: number, nb = 4) {
  if (max <= 0) return { haut: 1, pas: [0, 1] };
  const brut = max / nb, p = 10 ** Math.floor(Math.log10(brut));
  const unite = [1, 2, 2.5, 5, 10].map((k) => k * p).find((k) => k >= brut) ?? 10 * p;
  const haut = unite * Math.ceil(max / unite);
  return { haut, pas: Array.from({ length: Math.round(haut / unite) + 1 }, (_, i) => i * unite) };
}

/** Graduation abrégée : 1 200 000 → 1,2 M. */
export const abrege = (n: number) =>
  n >= 1e6 ? `${(n / 1e6).toLocaleString("fr-FR", { maximumFractionDigits: 1 })} M` : n >= 1e4 ? `${(n / 1e3).toLocaleString("fr-FR", { maximumFractionDigits: 0 })} k` : n.toLocaleString("fr-FR", { maximumFractionDigits: 1 });

/** Rectangle arrondi seulement côté donnée (haut pour une colonne, droite pour une barre). */
function chemin(x: number, y: number, l: number, h: number, r: number, cote: "haut" | "droite") {
  r = Math.max(0, Math.min(r, cote === "haut" ? l / 2 : h / 2, cote === "haut" ? h : l));
  return cote === "haut"
    ? `M${x},${y + h}V${y + r}Q${x},${y} ${x + r},${y}H${x + l - r}Q${x + l},${y} ${x + l},${y + r}V${y + h}Z`
    : `M${x},${y}H${x + l - r}Q${x + l},${y} ${x + l},${y + r}V${y + h - r}Q${x + l},${y + h} ${x + l - r},${y + h}H${x}Z`;
}

export type Barre = { id: string; label: string; valeur: number; couleur?: string; detail?: string };

/** Classement : une barre horizontale par élément, valeur écrite au bout de la barre. */
export function BarresHorizontales({ donnees, format, vide = "Aucune donnée.", unite }: {
  donnees: Barre[]; format: (n: number) => string; vide?: string; unite?: string;
}) {
  const [ref, largeur] = useLargeur<HTMLDivElement>();
  const [bulle, setBulle] = useState<Bulle>(null);
  if (donnees.length === 0) return <p className="py-6 text-center text-[13px] text-secondaire">{vide}</p>;
  const max = Math.max(...donnees.map((d) => d.valeur), 0);
  // Colonne des valeurs à la mesure de la plus longue (environ 7 px par caractère en 12 px).
  const colLabel = Math.min(170, Math.max(110, largeur * 0.32)), colValeur = Math.max(40, ...donnees.map((d) => format(d.valeur).length * 7 + 14));
  const zone = Math.max(40, largeur - colLabel - colValeur);
  const hauteurLigne = 30, epaisseur = 14;
  return (
    <div ref={ref} className="relative" onMouseLeave={() => setBulle(null)}>
      {largeur > 0 && (
        <svg width={largeur} height={donnees.length * hauteurLigne} role="img" aria-label={`Graphe en barres${unite ? `, ${unite}` : ""}`}>
          {donnees.map((d, i) => {
            const y = i * hauteurLigne, l = max > 0 ? (d.valeur / max) * zone : 0;
            return (
              <g key={d.id} onMouseMove={(e) => {
                const r = ref.current!.getBoundingClientRect();
                setBulle({ x: e.clientX - r.left, y: e.clientY - r.top, titre: d.label, lignes: [[unite ?? "Valeur", format(d.valeur), d.couleur ?? COULEUR_UNIQUE], ...(d.detail ? [["Détail", d.detail] as [string, string]] : [])] });
              }}>
                {/* Zone de survol plus grande que la marque. */}
                <rect x={0} y={y} width={largeur} height={hauteurLigne} fill="transparent" />
                <text x={colLabel - 10} y={y + hauteurLigne / 2} dominantBaseline="middle" textAnchor="end" className="fill-secondaire text-[12px]">
                  {d.label.length > 24 ? `${d.label.slice(0, 23)}…` : d.label}
                </text>
                <rect x={colLabel} y={y + (hauteurLigne - epaisseur) / 2} width={zone} height={epaisseur} rx={4} className="fill-fond" />
                {l > 0 && <path d={chemin(colLabel, y + (hauteurLigne - epaisseur) / 2, Math.max(l, 4), epaisseur, 4, "droite")} fill={d.couleur ?? COULEUR_UNIQUE} />}
                <text x={colLabel + zone + 8} y={y + hauteurLigne / 2} dominantBaseline="middle" className="fill-texte font-display text-[12px] font-semibold">{format(d.valeur)}</text>
              </g>
            );
          })}
        </svg>
      )}
      <Infobulle bulle={bulle} />
    </div>
  );
}

export type Serie = { id: string; label: string; couleur: string; valeurs: number[] };

/** Évolution : une colonne par période, empilée par série ; total au-dessus des colonnes, légende dès deux séries. */
export function Colonnes({ categories, series, format, surligne, hauteur = 220, tableau, detailCategorie }: {
  categories: string[]; series: Serie[]; format: (n: number) => string; surligne?: number | null; hauteur?: number;
  /** Bouton « Voir le tableau » : relais pour les couleurs peu contrastées et pour la lecture précise. */
  tableau?: boolean;
  /** Libellé complet d'une période dans l'infobulle (ex. « Mars 2026 »). */
  detailCategorie?: (i: number) => string;
}) {
  const [ref, largeur] = useLargeur<HTMLDivElement>();
  const [bulle, setBulle] = useState<Bulle>(null);
  const [vueTableau, setVueTableau] = useState(false);
  const totaux = categories.map((_, i) => series.reduce((s, x) => s + (x.valeurs[i] ?? 0), 0));
  const { haut, pas } = echelle(Math.max(...totaux, 0));
  const gauche = 48, bas = 24, hautMarge = 18;
  const zoneL = Math.max(40, largeur - gauche - 4), zoneH = hauteur - bas - hautMarge;
  const bande = zoneL / Math.max(1, categories.length), epaisseur = Math.min(28, bande * 0.6);
  const y = (v: number) => hautMarge + zoneH - (v / haut) * zoneH;

  return (
    <div>
      {series.length > 1 && (
        <ul className="mb-2 flex flex-wrap gap-x-4 gap-y-1 text-[12px] text-secondaire" aria-label="Légende">
          {series.map((s) => <li key={s.id} className="flex items-center gap-1.5"><span className="size-2.5 rounded-sm" style={{ background: s.couleur }} />{s.label}</li>)}
        </ul>
      )}
      {vueTableau ? (
        <div className="overflow-x-auto">
          <table className="w-full text-[12px]">
            <thead className="text-secondaire"><tr><th className="py-1 text-left font-semibold">Période</th>{series.map((s) => <th key={s.id} className="py-1 text-right font-semibold">{s.label}</th>)}{series.length > 1 && <th className="py-1 text-right font-semibold">Total</th>}</tr></thead>
            <tbody>
              {categories.map((c, i) => (
                <tr key={c} className="border-t border-bordure">
                  <td className="py-1">{detailCategorie?.(i) ?? c}</td>
                  {series.map((s) => <td key={s.id} className="py-1 text-right font-display">{format(s.valeurs[i] ?? 0)}</td>)}
                  {series.length > 1 && <td className="py-1 text-right font-display font-semibold">{format(totaux[i])}</td>}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div ref={ref} className="relative" onMouseLeave={() => setBulle(null)}>
          {largeur > 0 && (
            <svg width={largeur} height={hauteur} role="img" aria-label="Graphe en colonnes">
              {pas.map((v) => (
                <g key={v}>
                  <line x1={gauche} x2={largeur} y1={y(v)} y2={y(v)} className={v === 0 ? "stroke-bordure-forte" : "stroke-bordure"} strokeDasharray={v === 0 ? undefined : "2 4"} />
                  <text x={gauche - 6} y={y(v)} dominantBaseline="middle" textAnchor="end" className="fill-attenue text-[11px]">{abrege(v)}</text>
                </g>
              ))}
              {categories.map((c, i) => {
                const x = gauche + i * bande + (bande - epaisseur) / 2;
                let cumul = 0;
                const visibles = series.filter((s) => (s.valeurs[i] ?? 0) > 0);
                return (
                  <g key={c} onMouseMove={(e) => {
                    const r = ref.current!.getBoundingClientRect();
                    setBulle({
                      x: e.clientX - r.left, y: e.clientY - r.top, titre: detailCategorie?.(i) ?? c,
                      lignes: [...series.map((s) => [s.label, format(s.valeurs[i] ?? 0), s.couleur] as [string, string, string]), ...(series.length > 1 ? [["Total", format(totaux[i])] as [string, string]] : [])],
                    });
                  }}>
                    <rect x={gauche + i * bande} y={hautMarge} width={bande} height={zoneH} fill="transparent" />
                    {surligne === i && <rect x={gauche + i * bande + 2} y={hautMarge} width={bande - 4} height={zoneH} rx={4} className="fill-marine/5" />}
                    {visibles.map((s, k) => {
                      const v = s.valeurs[i] ?? 0, h = (v / haut) * zoneH, y0 = hautMarge + zoneH - cumul - h;
                      cumul += h;
                      // 2 px d'espace entre segments empilés ; seul le segment du haut est arrondi.
                      const espace = k < visibles.length - 1 ? 2 : 0;
                      return k === visibles.length - 1
                        ? <path key={s.id} d={chemin(x, y0, epaisseur, Math.max(h, 2), 4, "haut")} fill={s.couleur} />
                        : <rect key={s.id} x={x} y={y0 + espace} width={epaisseur} height={Math.max(0, h - espace)} fill={s.couleur} />;
                    })}
                    {totaux[i] > 0 && bande >= 34 && <text x={x + epaisseur / 2} y={y(totaux[i]) - 5} textAnchor="middle" className="fill-secondaire font-display text-[10px]">{abrege(totaux[i])}</text>}
                    <text x={gauche + i * bande + bande / 2} y={hauteur - 6} textAnchor="middle" className={cn("text-[11px]", surligne === i ? "fill-marine font-semibold" : "fill-secondaire")}>{c}</text>
                  </g>
                );
              })}
            </svg>
          )}
          <Infobulle bulle={bulle} />
        </div>
      )}
      {tableau && (
        <button type="button" className="mt-1 cursor-pointer text-[12px] font-semibold text-marine hover:underline" onClick={() => setVueTableau((t) => !t)}>
          {vueTableau ? "Voir le graphe" : "Voir le tableau"}
        </button>
      )}
    </div>
  );
}

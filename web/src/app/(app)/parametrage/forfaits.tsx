"use client";

import { useEffect, useState } from "react";
import { Plus, Trash2 } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn, filtrerDecimal, formatDateHeure, formatNombre, lireNombre } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

type Tranche = { aPartirDe: number; prix: number };
type Vue = { modifiable: boolean; modifiePar: string | null; modifieLe: string | null; tranches: Tranche[] };
type Ligne = { de: string; prix: string };

const fr = (n: number) => String(n).replace(".", ",");

/** Grille unique des forfaits du devis, par tranche de surface totale, pour tous les types de construction. */
export function ForfaitsParam() {
  const [vue, setVue] = useState<Vue | null>(null);
  const [lignes, setLignes] = useState<Ligne[]>([]);
  const [message, setMessage] = useState<{ ok: boolean; texte: string } | null>(null);

  const charger = (r: Vue) => { setVue(r); setLignes(r.tranches.map((t) => ({ de: fr(t.aPartirDe), prix: fr(t.prix) }))); };
  useEffect(() => { api<Vue>("/parametres/forfaits").then(charger).catch(() => undefined); }, []);
  if (!vue) return null;

  const maj = (l: Ligne[]) => { setLignes(l); setMessage(null); };
  // Borne haute affichée : jusqu'au seuil de la tranche suivante (non compris), dans l'ordre des surfaces.
  const tries = [...lignes.map((l, i) => ({ i, de: lireNombre(l.de) }))].sort((a, b) => a.de - b.de);
  const jusqua = (i: number) => {
    const k = tries.findIndex((t) => t.i === i);
    return k >= 0 && k < tries.length - 1 ? `jusqu'à ${formatNombre(tries[k + 1].de)} m² non compris` : "et au-delà";
  };
  async function enregistrer() {
    try {
      charger(await api<Vue>("/parametres/forfaits", { method: "PUT", body: JSON.stringify({ tranches: lignes.map((l) => ({ aPartirDe: lireNombre(l.de), prix: lireNombre(l.prix) })) }) }));
      setMessage({ ok: true, texte: "Grille enregistrée. Elle s'applique aux brouillons à leur prochain enregistrement ; les devis émis gardent leur forfait." });
    } catch (e) {
      setMessage({ ok: false, texte: e instanceof ApiError ? e.message : "Enregistrement impossible." });
    }
  }

  return (
    <div className="rounded-xl border border-bordure bg-white p-6">
      <h2 className="text-[15px] font-bold">Devis : grille des forfaits</h2>
      <p className="mt-1 text-[13px] text-secondaire">
        Forfait en DH/m² ajouté au prix de chaque dalle, choisi selon la surface totale du devis (somme des dalles), quel que soit le type de construction.
        La première tranche commence à 0 m².
      </p>
      <div className="mt-4 space-y-2">
        <div className="grid grid-cols-[1fr_1fr_auto] gap-3 text-[12px] font-semibold text-secondaire"><span>À partir de (m²)</span><span>Forfait (DH/m²)</span><span className="w-9" /></div>
        {lignes.map((l, i) => (
          <div key={i}>
            <div className="grid grid-cols-[1fr_1fr_auto] items-center gap-3">
              <Input className="h-9" inputMode="decimal" disabled={!vue.modifiable} aria-label={`Surface de départ de la tranche ${i + 1}`} value={l.de}
                onChange={(e) => maj(lignes.map((x, j) => (j === i ? { ...x, de: filtrerDecimal(e.target.value) } : x)))} />
              <Input className="h-9" inputMode="decimal" disabled={!vue.modifiable} aria-label={`Forfait de la tranche ${i + 1}`} value={l.prix}
                onChange={(e) => maj(lignes.map((x, j) => (j === i ? { ...x, prix: filtrerDecimal(e.target.value) } : x)))} />
              {vue.modifiable ? <Button variant="ghost" size="icon" aria-label={`Supprimer la tranche ${i + 1}`} onClick={() => maj(lignes.filter((_, j) => j !== i))}><Trash2 size={15} /></Button> : <span className="w-9" />}
            </div>
            <div className="mt-0.5 text-[12px] text-attenue">De {l.de || "?"} m² {jusqua(i)}</div>
          </div>
        ))}
      </div>
      {vue.modifiePar && <p className="mt-3 text-[12px] text-attenue">Modifié par {vue.modifiePar} le {formatDateHeure(vue.modifieLe)}.</p>}
      {vue.modifiable ? (
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <Button variant="outline" onClick={() => maj([...lignes, { de: "", prix: "" }])}><Plus size={15} />Ajouter une tranche</Button>
          <Button onClick={enregistrer}>Enregistrer</Button>
          {message && <span role={message.ok ? "status" : "alert"} className={cn("text-[13px]", message.ok ? "text-vert-texte" : "text-rouge-texte")}>{message.texte}</span>}
        </div>
      ) : <p className="mt-4 text-[13px] text-secondaire">Consultation seule : la grille est modifiée par le directeur, le chef de projet ou le responsable administratif.</p>}
    </div>
  );
}

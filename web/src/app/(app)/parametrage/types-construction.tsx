"use client";

import { useEffect, useState } from "react";
import { ArrowDown, ArrowUp, Plus, Trash2 } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn, filtrerDecimal, lireNombre } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

type Type = { code: string | null; libelle: string; actif: boolean; projets: number; forfait: number | null };
/** Forfait en cours de saisie, en texte (virgule décimale). */
type Ligne = Type & { forfaitTexte: string };
const versLigne = (t: Type): Ligne => ({ ...t, forfaitTexte: t.forfait ? String(t.forfait).replace(".", ",") : "" });
type Vue = { modifiable: boolean; types: Type[] };

/** Types de construction proposés à la création d'un projet, dans l'ordre de la liste déroulante. Un type porté par un projet se désactive, il ne se supprime pas. */
export function TypesConstructionParam({ integre, onChange }: { integre?: boolean; onChange?: () => void } = {}) {
  const [vue, setVue] = useState<Vue | null>(null);
  const [liste, setListe] = useState<Ligne[]>([]);
  const [message, setMessage] = useState<{ ok: boolean; texte: string } | null>(null);

  const charger = (r: Vue) => { setVue(r); setListe(r.types.map(versLigne)); };
  useEffect(() => { api<Vue>("/parametres/types-construction").then(charger).catch(() => undefined); }, []);
  if (!vue) return null;

  const maj = (l: Ligne[]) => { setListe(l); setMessage(null); };
  const deplacer = (i: number, sens: -1 | 1) => { const l = [...liste]; [l[i], l[i + sens]] = [l[i + sens], l[i]]; maj(l); };
  async function enregistrer() {
    try {
      charger(await api<Vue>("/parametres/types-construction", { method: "PUT", body: JSON.stringify({ types: liste.map(({ code, libelle, actif, forfaitTexte }) => ({ code, libelle, actif, forfait: lireNombre(forfaitTexte) || null })) }) }));
      setMessage({ ok: true, texte: "Types enregistrés." });
      onChange?.();
    } catch (e) {
      setMessage({ ok: false, texte: e instanceof ApiError ? e.message : "Enregistrement impossible." });
    }
  }

  return (
    <div className={cn(!integre && "rounded-xl border border-bordure bg-white p-6")}>
      <h2 className="text-[15px] font-bold">Projets : types de construction</h2>
      <p className="mt-1 text-[13px] text-secondaire">
        Proposés dans la liste déroulante du projet, dans cet ordre. Un type déjà porté par un projet ne se supprime pas : décochez « Proposé ».
        Le forfait fixe (DH/m²) donne le même prix quelle que soit la surface, par exemple pour un hangar. Laissé vide, le forfait suit la grille par surface. Un forfait propre au projet passe avant celui du type.
      </p>
      <div className="mt-4 space-y-1.5">
        {liste.map((t, i) => (
          <div key={t.code ?? `n${i}`} className="flex items-center gap-2">
            <Input className="h-9" disabled={!vue.modifiable} aria-label={`Libellé du type ${i + 1}`} value={t.libelle}
              onChange={(e) => maj(liste.map((x, j) => (j === i ? { ...x, libelle: e.target.value } : x)))} />
            <Input className="h-9 w-28 shrink-0 text-right" disabled={!vue.modifiable} inputMode="decimal" placeholder="Grille" aria-label={`Forfait fixe en DH/m², ${t.libelle || `type ${i + 1}`}`}
              value={t.forfaitTexte} onChange={(e) => maj(liste.map((x, j) => (j === i ? { ...x, forfaitTexte: filtrerDecimal(e.target.value) } : x)))} />
            <span className="w-20 shrink-0 text-right text-[12px] text-attenue">{t.projets > 0 ? `${t.projets} projet${t.projets > 1 ? "s" : ""}` : ""}</span>
            <label className="flex shrink-0 cursor-pointer items-center gap-2 text-[13px] text-secondaire">
              <input type="checkbox" className="h-4 w-4 accent-marine" disabled={!vue.modifiable} checked={t.actif}
                onChange={(e) => maj(liste.map((x, j) => (j === i ? { ...x, actif: e.target.checked } : x)))} />
              Proposé
            </label>
            {vue.modifiable && (
              <>
                <Button variant="ghost" size="icon" aria-label={`Monter ${t.libelle}`} disabled={i === 0} onClick={() => deplacer(i, -1)}><ArrowUp size={15} /></Button>
                <Button variant="ghost" size="icon" aria-label={`Descendre ${t.libelle}`} disabled={i === liste.length - 1} onClick={() => deplacer(i, 1)}><ArrowDown size={15} /></Button>
                <Button variant="ghost" size="icon" aria-label={`Supprimer ${t.libelle}`} disabled={t.projets > 0}
                  title={t.projets > 0 ? "Porté par des projets : décochez « Proposé »" : undefined} onClick={() => maj(liste.filter((_, j) => j !== i))}><Trash2 size={15} /></Button>
              </>
            )}
          </div>
        ))}
      </div>
      {vue.modifiable ? (
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <Button variant="outline" onClick={() => maj([...liste, { code: null, libelle: "", actif: true, projets: 0, forfait: null, forfaitTexte: "" }])}><Plus size={15} />Ajouter un type</Button>
          <Button onClick={enregistrer}>Enregistrer</Button>
          {message && <span role={message.ok ? "status" : "alert"} className={cn("text-[13px]", message.ok ? "text-vert-texte" : "text-rouge-texte")}>{message.texte}</span>}
        </div>
      ) : <p className="mt-4 text-[13px] text-secondaire">Consultation seule : les types sont modifiés par le directeur ou le chef de projet.</p>}
    </div>
  );
}

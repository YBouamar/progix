"use client";

import { useEffect, useState } from "react";
import { ArrowDown, ArrowUp, Plus, Trash2 } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

type Type = { code: string | null; libelle: string; actif: boolean; projets: number };
type Vue = { modifiable: boolean; types: Type[] };

/** Types de construction proposés à la création d'un projet, dans l'ordre de la liste déroulante. Un type porté par un projet se désactive, il ne se supprime pas. */
export function TypesConstructionParam() {
  const [vue, setVue] = useState<Vue | null>(null);
  const [liste, setListe] = useState<Type[]>([]);
  const [message, setMessage] = useState<{ ok: boolean; texte: string } | null>(null);

  const charger = (r: Vue) => { setVue(r); setListe(r.types); };
  useEffect(() => { api<Vue>("/parametres/types-construction").then(charger).catch(() => undefined); }, []);
  if (!vue) return null;

  const maj = (l: Type[]) => { setListe(l); setMessage(null); };
  const deplacer = (i: number, sens: -1 | 1) => { const l = [...liste]; [l[i], l[i + sens]] = [l[i + sens], l[i]]; maj(l); };
  async function enregistrer() {
    try {
      charger(await api<Vue>("/parametres/types-construction", { method: "PUT", body: JSON.stringify({ types: liste.map(({ code, libelle, actif }) => ({ code, libelle, actif })) }) }));
      setMessage({ ok: true, texte: "Types enregistrés." });
    } catch (e) {
      setMessage({ ok: false, texte: e instanceof ApiError ? e.message : "Enregistrement impossible." });
    }
  }

  return (
    <div className="rounded-xl border border-bordure bg-white p-6">
      <h2 className="text-[15px] font-bold">Projets : types de construction</h2>
      <p className="mt-1 text-[13px] text-secondaire">
        Proposés dans la liste déroulante du projet, dans cet ordre. Un type déjà porté par un projet ne se supprime pas : décochez « Proposé ».
        Le type ne change pas le prix : le forfait vient de la grille par surface.
      </p>
      <div className="mt-4 space-y-1.5">
        {liste.map((t, i) => (
          <div key={t.code ?? `n${i}`} className="flex items-center gap-2">
            <Input className="h-9" disabled={!vue.modifiable} aria-label={`Libellé du type ${i + 1}`} value={t.libelle}
              onChange={(e) => maj(liste.map((x, j) => (j === i ? { ...x, libelle: e.target.value } : x)))} />
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
          <Button variant="outline" onClick={() => maj([...liste, { code: null, libelle: "", actif: true, projets: 0 }])}><Plus size={15} />Ajouter un type</Button>
          <Button onClick={enregistrer}>Enregistrer</Button>
          {message && <span role={message.ok ? "status" : "alert"} className={cn("text-[13px]", message.ok ? "text-vert-texte" : "text-rouge-texte")}>{message.texte}</span>}
        </div>
      ) : <p className="mt-4 text-[13px] text-secondaire">Consultation seule : les types sont modifiés par le directeur ou le chef de projet.</p>}
    </div>
  );
}

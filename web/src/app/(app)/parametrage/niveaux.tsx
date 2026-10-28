"use client";

import { useEffect, useState } from "react";
import { ArrowDown, ArrowUp, Plus, Trash2 } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

type Niveau = { valeur: number | null; libelle: string; actif: boolean; dalles: number };
type Vue = { modifiable: boolean; niveaux: Niveau[] };

/** Niveaux proposés pour les dalles : libellés libres, dans l'ordre d'affichage. Un niveau porté par une dalle se désactive, il ne se supprime pas. */
export function NiveauxParam() {
  const [vue, setVue] = useState<Vue | null>(null);
  const [liste, setListe] = useState<Niveau[]>([]);
  const [message, setMessage] = useState<{ ok: boolean; texte: string } | null>(null);

  const charger = (r: Vue) => { setVue(r); setListe(r.niveaux); };
  useEffect(() => { api<Vue>("/parametres/niveaux").then(charger).catch(() => undefined); }, []);
  if (!vue) return null;

  const maj = (l: Niveau[]) => { setListe(l); setMessage(null); };
  const deplacer = (i: number, sens: -1 | 1) => {
    const l = [...liste];
    [l[i], l[i + sens]] = [l[i + sens], l[i]];
    maj(l);
  };
  async function enregistrer() {
    try {
      charger(await api<Vue>("/parametres/niveaux", { method: "PUT", body: JSON.stringify({ niveaux: liste.map(({ valeur, libelle, actif }) => ({ valeur, libelle, actif })) }) }));
      setMessage({ ok: true, texte: "Niveaux enregistrés." });
    } catch (e) {
      setMessage({ ok: false, texte: e instanceof ApiError ? e.message : "Enregistrement impossible." });
    }
  }

  return (
    <div className="rounded-xl border border-bordure bg-white p-6">
      <h2 className="text-[15px] font-bold">Dalles : niveaux</h2>
      <p className="mt-1 text-[13px] text-secondaire">
        Niveaux proposés à la saisie d&apos;une dalle, dans cet ordre. Un niveau déjà porté par une dalle ne se supprime pas : décochez « Proposé ».
        Un libellé modifié s&apos;applique aux dalles existantes ; les documents déjà émis gardent le leur.
      </p>
      <div className="mt-4 max-h-[420px] space-y-1.5 overflow-y-auto pr-1">
        {liste.map((n, i) => (
          <div key={n.valeur ?? `n${i}`} className="flex items-center gap-2">
            <Input className="h-9" disabled={!vue.modifiable} aria-label={`Libellé du niveau ${i + 1}`} value={n.libelle}
              onChange={(e) => maj(liste.map((x, j) => (j === i ? { ...x, libelle: e.target.value } : x)))} />
            <span className="w-20 shrink-0 text-right text-[12px] text-attenue">{n.dalles > 0 ? `${n.dalles} dalle${n.dalles > 1 ? "s" : ""}` : ""}</span>
            <label className="flex shrink-0 cursor-pointer items-center gap-2 text-[13px] text-secondaire">
              <input type="checkbox" className="h-4 w-4 accent-marine" disabled={!vue.modifiable} checked={n.actif}
                onChange={(e) => maj(liste.map((x, j) => (j === i ? { ...x, actif: e.target.checked } : x)))} />
              Proposé
            </label>
            {vue.modifiable && (
              <>
                <Button variant="ghost" size="icon" aria-label={`Monter ${n.libelle}`} disabled={i === 0} onClick={() => deplacer(i, -1)}><ArrowUp size={15} /></Button>
                <Button variant="ghost" size="icon" aria-label={`Descendre ${n.libelle}`} disabled={i === liste.length - 1} onClick={() => deplacer(i, 1)}><ArrowDown size={15} /></Button>
                <Button variant="ghost" size="icon" aria-label={`Supprimer ${n.libelle}`} disabled={n.dalles > 0}
                  title={n.dalles > 0 ? "Porté par des dalles : décochez « Proposé »" : undefined} onClick={() => maj(liste.filter((_, j) => j !== i))}><Trash2 size={15} /></Button>
              </>
            )}
          </div>
        ))}
      </div>
      {vue.modifiable ? (
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <Button variant="outline" onClick={() => maj([...liste, { valeur: null, libelle: "", actif: true, dalles: 0 }])}><Plus size={15} />Ajouter un niveau</Button>
          <Button onClick={enregistrer}>Enregistrer</Button>
          {message && <span role={message.ok ? "status" : "alert"} className={cn("text-[13px]", message.ok ? "text-vert-texte" : "text-rouge-texte")}>{message.texte}</span>}
        </div>
      ) : <p className="mt-4 text-[13px] text-secondaire">Consultation seule : les niveaux sont modifiés par le directeur ou le chef de projet.</p>}
    </div>
  );
}

"use client";

import { useEffect, useState } from "react";
import { Plus, Trash2 } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

type Categorie = { id: string | null; libelle: string; actif: boolean };
type Vue = { modifiable: boolean; categories: Categorie[] };

/** Catégories proposées à la saisie d'une dépense de dalle. */
export function CategoriesDepenseParam() {
  const [vue, setVue] = useState<Vue | null>(null);
  const [liste, setListe] = useState<Categorie[]>([]);
  const [message, setMessage] = useState<{ ok: boolean; texte: string } | null>(null);

  const charger = (r: Vue) => { setVue(r); setListe(r.categories); };
  useEffect(() => { api<Vue>("/parametres/categories-depense").then(charger).catch(() => undefined); }, []);
  if (!vue) return null;

  const maj = (l: Categorie[]) => { setListe(l); setMessage(null); };
  async function enregistrer() {
    try {
      charger(await api<Vue>("/parametres/categories-depense", { method: "PUT", body: JSON.stringify({ categories: liste }) }));
      setMessage({ ok: true, texte: "Catégories enregistrées." });
    } catch (e) {
      setMessage({ ok: false, texte: e instanceof ApiError ? e.message : "Enregistrement impossible." });
    }
  }

  return (
    <div className="mt-5 rounded-xl border border-bordure bg-white p-6">
      <h2 className="text-[15px] font-bold">Dépenses de dalle : catégories</h2>
      <p className="mt-1 text-[13px] text-secondaire">
        Chaque dépense de dalle se classe dans une de ces catégories. Les dépenses déjà saisies gardent leur catégorie, même si elle est retirée ici.
      </p>
      <div className="mt-4 space-y-2">
        {liste.map((c, i) => (
          <div key={c.id ?? `n${i}`} className="flex items-center gap-3">
            <Input className="h-9" disabled={!vue.modifiable} aria-label={`Libellé de la catégorie ${i + 1}`} value={c.libelle}
              onChange={(e) => maj(liste.map((x, j) => (j === i ? { ...x, libelle: e.target.value } : x)))} />
            <label className="flex shrink-0 cursor-pointer items-center gap-2 text-[13px] text-secondaire">
              <input type="checkbox" className="h-4 w-4 accent-marine" disabled={!vue.modifiable} checked={c.actif}
                onChange={(e) => maj(liste.map((x, j) => (j === i ? { ...x, actif: e.target.checked } : x)))} />
              Proposée
            </label>
            {vue.modifiable && <Button variant="ghost" size="icon" aria-label={`Supprimer ${c.libelle}`} onClick={() => maj(liste.filter((_, j) => j !== i))}><Trash2 size={15} /></Button>}
          </div>
        ))}
      </div>
      {vue.modifiable ? (
        <div className="mt-4 flex items-center gap-3">
          <Button variant="outline" onClick={() => maj([...liste, { id: null, libelle: "", actif: true }])}><Plus size={15} />Ajouter une catégorie</Button>
          <Button onClick={enregistrer}>Enregistrer</Button>
          {message && <span role={message.ok ? "status" : "alert"} className={cn("text-[13px]", message.ok ? "text-vert-texte" : "text-rouge-texte")}>{message.texte}</span>}
        </div>
      ) : <p className="mt-4 text-[13px] text-secondaire">Consultation seule.</p>}
    </div>
  );
}

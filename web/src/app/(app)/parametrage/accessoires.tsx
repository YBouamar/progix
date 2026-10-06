"use client";

import { useEffect, useState } from "react";
import { Plus, Trash2 } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

type Accessoire = { id: string | null; libelle: string; unite: string | null; actif: boolean };
type Vue = { modifiable: boolean; accessoires: Accessoire[] };

/** Accessoires proposés d'office dans chaque commande usine. */
export function AccessoiresParam() {
  const [vue, setVue] = useState<Vue | null>(null);
  const [liste, setListe] = useState<Accessoire[]>([]);
  const [message, setMessage] = useState<{ ok: boolean; texte: string } | null>(null);

  const charger = (r: Vue) => { setVue(r); setListe(r.accessoires); };
  useEffect(() => { api<Vue>("/parametres/accessoires").then(charger).catch(() => undefined); }, []);
  if (!vue) return null;

  const maj = (l: Accessoire[]) => { setListe(l); setMessage(null); };
  async function enregistrer() {
    try {
      charger(await api<Vue>("/parametres/accessoires", { method: "PUT", body: JSON.stringify({ accessoires: liste }) }));
      setMessage({ ok: true, texte: "Accessoires enregistrés." });
    } catch (e) {
      setMessage({ ok: false, texte: e instanceof ApiError ? e.message : "Enregistrement impossible." });
    }
  }

  return (
    <div className="mt-5 rounded-xl border border-bordure bg-white p-6">
      <h2 className="text-[15px] font-bold">Commande usine : accessoires</h2>
      <p className="mt-1 text-[13px] text-secondaire">
        Ces accessoires sont proposés dans chaque commande usine. L&apos;utilisateur y saisit seulement les quantités, dans l&apos;unité indiquée ici.
      </p>
      <datalist id="unites-accessoires">{["Unité", "Kg", "L", "m", "Rouleau", "Boîte"].map((u) => <option key={u} value={u} />)}</datalist>
      <div className="mt-4 space-y-2">
        {liste.map((a, i) => (
          <div key={a.id ?? `n${i}`} className="flex items-center gap-3">
            <Input className="h-9" disabled={!vue.modifiable} aria-label={`Libellé de l'accessoire ${i + 1}`} value={a.libelle}
              onChange={(e) => maj(liste.map((x, j) => (j === i ? { ...x, libelle: e.target.value } : x)))} />
            <Input className="h-9 w-32 shrink-0" list="unites-accessoires" disabled={!vue.modifiable} placeholder="Unité" aria-label={`Unité de ${a.libelle}`} value={a.unite ?? ""}
              onChange={(e) => maj(liste.map((x, j) => (j === i ? { ...x, unite: e.target.value } : x)))} />
            <label className="flex shrink-0 cursor-pointer items-center gap-2 text-[13px] text-secondaire">
              <input type="checkbox" className="h-4 w-4 accent-marine" disabled={!vue.modifiable} checked={a.actif}
                onChange={(e) => maj(liste.map((x, j) => (j === i ? { ...x, actif: e.target.checked } : x)))} />
              Proposé
            </label>
            {vue.modifiable && <Button variant="ghost" size="icon" aria-label={`Supprimer ${a.libelle}`} onClick={() => maj(liste.filter((_, j) => j !== i))}><Trash2 size={15} /></Button>}
          </div>
        ))}
      </div>
      {vue.modifiable ? (
        <div className="mt-4 flex items-center gap-3">
          <Button variant="outline" onClick={() => maj([...liste, { id: null, libelle: "", unite: null, actif: true }])}><Plus size={15} />Ajouter un accessoire</Button>
          <Button onClick={enregistrer}>Enregistrer</Button>
          {message && <span role={message.ok ? "status" : "alert"} className={cn("text-[13px]", message.ok ? "text-vert-texte" : "text-rouge-texte")}>{message.texte}</span>}
        </div>
      ) : <p className="mt-4 text-[13px] text-secondaire">Consultation seule.</p>}
    </div>
  );
}

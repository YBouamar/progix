"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { api, ApiError } from "@/lib/api";
import { cn } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Select } from "@/components/ui/input";

type Personne = { id: string; nom: string };
type Dalle = { id: string; projetId: string; reference: string; projet: string; bloque: boolean; emplacement: string; dessinateurId: string | null; etat: string; etudePlanifiee: string | null; enRetard: boolean };
type Vue = { personnes: Personne[]; dalles: Dalle[] };
const ETATS: Record<string, [string, "neutre" | "orange" | "vert"]> = { lancer: ["À lancer", "neutre"], cours: ["En cours", "orange"], terminee: ["Terminée", "vert"] };

export default function RepartitionDessinateurs() {
  const [v, setV] = useState<Vue | null>(null);
  const [erreur, setErreur] = useState("");
  const [survol, setSurvol] = useState<string | null>(null);
  const glisse = useRef<string | null>(null);

  const charger = useCallback(() => api<Vue>("/repartition/dessinateurs").then(setV).catch((e) => setErreur(e instanceof ApiError ? e.message : "Chargement impossible.")), []);
  useEffect(() => { charger(); }, [charger]);

  const affecter = async (d: Dalle, colonne: string) => {
    const cible = colonne === "aucun" ? null : colonne;
    if (cible === d.dessinateurId) return;
    setErreur("");
    try { await api(`/projets/${d.projetId}/dalles/${d.id}/dessinateur`, { method: "PUT", body: JSON.stringify({ userId: cible }) }); }
    catch (e) { setErreur(e instanceof ApiError ? e.message : "Affectation impossible."); }
    await charger();
  };

  const planifier = async (d: Dalle, date: string) => {
    setErreur("");
    try { await api(`/projets/${d.projetId}/dalles/${d.id}/etude`, { method: "PUT", body: JSON.stringify({ date: date || null }) }); }
    catch (e) { setErreur(e instanceof ApiError ? e.message : "Planification impossible."); }
    await charger();
  };

  if (!v) return erreur ? <div role="alert" className="rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div> : null;
  const colonnes = [{ id: "aucun", nom: "Non assignées" }, ...v.personnes];

  return (
    <div>
      <p className="mb-4 max-w-4xl text-[13px] text-secondaire">
        Une carte par dalle dont les plans ne sont pas encore validés par le BCT, triée par date d&apos;étude planifiée dans chaque colonne. Glissez une carte dans la colonne d&apos;un dessinateur, ou choisissez-le dans la liste de la carte.
        Une dalle a un seul dessinateur ; il fait ensuite avancer l&apos;étude depuis le suivi de la dalle.
      </p>
      {erreur && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}
      <div className="flex gap-4 overflow-x-auto pb-4">
        {colonnes.map((c) => {
          const cartes = v.dalles.filter((d) => (d.dessinateurId ?? "aucun") === c.id);
          return (
            <div key={c.id} onDragOver={(e) => { e.preventDefault(); setSurvol(c.id); }} onDragLeave={() => setSurvol((s) => (s === c.id ? null : s))}
              onDrop={(e) => { e.preventDefault(); setSurvol(null); const d = v.dalles.find((x) => x.id === glisse.current); glisse.current = null; if (d) affecter(d, c.id); }}
              className={cn("w-72 shrink-0 rounded-xl border p-3", survol === c.id ? "border-marine bg-white" : "border-bordure", c.id === "aucun" ? "bg-orange-fond/60" : "bg-fond")}>
              <div className="mb-3 flex items-center justify-between gap-2 px-1">
                <span className={cn("font-display font-bold", c.id === "aucun" && "text-orange-texte")}>{c.nom}</span>
                <span className="rounded-full bg-white px-2 py-0.5 font-display text-[12px] font-bold">{cartes.length}</span>
              </div>
              <div className="space-y-2.5">
                {cartes.map((d) => (
                  <div key={d.id} draggable onDragStart={(e) => { glisse.current = d.id; e.dataTransfer.setData("text/plain", d.reference); }}
                    className="cursor-grab rounded-lg border border-bordure bg-white p-3 active:cursor-grabbing">
                    <div className="flex items-start justify-between gap-2">
                      <Link href={`/projets/${d.projetId}`} className="font-display text-[13px] font-bold text-marine hover:underline">{d.reference}</Link>
                      <Badge ton={ETATS[d.etat]?.[1] ?? "neutre"}>{ETATS[d.etat]?.[0] ?? d.etat}</Badge>
                    </div>
                    <div className="mt-1 text-[13px]">{d.projet}{d.emplacement ? ` · ${d.emplacement}` : ""}</div>
                    {d.bloque && <div className="text-[12px] text-rouge-texte">Projet bloqué</div>}
                    <label className={cn("mt-2 flex items-center gap-2 text-[12px]", d.enRetard ? "font-semibold text-rouge-texte" : "text-secondaire")}>
                      Étude{d.enRetard ? ", en retard" : ""}
                      <input type="date" className="h-8 flex-1 rounded-md border border-bordure-forte bg-white px-2 font-display text-[12px] text-texte" aria-label={`Date d'étude planifiée, ${d.reference}`}
                        value={d.etudePlanifiee ?? ""} onChange={(e) => planifier(d, e.target.value)} />
                    </label>
                    <Select className="mt-2 h-9 text-[13px]" aria-label={`Affecter la dalle ${d.reference}`} value={d.dessinateurId ?? "aucun"} onChange={(e) => affecter(d, e.target.value)}>
                      <option value="aucun">Non assignée</option>{v.personnes.map((p) => <option key={p.id} value={p.id}>{p.nom}</option>)}
                    </Select>
                  </div>
                ))}
                {cartes.length === 0 && <div className="rounded-lg border border-dashed border-bordure-forte px-3 py-6 text-center text-[12px] text-attenue">{c.id === "aucun" ? "Toutes les dalles ont un dessinateur" : "Déposez une dalle ici"}</div>}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

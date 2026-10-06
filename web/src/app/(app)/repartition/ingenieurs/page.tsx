"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { api, ApiError } from "@/lib/api";
import { cn, initiales } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { TON_STATUT } from "../../projets/types";

type Personne = { id: string; nom: string };
type Projet = { id: string; code: string; libelle: string; statut: string; statutLabel: string; bloque: boolean; dalles: number; realisees: number; ingenieurs: { affectationId: string; userId: string }[] };
type Vue = { personnes: Personne[]; projets: Projet[] };

export default function RepartitionIngenieurs() {
  const [v, setV] = useState<Vue | null>(null);
  const [erreur, setErreur] = useState("");
  const [survol, setSurvol] = useState<string | null>(null);
  const glisse = useRef<{ projetId: string; depuis: string } | null>(null);

  const charger = useCallback(() => api<Vue>("/repartition/ingenieurs").then(setV).catch((e) => setErreur(e instanceof ApiError ? e.message : "Chargement impossible.")), []);
  useEffect(() => { charger(); }, [charger]);

  const agir = async (f: () => Promise<unknown>) => {
    setErreur("");
    try { await f(); } catch (e) { setErreur(e instanceof ApiError ? e.message : "Modification impossible."); }
    await charger();
  };
  const ajouter = (p: Projet, userId: string) => api(`/projets/${p.id}/equipe`, { method: "POST", body: JSON.stringify({ fonction: "ingenieur", userId }) });
  const retirer = (p: Projet, userId: string) => {
    const a = p.ingenieurs.find((x) => x.userId === userId);
    return a ? api(`/projets/${p.id}/equipe/${a.affectationId}/retirer`, { method: "POST" }) : Promise.resolve();
  };
  const basculer = (p: Projet, userId: string) => agir(() => (p.ingenieurs.some((x) => x.userId === userId) ? retirer(p, userId) : ajouter(p, userId)));
  // Déposer une carte : vers un ingénieur, il s'ajoute (et remplace celui de la colonne d'origine) ; vers « Non assignés », tous sont retirés.
  const deposer = (colonne: string) => {
    const g = glisse.current; glisse.current = null; setSurvol(null);
    const p = v?.projets.find((x) => x.id === g?.projetId);
    if (!g || !p || g.depuis === colonne) return;
    agir(async () => {
      if (colonne === "aucun") { for (const a of p.ingenieurs) await retirer(p, a.userId); return; }
      if (!p.ingenieurs.some((x) => x.userId === colonne)) await ajouter(p, colonne);
      if (g.depuis !== "aucun") await retirer(p, g.depuis);
    });
  };

  if (!v) return erreur ? <div role="alert" className="rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div> : null;
  const colonnes = [{ id: "aucun", nom: "Non assignés" }, ...v.personnes];

  return (
    <div>
      <p className="mb-4 max-w-4xl text-[13px] text-secondaire">
        Une carte par projet en cours. Un projet peut être suivi par plusieurs ingénieurs : sa carte apparaît alors dans chaque colonne.
        Glissez une carte vers une colonne, ou utilisez les initiales sur la carte pour ajouter et retirer. Chaque changement est enregistré dans l&apos;historique de l&apos;équipe du projet.
      </p>
      {erreur && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}
      <div className="flex gap-4 overflow-x-auto pb-4">
        {colonnes.map((c) => {
          const cartes = v.projets.filter((p) => (c.id === "aucun" ? p.ingenieurs.length === 0 : p.ingenieurs.some((x) => x.userId === c.id)));
          return (
            <div key={c.id} onDragOver={(e) => { e.preventDefault(); setSurvol(c.id); }} onDragLeave={() => setSurvol((s) => (s === c.id ? null : s))} onDrop={(e) => { e.preventDefault(); deposer(c.id); }}
              className={cn("w-72 shrink-0 rounded-xl border p-3", survol === c.id ? "border-marine bg-white" : "border-bordure", c.id === "aucun" ? "bg-orange-fond/60" : "bg-fond")}>
              <div className="mb-3 flex items-center justify-between gap-2 px-1">
                <span className={cn("font-display font-bold", c.id === "aucun" && "text-orange-texte")}>{c.nom}</span>
                <span className="rounded-full bg-white px-2 py-0.5 font-display text-[12px] font-bold">{cartes.length}</span>
              </div>
              <div className="space-y-2.5">
                {cartes.map((p) => (
                  <div key={p.id} draggable onDragStart={(e) => { glisse.current = { projetId: p.id, depuis: c.id }; e.dataTransfer.setData("text/plain", p.code); }}
                    className="cursor-grab rounded-lg border border-bordure bg-white p-3 active:cursor-grabbing">
                    <div className="flex items-start justify-between gap-2">
                      <Link href={`/projets/${p.id}`} className="font-display text-[13px] font-bold text-marine hover:underline">{p.code}</Link>
                      <Badge ton={TON_STATUT[p.statut] ?? "neutre"}>{p.statutLabel}</Badge>
                    </div>
                    <div className="mt-1 text-[13px] font-semibold">{p.libelle}</div>
                    <div className="mt-0.5 text-[12px] text-secondaire">{p.dalles} dalle(s) · {p.realisees} réalisée(s){p.bloque ? " · projet bloqué" : ""}</div>
                    <div className="mt-2 flex flex-wrap items-center gap-1.5">
                      <span className="text-[12px] text-attenue">Ingénieurs</span>
                      {v.personnes.map((i) => {
                        const on = p.ingenieurs.some((x) => x.userId === i.id);
                        return (
                          <button key={i.id} title={i.nom} aria-pressed={on} aria-label={`${on ? "Retirer" : "Ajouter"} ${i.nom} du projet ${p.code}`} onClick={() => basculer(p, i.id)}
                            className={cn("h-7 min-w-7 cursor-pointer rounded-full border px-1.5 font-display text-[11px] font-bold", on ? "border-marine bg-marine text-white" : "border-bordure-forte bg-white text-secondaire hover:bg-fond")}>{initiales(i.nom)}</button>
                        );
                      })}
                    </div>
                  </div>
                ))}
                {cartes.length === 0 && <div className="rounded-lg border border-dashed border-bordure-forte px-3 py-6 text-center text-[12px] text-attenue">{c.id === "aucun" ? "Tous les projets ont un ingénieur" : "Déposez un projet ici"}</div>}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { cn, formatDateHeure } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

type Point = { code: string; section: string; rubrique: string; libelle: string; resultat: string; observation: string | null };
type Vue = { modifiable: boolean; verrou: string | null; saisiPar: string | null; saisiLe: string | null; nonConformes: number; points: Point[] };
const CHOIX: [string, string, string][] = [["c", "C", "bg-vert-fond text-vert-texte"], ["nc", "NC", "bg-rouge-fond text-rouge-texte"], ["na", "NA", "bg-neutre-fond text-neutre-texte"]];

/** Fiche d'auto-contrôle du plancher (étape 6) : chaque point critique est noté conforme, non conforme ou non applicable. */
export function FicheAutoControle({ projetId, dalleId }: { projetId: string; dalleId: string }) {
  const base = `/projets/${projetId}/dalles/${dalleId}/auto-controle`;
  const [vue, setVue] = useState<Vue | null>(null);
  const [points, setPoints] = useState<Point[]>([]);
  const [modifie, setModifie] = useState(false);
  const [message, setMessage] = useState<{ ok: boolean; texte: string } | null>(null);

  const charger = (r: Vue) => { setVue(r); setPoints(r.points); setModifie(false); };
  useEffect(() => { api<Vue>(base).then(charger).catch(() => setVue(null)); }, [base]);
  if (!vue) return null;

  const maj = (code: string, x: Partial<Point>) => { setPoints(points.map((p) => (p.code === code ? { ...p, ...x } : p))); setModifie(true); setMessage(null); };
  const enregistrer = async () => {
    try { charger(await api<Vue>(base, { method: "PUT", body: JSON.stringify({ points: points.map((p) => ({ code: p.code, resultat: p.resultat, observation: p.observation })) }) })); setMessage({ ok: true, texte: "Fiche enregistrée." }); }
    catch (e) { setMessage({ ok: false, texte: e instanceof ApiError ? e.message : "Enregistrement impossible." }); }
  };
  const restants = points.filter((p) => !p.resultat).length;
  const nc = points.filter((p) => p.resultat === "nc").length;
  let section = "", rubrique = "";

  return (
    <div className="mt-3 rounded-lg border border-bordure bg-white p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="font-semibold">Fiche d&apos;auto-contrôle du plancher</div>
        <div className="text-[12px] text-secondaire">C : conforme · NC : non conforme · NA : non applicable</div>
      </div>
      <div className="mt-3 space-y-1">
        {points.map((p) => {
          const titreSection = p.section !== section ? (section = p.section, rubrique = "", p.section) : null;
          const titreRubrique = p.rubrique && p.rubrique !== rubrique ? (rubrique = p.rubrique, p.rubrique) : null;
          return (
            <div key={p.code}>
              {titreSection && <div className="mt-3 border-b border-bordure pb-1 font-display text-[13px] font-bold uppercase tracking-wide text-marine">{titreSection}</div>}
              {titreRubrique && <div className="mt-2 text-[12px] font-semibold text-secondaire">{titreRubrique}</div>}
              <div className="flex flex-wrap items-center gap-2 py-1.5 text-[13px]">
                <span className="min-w-48 flex-1">{p.libelle}</span>
                <span className="flex overflow-hidden rounded-full border border-bordure-forte" role="group" aria-label={p.libelle}>
                  {CHOIX.map(([v, l, ton], i) => (
                    <button key={v} disabled={!vue.modifiable} aria-pressed={p.resultat === v} onClick={() => maj(p.code, { resultat: p.resultat === v ? "" : v })}
                      className={cn("min-w-11 px-3 py-1.5 text-[12px] font-bold", i > 0 && "border-l border-bordure-forte", p.resultat === v ? ton : "bg-white text-attenue", vue.modifiable && "cursor-pointer")}>{l}</button>
                  ))}
                </span>
                <Input className="h-9 w-full text-[13px] sm:w-64" disabled={!vue.modifiable} placeholder="Observation" aria-label={`Observation, ${p.libelle}`} value={p.observation ?? ""} onChange={(e) => maj(p.code, { observation: e.target.value })} />
              </div>
            </div>
          );
        })}
      </div>
      {nc > 0 && <p className="mt-3 rounded-lg bg-orange-fond px-3 py-2 text-[12px] text-orange-texte">{nc} point(s) non conforme(s). Ils sont signalés sur la fiche et n&apos;empêchent pas de valider l&apos;étape.</p>}
      {vue.modifiable ? (
        <div className="mt-3 flex flex-wrap items-center gap-3">
          <Button size="sm" variant="outline" disabled={!modifie} onClick={enregistrer}>Enregistrer la fiche</Button>
          <span className="text-[12px] text-secondaire">{restants > 0 ? `${restants} point(s) restent à noter avant de valider l'étape.` : "Tous les points sont notés."}{modifie ? " Modifications non enregistrées." : ""}</span>
          {message && <span role={message.ok ? "status" : "alert"} className={cn("text-[12px]", message.ok ? "text-vert-texte" : "text-rouge-texte")}>{message.texte}</span>}
        </div>
      ) : <p className="mt-3 text-[12px] text-attenue">{vue.verrou ?? "Consultation seule."}{vue.saisiPar ? ` Saisie par ${vue.saisiPar}${vue.saisiLe ? ` le ${formatDateHeure(vue.saisiLe)}` : ""}.` : ""}</p>}
    </div>
  );
}

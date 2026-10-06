"use client";

import { useCallback, useEffect, useState } from "react";
import { Plus } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn, formatDate, lireNombre } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Documents } from "@/components/documents";

type Rapport = { id: string; numero: number; valeur: number | null; conforme: boolean; remarque: string | null; saisiPar: string; date: string; dernier: boolean };
type Vue = { verrou: string | null; fige: boolean; dernierSansFichier: boolean; peutCreer: boolean; rapports: Rapport[] };

/** Rapports d'écrasement d'une dalle : valeur, conformité et fichier. Un rapport non conforme reste dans l'historique ; un nouveau rapport le remplace. */
export function RapportsEcrasement({ projetId, dalleId }: { projetId: string; dalleId: string }) {
  const [vue, setVue] = useState<Vue | null>(null);
  const [valeur, setValeur] = useState("");
  const [conforme, setConforme] = useState<boolean | null>(null);
  const [remarque, setRemarque] = useState("");
  const [ouvert, setOuvert] = useState<string | null>(null);
  const [erreur, setErreur] = useState("");
  const [envoi, setEnvoi] = useState(false);
  const base = `/projets/${projetId}/dalles/${dalleId}/rapports-ecrasement`;

  const charger = useCallback(() => api<Vue>(base).then(setVue).catch(() => undefined), [base]);
  useEffect(() => { charger(); }, [charger]);

  async function creer() {
    setErreur(""); setEnvoi(true);
    try {
      const r = await api<Vue>(base, { method: "POST", body: JSON.stringify({ valeur: valeur.trim() ? lireNombre(valeur) : null, conforme, remarque }) });
      setVue(r); setValeur(""); setConforme(null); setRemarque("");
    } catch (e) {
      setErreur(e instanceof ApiError ? (e.status === 403 ? "Votre rôle ne permet pas de saisir un rapport d'écrasement." : e.message) : "Action impossible.");
    } finally { setEnvoi(false); }
  }

  if (!vue) return null;
  const dernier = vue.rapports[0];

  return (
    <div className="mt-3 rounded-lg border border-bordure bg-white p-3">
      <div className="text-[13px] font-bold">Rapports d&apos;écrasement</div>
      {erreur && <div role="alert" className="mt-2 rounded-md bg-rouge-fond px-3 py-1.5 text-[13px] text-rouge-texte">{erreur}</div>}
      {vue.rapports.length === 0 && <div className="mt-1 text-[13px] text-secondaire">Aucun rapport pour l&apos;instant : saisissez la valeur, l&apos;avis de conformité, puis déposez le fichier du laboratoire.</div>}
      {dernier && !dernier.conforme && !vue.fige && (
        <div className="mt-2 rounded-md bg-rouge-fond px-3 py-1.5 text-[13px] text-rouge-texte">
          Dernier rapport non conforme : l&apos;étape reste bloquée jusqu&apos;à un nouveau rapport conforme.
        </div>
      )}

      {vue.rapports.map((r) => {
        const visible = r.dernier || ouvert === r.id;
        return (
          <div key={r.id} className="mt-3 border-t border-bordure pt-3 first:mt-2">
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-display font-bold text-marine">Rapport n° {r.numero}</span>
              <span className="font-display font-bold">{r.valeur === null ? "Valeur non saisie" : `${r.valeur.toLocaleString("fr-FR")} MPa`}</span>
              <Badge ton={r.conforme ? "vert" : "rouge"}>{r.conforme ? "Conforme" : "Non conforme"}</Badge>
              {!r.dernier && <Badge>Remplacé</Badge>}
              <span className="text-[12px] text-secondaire">{r.saisiPar} · {formatDate(r.date)}</span>
              {!r.dernier && (
                <button className="ml-auto cursor-pointer text-[12px] font-semibold text-marine underline" onClick={() => setOuvert(visible ? null : r.id)}>
                  {visible ? "Masquer le fichier" : "Voir le fichier"}
                </button>
              )}
            </div>
            {r.remarque && <div className="mt-0.5 text-[13px]">{r.remarque}</div>}
            {visible && <Documents compact key={`${r.id}-${vue.fige}`} contexte="rapport_ecrasement" cibleId={r.id} titre="Fichier du rapport" lectureSeule={!r.dernier || vue.fige} />}
          </div>
        );
      })}

      {vue.dernierSansFichier && !vue.verrou && <div className="mt-2 text-[12px] font-semibold text-orange-texte">Déposez le fichier de ce rapport : il est obligatoire, même pour un rapport non conforme.</div>}

      {vue.peutCreer && (
        <div className="mt-3 border-t border-bordure pt-3">
          <div className="text-[12px] font-semibold text-secondaire">{dernier ? "Nouveau rapport, qui remplace le précédent" : "Nouveau rapport"}</div>
          <div className="mt-2 flex flex-wrap items-center gap-2">
            <Input className="h-9 w-36" inputMode="decimal" placeholder="Valeur, en MPa" aria-label="Valeur du rapport, en MPa" value={valeur} onChange={(e) => setValeur(e.target.value)} />
            <span className="flex overflow-hidden rounded-full border border-bordure-forte" role="group" aria-label="Avis de conformité">
              {([[true, "Conforme"], [false, "Non conforme"]] as const).map(([v, label]) => (
                <button key={label} type="button" aria-pressed={conforme === v} onClick={() => setConforme(v)}
                  className={cn("cursor-pointer px-3 py-1.5 text-[12px] font-semibold", conforme === v ? (v ? "bg-vert-fond text-vert-texte" : "bg-rouge-fond text-rouge-texte") : "bg-white text-secondaire")}>
                  {label}
                </button>
              ))}
            </span>
            <Input className="h-9 min-w-40 flex-1" placeholder="Remarque (facultative)" value={remarque} onChange={(e) => setRemarque(e.target.value)} />
            <Button size="sm" variant="outline" className="shrink-0" disabled={envoi} onClick={creer}><Plus size={14} />Enregistrer le rapport</Button>
          </div>
          <div className="mt-1 text-[12px] text-attenue">Une fois enregistré, un rapport ne se modifie plus. Déposez ensuite son fichier.</div>
        </div>
      )}
    </div>
  );
}

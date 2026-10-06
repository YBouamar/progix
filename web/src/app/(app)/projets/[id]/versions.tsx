"use client";

import { useEffect, useState } from "react";
import { Plus } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { formatDate } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Documents } from "@/components/documents";

type Version = { id: string; lettre: string; commentaire: string | null; creePar: string; date: string; courante: boolean };
type Dalle = { id: string; reference: string; niveauLabel: string; etape: string };
type Vue = { dalle: Dalle; peutCreer: boolean; fige: boolean; versions: Version[] };

const CATEGORIES: [string, string][] = [["plan", "Plan d'exécution (dwg)"], ["modele", "Modèle de calcul"], ["autre", "Autres fichiers"]];

/** Versions des plans d'exécution d'une dalle : chaque nouvelle version incrémente la lettre de la référence. */
export function VersionsPlans({ projetId, dalleId, onChange }: { projetId: string; dalleId: string; onChange: (d: Dalle) => void }) {
  const [vue, setVue] = useState<Vue | null>(null);
  const [commentaire, setCommentaire] = useState("");
  const [ouverte, setOuverte] = useState<string | null>(null);
  const [erreur, setErreur] = useState("");
  const base = `/projets/${projetId}/dalles/${dalleId}/versions`;

  useEffect(() => { api<Vue>(base).then(setVue).catch(() => undefined); }, [base]);

  async function creer() {
    setErreur("");
    try {
      const r = await api<Vue>(base, { method: "POST", body: JSON.stringify({ commentaire }) });
      setVue(r); setCommentaire(""); onChange(r.dalle);
    } catch (e) {
      setErreur(e instanceof ApiError ? (e.status === 403 ? "Les versions de plans sont créées par les dessinateurs." : e.message) : "Action impossible.");
    }
  }

  if (!vue) return null;
  const suivante = vue.versions.length === 0 ? vue.dalle.reference.slice(-1) : String.fromCharCode(vue.versions[0].lettre.charCodeAt(0) + 1);

  return (
    <div className="mt-3 rounded-lg border border-bordure bg-white p-3">
      <div className="text-[13px] font-bold">Versions des plans</div>
      {vue.fige && <div className="mt-1 text-[12px] text-secondaire">Plans figés depuis la validation du BCT : plus de nouvelle version ni de dépôt.</div>}
      {erreur && <div role="alert" className="mt-2 rounded-md bg-rouge-fond px-3 py-1.5 text-[13px] text-rouge-texte">{erreur}</div>}
      {vue.versions.length === 0 && <div className="mt-1 text-[13px] text-secondaire">Aucune version pour l&apos;instant.</div>}

      {vue.versions.map((v) => {
        const visible = v.courante || ouverte === v.id;
        return (
          <div key={v.id} className="mt-3 border-t border-bordure pt-3 first:mt-2">
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-display font-bold text-marine">Version {v.lettre}</span>
              {v.courante ? <Badge ton="vert">En vigueur</Badge> : <Badge>Ancienne</Badge>}
              <span className="text-[12px] text-secondaire">{v.creePar} · {formatDate(v.date)}</span>
              {!v.courante && (
                <button className="ml-auto cursor-pointer text-[12px] font-semibold text-marine underline" onClick={() => setOuverte(visible ? null : v.id)}>
                  {visible ? "Masquer les fichiers" : "Voir les fichiers"}
                </button>
              )}
            </div>
            {v.commentaire && <div className="mt-0.5 text-[13px]">{v.commentaire}</div>}
            {visible && CATEGORIES.map(([code, label]) => (
              <Documents key={`${v.id}-${code}`} compact contexte="plan_execution" cibleId={v.id} categorie={code} titre={label} lectureSeule={!v.courante || vue.fige} />
            ))}
          </div>
        );
      })}

      {vue.peutCreer && (
        <div className="mt-3 flex gap-2 border-t border-bordure pt-3">
          <Input className="h-9" placeholder="Objet de la version (facultatif)" value={commentaire} onChange={(e) => setCommentaire(e.target.value)} />
          <Button size="sm" variant="outline" className="shrink-0" onClick={creer}><Plus size={14} />Nouvelle version {suivante}</Button>
        </div>
      )}
    </div>
  );
}

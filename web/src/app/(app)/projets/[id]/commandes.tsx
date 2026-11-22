"use client";

import { useCallback, useEffect, useState } from "react";
import { CircleAlert, Plus } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { formatSurface } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { CommandeUsine } from "./commande";

type Resume = { id: string; numero: number; etat: string; etatLabel: string; tendons: number; surface: number | null; modifiee: boolean };
type Liste = {
  commandes: Resume[]; soumise: boolean; surfaceDalle: number; surfaceCommandes: number; ecartSurface: boolean; peutCompleter: boolean;
  typesAncrage: number[]; proposes: { libelle: string; unite: string | null }[];
};

const Alerte = ({ texte }: { texte: string }) => <span title={texte} aria-label={texte} role="img" className="ml-1 inline-flex align-middle text-orange-texte"><CircleAlert size={15} /></span>;

/**
 * Les commandes usine d'une dalle : la commande initiale, puis les compléments envoyés plus tard au chantier.
 * Avant la soumission de l'étape, seule la commande initiale se saisit. Après, chaque commande se modifie avec trace.
 */
export function CommandesDalle({ projetId, dalleId }: { projetId: string; dalleId: string }) {
  const [liste, setListe] = useState<Liste | null>(null);
  const [nouvelle, setNouvelle] = useState(false);
  const [ouverte, setOuverte] = useState<string | null>(null);
  const [erreur, setErreur] = useState("");
  const charger = useCallback(() => api<Liste>(`/projets/${projetId}/dalles/${dalleId}/commandes`).then(setListe).catch(() => undefined), [projetId, dalleId]);
  useEffect(() => { charger(); }, [charger]);

  async function recevoir(cid: string) {
    setErreur("");
    try { await api(`/projets/${projetId}/dalles/${dalleId}/commandes/${cid}/etat`, { method: "POST", body: JSON.stringify({ etat: "recue" }) }); await charger(); }
    catch (e) { setErreur(e instanceof ApiError ? (e.status === 403 ? "Votre rôle ne permet pas de déclarer la réception." : e.message) : "Action impossible."); }
  }

  if (!liste) return <CommandeUsine projetId={projetId} dalleId={dalleId} />;
  // Étape non soumise : la commande initiale n'existe pas encore sous forme tracée.
  if (!liste.soumise) return <CommandeUsine projetId={projetId} dalleId={dalleId} />;

  return (
    <div className="mt-3 space-y-3">
      <div className="rounded-lg border border-bordure bg-white p-3 text-[13px]">
        <div className="font-bold">Commandes de la dalle</div>
        <div className="mt-1 flex flex-wrap gap-x-6 gap-y-1">
          <span>Surface de la dalle : <b className="font-display">{formatSurface(liste.surfaceDalle)}</b>{liste.ecartSurface && <Alerte texte="La surface des commandes diffère de celle de la dalle. La surface de la dalle est retenue pour la facturation." />}</span>
          <span>Surface des commandes : <b className="font-display">{formatSurface(liste.surfaceCommandes)}</b>{liste.ecartSurface && <Alerte texte="La surface des commandes diffère de celle de la dalle. La surface de la dalle est retenue pour la facturation." />}</span>
        </div>
        <ul className="mt-2 divide-y divide-bordure">
          {liste.commandes.map((c) => (
            <li key={c.id} className="flex flex-wrap items-center gap-2 py-1.5">
              <button className="font-semibold text-marine underline" onClick={() => setOuverte(ouverte === c.id ? null : c.id)} aria-expanded={ouverte === c.id}>
                Commande n° {c.numero}{c.numero === 1 ? " (initiale)" : ""}
              </button>
              <span className="rounded-full bg-fond px-2 py-0.5 text-[12px]">{c.etatLabel}</span>
              <span className="text-secondaire">{c.tendons} tendon{c.tendons > 1 ? "s" : ""}{c.surface ? ` · ${formatSurface(c.surface)}` : ""}</span>
              {c.etat === "envoyee" && <Button size="sm" variant="outline" onClick={() => recevoir(c.id)}>Déclarer reçue au chantier</Button>}
              {c.modifiee && <span className="rounded-full bg-orange-fond px-2 py-0.5 text-[12px] text-orange-texte">Modifiée</span>}
            </li>
          ))}
        </ul>
        {erreur && <div role="alert" className="mt-2 text-rouge-texte">{erreur}</div>}
        {liste.peutCompleter && !nouvelle && (
          <Button size="sm" variant="outline" className="mt-2" onClick={() => setNouvelle(true)}><Plus size={14} />Ajouter une commande complémentaire</Button>
        )}
      </div>

      {liste.commandes.filter((c) => ouverte === c.id).map((c) => (
        <CommandeUsine key={c.id} projetId={projetId} dalleId={dalleId} commandeId={c.id} onChange={charger} />
      ))}
      {nouvelle && (
        <CommandeUsine projetId={projetId} dalleId={dalleId} modele={{ typesAncrage: liste.typesAncrage, proposes: liste.proposes, surfaceDalle: liste.surfaceDalle }}
          onChange={() => { setNouvelle(false); charger(); }} />
      )}
    </div>
  );
}

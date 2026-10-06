"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/modal";
import { PartenaireCombo } from "@/components/partenaire-combo";
import { PartenaireSheet } from "../../clients/partenaire-sheet";
import type { Compteur } from "../../clients/types";

type Ligne = { role: string; label: string; partenaireId: string | null; nom: string | null; contact: string | null };

export function PartenairesProjet({ projetId, ecriture }: { projetId: string; ecriture: boolean }) {
  const [lignes, setLignes] = useState<Ligne[]>([]);
  const [choix, setChoix] = useState<Ligne | null>(null);
  const [sel, setSel] = useState<{ id: string | null; texte: string }>({ id: null, texte: "" });
  const [erreur, setErreur] = useState("");
  const [creation, setCreation] = useState<Compteur[] | null>(null);

  useEffect(() => { api<Ligne[]>(`/projets/${projetId}/partenaires`).then(setLignes).catch(() => undefined); }, [projetId]);

  async function affecter(role: string, partenaireId: string | null) {
    setErreur("");
    try {
      setLignes(await api<Ligne[]>(`/projets/${projetId}/partenaires/${role}`, { method: "PUT", body: JSON.stringify({ partenaireId }) }));
      setChoix(null);
    } catch (e) {
      setErreur(e instanceof ApiError ? e.message : "Action impossible.");
    }
  }

  const ouvrir = (l: Ligne) => { setChoix(l); setSel({ id: null, texte: "" }); setErreur(""); };

  return (
    <div className="mt-5 rounded-xl border border-bordure bg-white p-6">
      <h3 className="mb-4 text-[15px] font-bold">Partenaires</h3>
      <div className="grid gap-3 sm:grid-cols-2">
        {lignes.map((l) => (
          <div key={l.role} className="flex items-center justify-between gap-3 rounded-lg border border-bordure p-4">
            <div className="min-w-0">
              <div className="text-[12px] font-semibold uppercase tracking-wide text-attenue">{l.label}</div>
              <div className={l.nom ? "truncate font-semibold" : "text-secondaire"}>{l.nom ?? "Non renseigné"}</div>
              <div className="truncate text-[13px] text-secondaire">
                {l.nom ? l.contact : l.role === "laboratoire" ? "Requis avant le rapport d'écrasement" : ""}
              </div>
            </div>
            {ecriture && (
              <div className="flex shrink-0 gap-1">
                <Button variant="outline" size="sm" onClick={() => ouvrir(l)}>{l.nom ? "Changer" : "Ajouter"}</Button>
                {l.nom && <Button variant="ghost" size="sm" onClick={() => affecter(l.role, null)}>Retirer</Button>}
              </div>
            )}
          </div>
        ))}
      </div>

      <Modal open={!!choix && !creation} onOpenChange={(o) => !o && setChoix(null)}
        title={choix?.label ?? ""} description="Choisissez un partenaire de ce type dans la liste, ou créez-le sans quitter le projet."
        footer={
          <>
            <Button variant="ghost" className="mr-auto"
              onClick={async () => setCreation(await api<Compteur[]>("/partenaires/compteurs").catch(() => []))}>Créer un partenaire</Button>
            <Button variant="outline" onClick={() => setChoix(null)}>Annuler</Button>
            <Button disabled={!sel.id} onClick={() => choix && affecter(choix.role, sel.id)}>Valider</Button>
          </>
        }>
        {choix && (
          <PartenaireCombo autoFocus type={choix.role} id={sel.id} texte={sel.texte} placeholder="Rechercher"
            onChange={(id, texte) => setSel({ id, texte })} />
        )}
        {erreur && <div role="alert" className="mt-2 text-[13px] text-rouge-texte">{erreur}</div>}
      </Modal>

      {creation && choix && (
        <PartenaireSheet id={null} ecriture types={creation} typeInitial={choix.role} onClose={() => setCreation(null)}
          onSaved={(c) => { setCreation(null); if (c) setSel({ id: c.id, texte: c.nom }); }} />
      )}
    </div>
  );
}

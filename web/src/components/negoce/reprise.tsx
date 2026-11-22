"use client";

import { useState } from "react";
import { api } from "@/lib/api";
import { filtrerDecimal, formatQuantite, lireNombre } from "@/lib/negoce";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";
import { Alerte, messageErreur } from "./commun";

export type LigneReprise = { id: string; code: string; designation: string; unite: string; reste: number };

/**
 * Crée un document qui reprend une partie d'un autre (bon de livraison ou facture d'une commande, avoir d'une facture).
 * Chaque ligne se reprend en tout ou en partie, dans la limite de ce qui reste.
 */
export function Reprise({ titre, description, chemin, corpsDe, avecEcheance, libelleReste, lignes, motif, onFermer, onFait }: {
  titre: string; description: string; chemin: string; corpsDe: Record<string, unknown>; avecEcheance?: boolean; libelleReste: string;
  lignes: LigneReprise[]; motif?: boolean; onFermer: () => void; onFait: () => void;
}) {
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10));
  const [echeance, setEcheance] = useState("");
  const [texte, setTexte] = useState("");
  const [quantites, setQuantites] = useState<Record<string, string>>({});
  const [erreur, setErreur] = useState("");
  const [enCours, setEnCours] = useState(false);
  const reprises = lignes.filter((l) => lireNombre(quantites[l.id] ?? "") > 0);

  const valider = async () => {
    setErreur(""); setEnCours(true);
    try {
      const corps: Record<string, unknown> = { ...corpsDe, dateDocument: date, lignes: reprises.map((l) => ({ ligneId: l.id, quantite: lireNombre(quantites[l.id]) })) };
      if (avecEcheance) corps.echeance = echeance || null;
      if (motif) corps.motif = texte; else corps.notes = texte;
      await api(chemin, { method: "POST", body: JSON.stringify(corps) });
      onFait();
    } catch (e) { setErreur(messageErreur(e)); } finally { setEnCours(false); }
  };

  return (
    <Modal open onOpenChange={(o) => !o && onFermer()} large title={titre} description={description}
      footer={<><Button variant="outline" onClick={onFermer}>Annuler</Button><Button onClick={valider} disabled={enCours || reprises.length === 0 || (!!motif && !texte.trim())}>Créer</Button></>}>
      <Alerte message={erreur} />
      <div className="mb-4 grid gap-4 sm:grid-cols-3">
        <Field label="Date"><Input type="date" value={date} onChange={(e) => setDate(e.target.value)} /></Field>
        {avecEcheance && <Field label="Échéance"><Input type="date" value={echeance} onChange={(e) => setEcheance(e.target.value)} /></Field>}
        <Field label={motif ? "Motif" : "Notes"} required={!!motif} className={avecEcheance ? "" : "sm:col-span-2"}><Input value={texte} onChange={(e) => setTexte(e.target.value)} /></Field>
      </div>
      {lignes.length === 0 ? <p className="text-secondaire">Rien à reprendre.</p> : (
        <ul className="space-y-2">
          {lignes.map((l) => (
            <li key={l.id} className="grid items-center gap-2 sm:grid-cols-[1fr_150px]">
              <div><div className="font-semibold">{l.designation}</div><div className="text-[13px] text-secondaire">{libelleReste} : {formatQuantite(l.reste)} {l.unite}</div></div>
              <div className="flex gap-1.5">
                <Input aria-label={`Quantité de ${l.designation}`} inputMode="decimal" value={quantites[l.id] ?? ""} onChange={(e) => setQuantites({ ...quantites, [l.id]: filtrerDecimal(e.target.value) })} />
                <Button type="button" variant="outline" size="sm" onClick={() => setQuantites({ ...quantites, [l.id]: String(l.reste).replace(".", ",") })}>Tout</Button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </Modal>
  );
}

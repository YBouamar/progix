"use client";

import { useState } from "react";
import { api } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";
import { Alerte, messageErreur } from "./commun";

/** Fenêtre de confirmation avec motif obligatoire : une annulation se trace, elle ne s'efface pas. */
export function Annulation({ chemin, titre, onFermer, onFait }: { chemin: string; titre: string; onFermer: () => void; onFait: () => void }) {
  const [motif, setMotif] = useState("");
  const [erreur, setErreur] = useState("");
  const valider = async () => {
    try { await api(chemin, { method: "POST", body: JSON.stringify({ motif }) }); onFait(); } catch (e) { setErreur(messageErreur(e)); }
  };
  return (
    <Modal open onOpenChange={(o) => !o && onFermer()} title={titre} description="Le motif est conservé avec le document."
      footer={<><Button variant="outline" onClick={onFermer}>Retour</Button><Button variant="danger" disabled={!motif.trim()} onClick={valider}>Confirmer l&apos;annulation</Button></>}>
      <Alerte message={erreur} />
      <Field label="Motif" required><Input value={motif} onChange={(e) => setMotif(e.target.value)} autoFocus /></Field>
    </Modal>
  );
}


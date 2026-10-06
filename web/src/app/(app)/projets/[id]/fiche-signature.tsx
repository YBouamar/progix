"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { SignatureDoc } from "@/components/signature-doc";

type Etat = { figee: boolean; signePar: string | null; signeLe: string | null; peutSigner: boolean };

/** Signature et PDF d'une fiche de chantier, proposés une fois l'étape validée. */
export function FicheSignature({ dalleId, type, quoi }: { dalleId: string; type: "auto-controle" | "tension" | "injection"; quoi: string }) {
  const url = `/dalles/${dalleId}/fiches/${type}`;
  const [etat, setEtat] = useState<Etat | null>(null);
  useEffect(() => { api<Etat>(url).then(setEtat).catch(() => setEtat(null)); }, [url]);
  if (!etat || !etat.figee) return null;
  return (
    <SignatureDoc<Etat> signePar={etat.signePar} signeLe={etat.signeLe} quoi={quoi} url={url} peutSigner={etat.peutSigner}
      raison="Fiche non signée : elle ne peut pas encore être imprimée. La signature revient à ceux qui réalisent cette étape, au chef de projet ou au directeur." onSigne={setEtat} />
  );
}

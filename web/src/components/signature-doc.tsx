"use client";

import { useState } from "react";
import { FileDown } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/input";

/** Signature par mot de passe : tant qu'un document n'est pas signé, il ne peut être ni imprimé ni envoyé. */
export function SignatureDoc<T>({ signePar, signeLe, quoi, url, peutSigner = true, raison, onSigne }: {
  signePar: string | null; signeLe: string | null; quoi: string; url: string; peutSigner?: boolean; raison?: string; onSigne: (r: T) => void;
}) {
  const [motDePasse, setMotDePasse] = useState("");
  const [erreur, setErreur] = useState("");
  const [envoi, setEnvoi] = useState(false);
  if (signePar) return (
    <div className="mt-3 flex flex-wrap items-center justify-between gap-3">
      <span className="text-[12px] text-secondaire">Signé électroniquement par {signePar}{signeLe ? ` le ${new Date(signeLe).toLocaleString("fr-FR", { dateStyle: "short", timeStyle: "short" })}` : ""}</span>
      <a href={`/api${url}/pdf`} target="_blank" rel="noreferrer" className="inline-flex items-center gap-2 rounded-lg border border-bordure-forte bg-white px-3.5 py-2 text-[13px] font-semibold hover:bg-fond"><FileDown size={15} />Télécharger en PDF</a>
    </div>
  );
  if (!peutSigner) return <p className="mt-3 text-[12px] text-secondaire">{raison ?? `Document non signé : il ne peut pas encore être imprimé ni envoyé.`}</p>;
  const signer = async () => {
    setEnvoi(true); setErreur("");
    try { onSigne(await api<T>(`${url}/signer`, { method: "POST", body: JSON.stringify({ motDePasse }) })); setMotDePasse(""); }
    catch (e) { setErreur(e instanceof ApiError ? e.message : "Signature impossible."); }
    finally { setEnvoi(false); }
  };
  return (
    <div className="mt-3 rounded-lg border border-bordure bg-fond p-4">
      <div className="text-[13px] font-semibold">Signer {quoi}</div>
      <p className="text-[12px] text-secondaire">Un document se signe avant de pouvoir être imprimé ou envoyé. Votre nom, la date et un QR code de vérification y figureront.</p>
      <div className="mt-2 flex items-end gap-2">
        <Field label="Votre mot de passe" required className="flex-1"><Input type="password" autoComplete="current-password" value={motDePasse} onChange={(e) => setMotDePasse(e.target.value)} onKeyDown={(e) => { if (e.key === "Enter" && motDePasse) signer(); }} /></Field>
        <Button disabled={envoi || !motDePasse} onClick={signer}>Signer</Button>
      </div>
      {erreur && <div role="alert" className="mt-2 rounded-lg bg-rouge-fond px-3 py-2 text-[12px] text-rouge-texte">{erreur}</div>}
    </div>
  );
}

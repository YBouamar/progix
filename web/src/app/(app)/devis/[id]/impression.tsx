"use client";

import { FileDown, Printer } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { SignatureDoc } from "@/components/signature-doc";
import { Documents } from "@/components/documents";
import type { DevisDetail } from "../types";

/** Contrôle avant envoi : « bloquant » empêche de marquer le devis envoyé, « a_voir » se signale sans bloquer. */
export type Controle = { niveau: "bon" | "a_voir" | "bloquant"; texte: string };

const LIEN = "inline-flex h-10 items-center justify-center gap-2 rounded-lg border border-bordure-forte bg-white px-4 text-[14px] font-semibold hover:bg-fond";

/**
 * Étape 3 de la fiche du devis : aperçu du PDF tel qu'il sera imprimé, contrôles avant envoi, puis les actions dans l'ordre
 * (relire le brouillon, marquer envoyé, signer, imprimer le devis signé).
 */
export function ImpressionDevis({ d, controles, modifie, occupe, onEnvoyer, onSigne }: {
  d: DevisDetail; controles: Controle[]; modifie: boolean; occupe: boolean; onEnvoyer: () => void; onSigne: (r: DevisDetail) => void;
}) {
  const brouillon = d.etat === "brouillon";
  const pdf = `/api/devis/${d.id}/pdf`;
  // Le PDF existe pour un brouillon (document de travail) et pour un devis signé ; un devis envoyé non signé ne s'imprime pas.
  const apercu = brouillon ? !modifie : !!d.signePar;
  const bloquants = controles.filter((c) => c.niveau === "bloquant");

  return (
    <div className="mt-5 flex flex-wrap items-start gap-5">
      <section aria-label="Aperçu du devis" className="min-w-0 flex-[999_1_520px] rounded-xl bg-neutre-fond p-4">
        {apercu ? (
          <>
            <iframe key={`${d.etat}-${d.signeLe ?? ""}-${d.totalTTC}`} src={`${pdf}?apercu=true#toolbar=0&view=FitH`} title={`Aperçu du devis ${d.numero}`} className="h-[78vh] min-h-[520px] w-full rounded-lg border border-bordure bg-white" />
            <p className="mt-2 text-center text-[12px] text-secondaire">
              {brouillon ? "Aperçu du brouillon tel qu'enregistré : marqué « BROUILLON », sans signature ni QR code." : "Aperçu du devis signé, tel qu'il est remis au client."} Les ratios n&apos;y figurent pas.{" "}
              <a href={`${pdf}?apercu=true`} target="_blank" rel="noreferrer" className="font-semibold text-marine hover:underline">Ouvrir le PDF dans un nouvel onglet pour l&apos;imprimer</a>
            </p>
          </>
        ) : (
          <div className="flex min-h-[320px] flex-col items-center justify-center gap-2 rounded-lg border border-dashed border-bordure-forte bg-white px-6 py-12 text-center">
            <div className="font-display font-bold">{brouillon ? "Enregistrez le brouillon pour voir l'aperçu" : "Le devis n'est pas encore signé"}</div>
            <p className="max-w-[48ch] text-[13px] text-secondaire">
              {brouillon ? "Le PDF reprend la version enregistrée : vos modifications en cours n'y sont pas encore." : "Un devis envoyé se signe avant de pouvoir être imprimé. L'aperçu apparaît dès la signature."}
            </p>
          </div>
        )}
      </section>

      <aside className="flex min-w-[280px] flex-[1_1_320px] flex-col gap-4">
        {brouillon && (
          <section className="rounded-xl border border-bordure bg-white p-5">
            <h3 className="text-[15px] font-bold">Contrôles avant envoi</h3>
            <ul className="mt-3 space-y-2.5 text-[13px]">
              {controles.map((c, i) => (
                <li key={i} className="flex items-start gap-2.5">
                  <Badge className="shrink-0" ton={c.niveau === "bon" ? "vert" : c.niveau === "a_voir" ? "orange" : "rouge"}>{c.niveau === "bon" ? "Bon" : c.niveau === "a_voir" ? "À voir" : "Bloquant"}</Badge>
                  <span>{c.texte}</span>
                </li>
              ))}
            </ul>
          </section>
        )}

        {brouillon && (
          <section className="flex flex-col gap-3 rounded-xl border border-bordure bg-white p-5">
            <h3 className="text-[15px] font-bold">Relecture</h3>
            {modifie ? <Button variant="outline" disabled title="Enregistrez d'abord le brouillon : le PDF reprend la version enregistrée"><Printer size={16} />Imprimer le brouillon</Button> : (
              <>
                <a href={`${pdf}?apercu=true`} target="_blank" rel="noreferrer" className={LIEN}><Printer size={16} />Imprimer le brouillon</a>
                <a href={pdf} download={`${d.numero}-brouillon.pdf`} className={LIEN}><FileDown size={16} />Télécharger le PDF du brouillon</a>
              </>
            )}
          </section>
        )}

        <section className="flex flex-col gap-3 rounded-xl border border-bordure bg-white p-5">
          <h3 className="text-[15px] font-bold">Envoi au client</h3>
          {brouillon ? (
            <>
              <p className="text-[13px] text-secondaire">Marquer le devis envoyé le fige. Il se signe ensuite d&apos;un clic, puis s&apos;imprime avec le bloc de signature et le QR code.</p>
              {d.actions.envoyer
                ? <Button disabled={occupe || modifie || bloquants.length > 0} onClick={onEnvoyer}
                    title={modifie ? "Enregistrez d'abord le brouillon" : bloquants.length > 0 ? bloquants[0].texte : undefined}>Marquer envoyé</Button>
                : <p className="text-[13px] text-secondaire">Votre fonction consulte les devis sans les envoyer.</p>}
              <Button variant="outline" disabled>Signer le devis</Button>
              <Button variant="outline" disabled>Imprimer le devis signé</Button>
            </>
          ) : (
            <>
              <p className="text-[13px] text-secondaire">{d.info}{d.alerte ? ` · ${d.alerte}` : ""}</p>
              <SignatureDoc<DevisDetail> signePar={d.signePar} signeLe={d.signeLe} quoi="le devis" url={`/devis/${d.id}`} peutSigner={d.signable}
                raison="Devis non signé : il ne peut pas encore être imprimé ni envoyé. La signature revient au responsable administratif, au chef de projet ou au directeur."
                onSigne={onSigne} />
            </>
          )}
        </section>

        <section className="rounded-xl border border-bordure bg-white px-5 pb-5 pt-2">
          <Documents compact key={`plans-${d.etat}`} contexte="plan_principe" cibleId={d.id} lectureSeule={!brouillon} titre="Plans de principe joints au devis"
            aide="Ils accompagnent le devis remis au client. Dépôt possible tant que le devis est en brouillon." />
        </section>
      </aside>
    </div>
  );
}

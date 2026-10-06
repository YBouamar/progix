"use client";

import { use, useEffect, useState } from "react";

type Doc = { type: string; numero: string; entite: string; lignes: { label: string; valeur: string }[]; signePar: string; signeLe: string | null; annule: string | null };

/** Page publique atteinte par le QR code d'un document signé : elle confirme que le document vient bien de PROGIX. */
export default function VerificationPage({ params }: { params: Promise<{ jeton: string }> }) {
  const { jeton } = use(params);
  const [doc, setDoc] = useState<Doc | null>(null);
  const [etat, setEtat] = useState<"chargement" | "ok" | "inconnu" | "erreur">("chargement");

  useEffect(() => {
    fetch(`/api/verification/${encodeURIComponent(jeton)}`)
      .then(async (r) => { if (r.status === 404) { setEtat("inconnu"); return; } if (!r.ok) throw new Error(); setDoc(await r.json()); setEtat("ok"); })
      .catch(() => setEtat("erreur"));
  }, [jeton]);

  const lignes: [string, string][] = doc ? [
    ["Document", `${doc.type} ${doc.numero}`], ["Émis par", doc.entite], ...doc.lignes.map((l): [string, string] => [l.label, l.valeur]),
    ["Signé électroniquement par", doc.signePar],
    ["Signé le", doc.signeLe ? new Date(doc.signeLe).toLocaleString("fr-FR", { dateStyle: "short", timeStyle: "short", timeZone: "Africa/Casablanca" }) : ""],
  ] : [];

  return (
    <main className="flex min-h-screen items-start justify-center bg-fond px-4 py-10">
      <div className="w-full max-w-md rounded-xl border border-bordure bg-white p-6">
        <div className="font-display text-lg font-bold text-marine">PROGIX</div>
        <h1 className="mt-1 font-display text-xl font-bold">Vérification d&apos;un document</h1>
        {etat === "chargement" && <p className="mt-4 text-secondaire">Vérification en cours...</p>}
        {etat === "erreur" && <p role="alert" className="mt-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">Vérification impossible pour le moment. Réessayez plus tard.</p>}
        {etat === "inconnu" && (
          <div role="alert" className="mt-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">
            <div className="font-semibold">Document inconnu.</div>Ce code ne correspond à aucun document émis par PROGIX. Ne vous fiez pas au document qui le porte.
          </div>
        )}
        {etat === "ok" && doc && (
          <>
            <div role="status" className={`mt-4 rounded-lg px-4 py-3 ${doc.annule ? "bg-rouge-fond text-rouge-texte" : "bg-vert-fond text-vert-texte"}`}>
              <div className="font-semibold">{doc.annule ? "Document authentique, mais annulé." : "Document authentique."}</div>
              {doc.annule ?? "Comparez les informations ci-dessous avec celles du document en votre possession."}
            </div>
            <dl className="mt-4 divide-y divide-bordure text-[14px]">
              {lignes.map(([l, v]) => (
                <div key={l} className="flex justify-between gap-4 py-2"><dt className="text-secondaire">{l}</dt><dd className="text-right font-semibold">{v}</dd></div>
              ))}
            </dl>
          </>
        )}
      </div>
    </main>
  );
}

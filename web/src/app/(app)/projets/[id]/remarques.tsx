"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { formatDateHeure, initiales } from "@/lib/utils";
import { Button } from "@/components/ui/button";

type Trace = { action: string; ancienTexte: string; par: string; date: string };
type Remarque = {
  id: string; auteur: string; role: string; date: string; texte: string | null;
  supprime: boolean; supprimePar: string | null; supprimeLe: string | null;
  modifiePar: string | null; modifieLe: string | null; mienne: boolean; historique: Trace[];
};

const ZONE = "w-full rounded-lg border border-bordure-forte px-3 py-2 text-sm focus:border-marine focus:outline-none focus:ring-2 focus:ring-marine/15";

export function RemarquesProjet({ projetId, lectureSeule }: { projetId: string; lectureSeule: boolean }) {
  const [liste, setListe] = useState<Remarque[]>([]);
  const [texte, setTexte] = useState("");
  const [edition, setEdition] = useState<{ id: string; texte: string } | null>(null);
  const [historique, setHistorique] = useState<string | null>(null);
  const [erreur, setErreur] = useState("");
  const base = `/projets/${projetId}/remarques`;

  useEffect(() => { api<Remarque[]>(base).then(setListe).catch(() => undefined); }, [base]);

  async function agir(f: () => Promise<Remarque>, nouveau = false) {
    setErreur("");
    try {
      const r = await f();
      setListe((l) => (nouveau ? [r, ...l] : l.map((x) => (x.id === r.id ? r : x))));
      setTexte(nouveau ? "" : texte);
      setEdition(null);
    } catch (e) {
      setErreur(e instanceof ApiError ? e.message : "Action impossible.");
    }
  }

  return (
    <div className="mt-5 rounded-xl border border-bordure bg-white p-6">
      <h3 className="mb-4 text-[15px] font-bold">Remarques</h3>
      {erreur && <div role="alert" className="mb-3 rounded-lg bg-rouge-fond px-4 py-2 text-rouge-texte">{erreur}</div>}

      {!lectureSeule && (
        <div className="mb-5">
          <textarea rows={3} className={ZONE} placeholder="Ajouter une remarque" value={texte} onChange={(e) => setTexte(e.target.value)} />
          <div className="mt-2 flex items-center justify-between gap-3">
            <span className="text-[12px] text-attenue">
              Signée de votre nom et datée. Toute modification ou suppression reste tracée : ancien texte, auteur et date.
            </span>
            <Button size="sm" disabled={!texte.trim()}
              onClick={() => agir(() => api<Remarque>(base, { method: "POST", body: JSON.stringify({ texte }) }), true)}>Publier</Button>
          </div>
        </div>
      )}

      {liste.length === 0 && <div className="text-[13px] text-attenue">Aucune remarque.</div>}
      <ul className="space-y-4">
        {liste.map((r) => (
          <li key={r.id} className="flex gap-3">
            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-neutre-fond font-display text-[12px] font-bold text-neutre-texte">
              {initiales(r.auteur)}
            </div>
            <div className="min-w-0 flex-1">
              <div className="text-[13px]">
                <span className="font-semibold">{r.auteur}</span>
                <span className="text-secondaire"> · {r.role} · {formatDateHeure(r.date)}</span>
              </div>
              {edition?.id === r.id ? (
                <div className="mt-1">
                  <textarea rows={3} autoFocus className={ZONE} value={edition.texte} onChange={(e) => setEdition({ id: r.id, texte: e.target.value })} />
                  <div className="mt-2 flex gap-2">
                    <Button size="sm" disabled={!edition.texte.trim()}
                      onClick={() => agir(() => api<Remarque>(`${base}/${r.id}`, { method: "PUT", body: JSON.stringify({ texte: edition.texte }) }))}>Enregistrer</Button>
                    <Button size="sm" variant="outline" onClick={() => setEdition(null)}>Annuler</Button>
                  </div>
                </div>
              ) : (
                <p className={r.supprime ? "italic text-secondaire" : "whitespace-pre-line"}>{r.supprime ? "Remarque supprimée." : r.texte}</p>
              )}
              {(r.supprime || r.modifieLe) && (
                <div className="mt-0.5 text-[12px] text-attenue">
                  {r.supprime ? `Supprimée par ${r.supprimePar} le ${formatDateHeure(r.supprimeLe)}` : `Modifiée par ${r.modifiePar} le ${formatDateHeure(r.modifieLe)}`}
                  {" · "}
                  <button className="cursor-pointer font-semibold underline" onClick={() => setHistorique(historique === r.id ? null : r.id)}>
                    {historique === r.id ? "Masquer l'historique" : "Voir l'historique"}
                  </button>
                </div>
              )}
              {historique === r.id && (
                <ul className="mt-2 space-y-2 rounded-lg bg-fond p-3 text-[13px]">
                  {r.historique.map((t, i) => (
                    <li key={i}>
                      <div className="text-[12px] text-secondaire">
                        {t.action === "suppression" ? "Texte avant suppression" : "Texte avant modification"}, par {t.par} le {formatDateHeure(t.date)}
                      </div>
                      <div className="whitespace-pre-line">{t.ancienTexte}</div>
                    </li>
                  ))}
                </ul>
              )}
              {r.mienne && !r.supprime && !lectureSeule && edition?.id !== r.id && (
                <div className="mt-1 flex gap-3 text-[13px] font-semibold text-secondaire">
                  <button className="cursor-pointer hover:text-marine" onClick={() => setEdition({ id: r.id, texte: r.texte ?? "" })}>Modifier</button>
                  <button className="cursor-pointer hover:text-rouge-texte"
                    onClick={() => agir(() => api<Remarque>(`${base}/${r.id}`, { method: "DELETE" }))}>Supprimer</button>
                </div>
              )}
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}

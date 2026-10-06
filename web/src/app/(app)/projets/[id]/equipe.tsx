"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { formatDate, initiales } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Select } from "@/components/ui/input";

type Membre = { id: string; userId: string; nom: string; debut: string; fin: string | null };
type Personne = { id: string; nom: string };
type Equipe = {
  ingenieurs: Membre[]; chefsChantier: Membre[];
  candidats: { ingenieurs: Personne[]; chefsChantier: Personne[] };
  droits: { ingenieurs: boolean; chefsChantier: boolean };
};

export function EquipeProjet({ projetId, lectureSeule }: { projetId: string; lectureSeule: boolean }) {
  const [equipe, setEquipe] = useState<Equipe | null>(null);
  const [erreur, setErreur] = useState("");
  const base = `/projets/${projetId}/equipe`;

  useEffect(() => { api<Equipe>(base).then(setEquipe).catch(() => undefined); }, [base]);

  async function agir(chemin: string, corps?: object) {
    setErreur("");
    try {
      setEquipe(await api<Equipe>(chemin, { method: "POST", body: JSON.stringify(corps ?? {}) }));
    } catch (e) {
      setErreur(e instanceof ApiError ? e.message : "Action impossible.");
    }
  }

  if (!equipe) return null;

  const Bloc = ({ titre, fonction, membres, candidats, droit }: {
    titre: string; fonction: string; membres: Membre[]; candidats: Personne[]; droit: boolean;
  }) => {
    const actifs = new Set(membres.filter((m) => !m.fin).map((m) => m.userId));
    const libres = candidats.filter((c) => !actifs.has(c.id));
    const peut = droit && !lectureSeule;
    return (
      <div>
        <div className="mb-2 text-[12px] font-semibold uppercase tracking-wide text-attenue">{titre}</div>
        {membres.length === 0 && <div className="text-secondaire">Non assigné</div>}
        <ul className="space-y-2">
          {membres.map((m) => (
            <li key={m.id} className="flex items-center gap-3">
              <div className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-full font-display text-[11px] font-bold ${m.fin ? "bg-neutre-fond text-attenue" : "bg-marine/10 text-marine"}`}>
                {initiales(m.nom)}
              </div>
              <div className="min-w-0 flex-1">
                <div className={m.fin ? "text-secondaire" : "font-semibold"}>{m.nom}</div>
                <div className="text-[12px] text-secondaire">
                  {m.fin ? `Du ${formatDate(m.debut)} au ${formatDate(m.fin)} · historique` : `Depuis le ${formatDate(m.debut)}`}
                </div>
              </div>
              {peut && !m.fin && <Button variant="ghost" size="sm" onClick={() => agir(`${base}/${m.id}/retirer`)}>Retirer</Button>}
            </li>
          ))}
        </ul>
        {peut && libres.length > 0 && (
          <Select className="mt-3 h-9 w-56 text-[13px]" value="" aria-label={`Affecter : ${titre}`}
            onChange={(e) => e.target.value && agir(base, { fonction, userId: e.target.value })}>
            <option value="">Affecter...</option>
            {libres.map((c) => <option key={c.id} value={c.id}>{c.nom}</option>)}
          </Select>
        )}
      </div>
    );
  };

  return (
    <div className="mt-5 rounded-xl border border-bordure bg-white p-6">
      <h3 className="mb-4 text-[15px] font-bold">Équipe</h3>
      {erreur && <div role="alert" className="mb-3 rounded-lg bg-rouge-fond px-4 py-2 text-rouge-texte">{erreur}</div>}
      <div className="grid gap-6 sm:grid-cols-2">
        <Bloc titre="Ingénieurs" fonction="ingenieur" membres={equipe.ingenieurs} candidats={equipe.candidats.ingenieurs} droit={equipe.droits.ingenieurs} />
        <Bloc titre="Chefs de chantier" fonction="chef_chantier" membres={equipe.chefsChantier} candidats={equipe.candidats.chefsChantier} droit={equipe.droits.chefsChantier} />
      </div>
    </div>
  );
}

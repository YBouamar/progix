"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { Check, Search, Wand2, X } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { cn, formatDate, formatDateHeure, formatSurface } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input, Select } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";
import { Sheet } from "@/components/ui/sheet";
import { AttenteFiche, AttenteLignes } from "@/components/ui/attente";
import { Documents } from "@/components/documents";
import { GenerationFondPlan } from "./fond-plan";

type Etude = {
  dalleId: string; projetId: string; projetCode: string; projetLibelle: string; ville: string | null;
  reference: string; niveauLabel: string; surface: number; etat: "a_affecter" | "en_cours" | "terminee"; etatLabel: string;
  ingenieurs: { userId: string; nom: string; depuis: string }[]; pieces: string[];
  termineeLe: string | null; termineePar: string | null; peutTerminer: boolean; peutRouvrir: boolean; peutModifierIngenieurs: boolean; peutDeposer: boolean;
};
type Piece = { code: string; label: string };
type Personne = { id: string; nom: string };
type Liste = {
  items: Etude[]; total: number; page: number; pageSize: number;
  compteurs: { etat: string; label: string; nb: number }[]; pieces: Piece[]; ingenieurs: Personne[]; peutAffecter: boolean;
};
type Detail = { etude: Etude; historique: { nom: string; par: string; debut: string; fin: string | null; retirePar: string | null }[] };

const TON = { a_affecter: "orange", en_cours: "violet", terminee: "vert" } as const;

/** Menu Étude : une étude par dalle de projet, ses ingénieurs, ses pièces et son état. */
export default function EtudesPage() {
  const { user } = useAuth();
  const [etat, setEtat] = useState("");
  const [ingenieur, setIngenieur] = useState("");
  const [saisie, setSaisie] = useState("");
  const [q, setQ] = useState("");
  const [page, setPage] = useState(1);
  const [data, setData] = useState<Liste | null>(null);
  const [erreur, setErreur] = useState("");
  const [ouverte, setOuverte] = useState<string | null>(null);

  useEffect(() => {
    const t = setTimeout(() => { setQ(saisie.trim()); setPage(1); }, 300);
    return () => clearTimeout(t);
  }, [saisie]);

  const charger = useCallback(async () => {
    try {
      const p = new URLSearchParams({ page: String(page) });
      if (etat) p.set("etat", etat);
      if (q) p.set("q", q);
      if (ingenieur) p.set("ingenieur", ingenieur);
      setData(await api<Liste>(`/etudes?${p}`));
      setErreur("");
    } catch (e) {
      setErreur(e instanceof ApiError ? e.message : "Chargement impossible.");
    }
  }, [page, etat, q, ingenieur]);

  useEffect(() => { charger(); }, [charger]);

  const compteurs = data?.compteurs ?? [];
  const onglets = [{ etat: "", label: "Toutes", nb: compteurs.reduce((s, c) => s + c.nb, 0) }, ...compteurs];
  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;
  const filtre = (v: string, set: (v: string) => void) => { set(v); setPage(1); };
  const estIngenieur = !!user && data?.ingenieurs.some((i) => i.id === user.id);

  return (
    <div>
      <div className="mb-4 flex flex-wrap gap-2">
        {onglets.map((o) => (
          <button key={o.etat} aria-pressed={etat === o.etat} onClick={() => filtre(o.etat, setEtat)}
            className={cn("flex cursor-pointer items-center gap-2 rounded-full border px-3.5 py-1.5 text-[13px] font-semibold",
              etat === o.etat ? "border-marine bg-marine text-white" : "border-bordure-forte bg-white hover:bg-fond")}>
            {o.label}
            <span className={cn("font-display text-[12px]", etat === o.etat ? "text-white/80" : "text-attenue")}>{o.nb}</span>
          </button>
        ))}
      </div>

      <div className="mb-5 flex flex-wrap items-center gap-3">
        <div className="relative w-full max-w-xs">
          <Search size={16} className="pointer-events-none absolute left-3 top-3 text-attenue" />
          <Input className="pl-9" placeholder="Code, libellé ou ville du projet" value={saisie} onChange={(e) => setSaisie(e.target.value)} />
        </div>
        <Select className="w-auto" value={ingenieur} onChange={(e) => filtre(e.target.value, setIngenieur)} aria-label="Ingénieur">
          <option value="">Ingénieur : tous</option>
          {estIngenieur && user && <option value={user.id}>Mes études</option>}
          {data?.ingenieurs.filter((i) => i.id !== user?.id).map((i) => <option key={i.id} value={i.id}>{i.nom}</option>)}
        </Select>
      </div>

      {erreur && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}

      <div className="overflow-x-auto rounded-xl border border-bordure bg-white">
        <table className="w-full text-left">
          <thead className="whitespace-nowrap border-b border-bordure bg-fond text-[12px] text-secondaire">
            <tr>
              <th className="px-4 py-3 font-semibold">Dalle</th>
              <th className="px-4 py-3 font-semibold">Projet</th>
              <th className="px-4 py-3 font-semibold">Niveau</th>
              <th className="px-4 py-3 text-right font-semibold">Surface</th>
              <th className="px-4 py-3 font-semibold">Ingénieurs</th>
              <th className="px-4 py-3 font-semibold">Pièces</th>
              <th className="px-4 py-3 font-semibold">État</th>
            </tr>
          </thead>
          <tbody>
            {data?.items.map((e) => (
              <tr key={e.dalleId} tabIndex={0} onClick={() => setOuverte(e.dalleId)}
                onKeyDown={(ev) => { if (ev.key === "Enter") setOuverte(e.dalleId); }}
                className="cursor-pointer border-b border-bordure last:border-0 hover:bg-fond">
                <td className="whitespace-nowrap px-4 py-3 font-display font-bold text-marine">{e.reference}</td>
                <td className="min-w-[220px] px-4 py-3">
                  <div className="font-semibold">{e.projetLibelle}</div>
                  {e.ville && <div className="text-[13px] text-secondaire">{e.ville}</div>}
                </td>
                <td className="whitespace-nowrap px-4 py-3 text-secondaire">{e.niveauLabel}</td>
                <td className="whitespace-nowrap px-4 py-3 text-right font-display">{e.surface > 0 ? formatSurface(e.surface) : "À renseigner"}</td>
                <td className="px-4 py-3">
                  {e.ingenieurs.length === 0 ? <span className="text-secondaire">Aucun</span> : e.ingenieurs.map((i) => i.nom).join(", ")}
                </td>
                <td className="whitespace-nowrap px-4 py-3">
                  <Pieces pieces={data.pieces} deposees={e.pieces} />
                </td>
                <td className="whitespace-nowrap px-4 py-3"><Badge ton={TON[e.etat]}>{e.etatLabel}</Badge></td>
              </tr>
            ))}
          </tbody>
        </table>
        {data && data.items.length === 0 && (
          <div className="px-6 py-14 text-center">
            <div className="font-display font-bold">Aucune étude ne correspond</div>
            <div className="text-secondaire">Chaque dalle créée dans un projet apparaît ici. Modifiez la recherche ou les filtres.</div>
          </div>
        )}
        {!data && !erreur && <AttenteLignes />}
      </div>

      {data && (
        <div className="mt-4 flex items-center justify-between gap-3 text-[13px] text-secondaire">
          <span>{data.total} étude(s)</span>
          {data.total > data.pageSize && (
            <span className="flex items-center gap-3">
              Page {page} sur {pages}
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>Précédent</Button>
              <Button variant="outline" size="sm" disabled={page >= pages} onClick={() => setPage(page + 1)}>Suivant</Button>
            </span>
          )}
        </div>
      )}

      {ouverte && data && (
        <FicheEtude dalleId={ouverte} pieces={data.pieces} ingenieurs={data.ingenieurs}
          onClose={() => { setOuverte(null); charger(); }} />
      )}
    </div>
  );
}

/** Les quatre pièces de l'étude, dans l'ordre du travail : pleine quand un fichier est déposé. */
function Pieces({ pieces, deposees }: { pieces: Piece[]; deposees: string[] }) {
  const nb = pieces.filter((p) => deposees.includes(p.code)).length;
  return (
    <div className="flex items-center gap-0.5" title={pieces.map((p) => `${p.label} : ${deposees.includes(p.code) ? "déposé" : "manquant"}`).join("\n")}>
      {pieces.map((p) => (
        <span key={p.code} aria-hidden className={cn("h-2 w-5 rounded-sm", deposees.includes(p.code) ? "bg-[#1e9e58]" : "bg-bordure")} />
      ))}
      <span className="ml-1.5 font-display text-[12px] font-bold text-secondaire">{nb}/{pieces.length}</span>
    </div>
  );
}

function FicheEtude({ dalleId, pieces, ingenieurs, onClose }: { dalleId: string; pieces: Piece[]; ingenieurs: Personne[]; onClose: () => void }) {
  const [d, setD] = useState<Detail | null>(null);
  const [erreur, setErreur] = useState("");
  const [envoi, setEnvoi] = useState(false);
  const [confirmation, setConfirmation] = useState(false);
  // Les blocs de pièces sont rechargés après chaque action : leurs droits de dépôt en dépendent.
  const [version, setVersion] = useState(0);
  const [generation, setGeneration] = useState(false);

  useEffect(() => {
    api<Detail>(`/etudes/${dalleId}`).then(setD).catch((e) => setErreur(e instanceof ApiError ? e.message : "Chargement impossible."));
  }, [dalleId]);

  async function agir(chemin: string, corps?: object) {
    setEnvoi(true); setErreur("");
    try {
      setD(await api<Detail>(`/etudes/${dalleId}/${chemin}`, { method: "POST", body: JSON.stringify(corps ?? {}) }));
      setVersion((v) => v + 1);
    } catch (e) {
      setErreur(e instanceof ApiError ? e.message : "Action impossible.");
    } finally {
      setEnvoi(false); setConfirmation(false);
    }
  }

  const e = d?.etude;
  const libres = e ? ingenieurs.filter((i) => !e.ingenieurs.some((x) => x.userId === i.id)) : [];

  return (
    <Sheet open onOpenChange={(o) => !o && onClose()} title={e ? `Étude de la dalle ${e.reference}` : "Étude de dalle"}
      description={e ? `${e.projetLibelle}, ${e.niveauLabel}${e.surface > 0 ? `, ${formatSurface(e.surface)}` : ""}` : undefined}
      footer={e && (e.peutTerminer || e.peutRouvrir) ? (
        <>
          {e.peutRouvrir && <Button variant="outline" disabled={envoi} onClick={() => agir("rouvrir")}>Rouvrir l&apos;étude</Button>}
          {e.peutTerminer && <Button disabled={envoi} onClick={() => setConfirmation(true)}><Check size={16} />Déclarer l&apos;étude terminée</Button>}
        </>
      ) : undefined}>
      {erreur && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}
      {!e ? (!erreur && <AttenteFiche className="border-0 p-0" />) : (
        <div className="space-y-7">
          <div className="flex flex-wrap items-center gap-2">
            <Badge ton={TON[e.etat]}>{e.etatLabel}</Badge>
            {e.termineeLe && <span className="text-[13px] text-secondaire">Terminée par {e.termineePar} le {formatDateHeure(e.termineeLe)}. Les pièces sont figées.</span>}
            <Link href={`/projets/${e.projetId}#dalles`} className="ml-auto text-[13px] font-semibold text-marine hover:underline">Ouvrir le projet {e.projetCode}</Link>
          </div>

          <section>
            <h3 className="text-[15px] font-bold">Ingénieurs de l&apos;étude</h3>
            {e.ingenieurs.length === 0 ? (
              <p className="mt-2 text-[13px] text-secondaire">
                Aucun ingénieur n&apos;est affecté. {e.peutModifierIngenieurs ? "Choisissez-en un ci-dessous." : "L'ingénieur en chef, le chef de projet ou le directeur affecte les ingénieurs."}
              </p>
            ) : (
              <ul className="mt-2 flex flex-wrap gap-2">
                {e.ingenieurs.map((i) => (
                  <li key={i.userId} className="flex items-center gap-1.5 rounded-full border border-bordure-forte bg-fond py-1 pl-3 pr-1.5 text-[13px] font-semibold">
                    {i.nom}
                    <span className="font-normal text-secondaire">depuis le {formatDate(i.depuis)}</span>
                    {e.peutModifierIngenieurs ? (
                      <button type="button" disabled={envoi} aria-label={`Retirer ${i.nom} de l'étude`} title="Retirer de l'étude"
                        onClick={() => agir(`ingenieurs/${i.userId}/retirer`)}
                        className="cursor-pointer rounded-full p-1 text-secondaire hover:bg-rouge-fond hover:text-rouge-texte"><X size={14} /></button>
                    ) : <span className="w-1.5" />}
                  </li>
                ))}
              </ul>
            )}
            {e.peutModifierIngenieurs && libres.length > 0 && (
              <Select className="mt-3 w-auto" value="" disabled={envoi} aria-label="Affecter un ingénieur"
                onChange={(ev) => { if (ev.target.value) agir("ingenieurs", { userId: ev.target.value }); }}>
                <option value="">Affecter un ingénieur</option>
                {libres.map((i) => <option key={i.id} value={i.id}>{i.nom}</option>)}
              </Select>
            )}
          </section>

          <section>
            <h3 className="text-[15px] font-bold">Pièces de l&apos;étude</h3>
            <p className="mt-1 text-[13px] text-secondaire">Dans l&apos;ordre du travail : le plan reçu du client, le fond de plan préparé pour ADAPT, la sortie du calcul, puis le plan de principe.</p>
            <ol className="mt-3 space-y-4">
              {pieces.map((p, i) => (
                <li key={p.code} className="rounded-lg border border-bordure p-4">
                  <Documents compact key={`${p.code}-${version}`} contexte="etude_dalle" cibleId={dalleId} categorie={p.code} titre={`${i + 1}. ${p.label}`} />
                  {p.code === "fond_plan" && e.peutDeposer && (
                    <div className="mt-3 flex flex-wrap items-center gap-3">
                      <Button variant="outline" size="sm" onClick={() => setGeneration(true)}><Wand2 size={15} />Générer depuis le plan de coffrage</Button>
                      <span className="text-[12px] text-attenue">Dalle, poteaux, voiles et réservations proposés, à vérifier avant le dépôt.</span>
                    </div>
                  )}
                </li>
              ))}
            </ol>
          </section>

          {d.historique.length > 0 && (
            <section>
              <h3 className="text-[15px] font-bold">Historique des affectations</h3>
              <ul className="mt-2 divide-y divide-bordure text-[13px]">
                {d.historique.map((h, i) => (
                  <li key={i} className="py-2">
                    <span className="font-semibold">{h.nom}</span>, affecté par {h.par} le {formatDate(h.debut)}
                    {h.fin && <span className="text-secondaire">, retiré par {h.retirePar} le {formatDate(h.fin)}</span>}
                  </li>
                ))}
              </ul>
            </section>
          )}
        </div>
      )}

      {generation && e && (
        <GenerationFondPlan dalleId={dalleId} reference={e.reference} onClose={() => setGeneration(false)}
          onDepose={() => { setGeneration(false); setVersion((v) => v + 1); }} />
      )}

      <Modal open={confirmation} onOpenChange={setConfirmation} title="Déclarer l'étude terminée"
        description="Les pièces de l'étude seront figées et ses ingénieurs ne pourront plus être changés. L'ingénieur en chef, le chef de projet ou le directeur peut rouvrir l'étude."
        footer={
          <>
            <Button variant="outline" onClick={() => setConfirmation(false)}>Annuler</Button>
            <Button disabled={envoi} onClick={() => agir("terminer")}>Déclarer terminée</Button>
          </>
        } />
    </Sheet>
  );
}

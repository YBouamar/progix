"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { Check, ChevronLeft, ChevronRight, Plus, Search, X } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { cn, formatDate, formatDateHeure, formatSurface } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field, Input, Select } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";
import { Sheet } from "@/components/ui/sheet";
import { AttenteFiche, AttenteLignes } from "@/components/ui/attente";
import { Documents } from "@/components/documents";

type Etat = "a_affecter" | "a_lancer" | "en_cours" | "termine" | "arrete";
type Execution = {
  dalleId: string; projetId: string; projetCode: string; projetLibelle: string; ville: string | null;
  reference: string; niveauLabel: string; surface: number; etat: Etat; etatLabel: string;
  dossier: { id: string; lettre: string; type: string; typeLabel: string; termineLe: string | null; envoyeLe: string | null } | null;
  dossiers: number; dessinateurs: { userId: string; nom: string; depuis: string }[];
  prevue: string | null; enRetard: boolean; fige: boolean; peutAffecter: boolean; peutOuvrir: boolean;
};
type Dossier = {
  id: string; lettre: string; reference: string; type: "exe" | "modif"; typeLabel: string; etat: Etat; etatLabel: string; commentaire: string | null;
  ouvertLe: string; ouvertPar: string; termineLe: string | null; terminePar: string | null; surface: number | null; dessinateurs: string | null;
  arretMotif: string | null; envoyeLe: string | null; pieces: string[]; dernier: boolean;
  peutDeposer: boolean; peutTerminer: boolean; peutRouvrir: boolean; peutArreter: boolean; peutReprendre: boolean; peutNoterEnvoi: boolean;
};
type Personne = { id: string; nom: string };
type Liste = {
  items: Execution[]; total: number; page: number; pageSize: number;
  compteurs: { etat: string; label: string; nb: number }[]; dessinateurs: Personne[]; peutAffecter: boolean;
};
type Detail = { execution: Execution; dossiers: Dossier[]; historique: { nom: string; par: string; debut: string; fin: string | null; retirePar: string | null }[] };
type Ligne = {
  jour: string; dalleId: string; dossierId: string | null; reference: string; projet: string; ville: string | null;
  client: string | null; bct: string | null; bet: string | null; niveauLabel: string; surface: number;
  type: "exe" | "modif"; typeLabel: string; etat: Etat; etatLabel: string; dessinateurs: string; ingenieurs: string;
  envoyeLe: string | null; arretMotif: string | null; enRetard: boolean;
};
type Jours = { du: string; au: string; aujourdhui: string; jours: { jour: string; lignes: Ligne[]; surfaceExe: number; surfaceModif: number }[] };

const TON = { a_affecter: "orange", a_lancer: "neutre", en_cours: "violet", termine: "vert", arrete: "rouge" } as const;
const PIECES: [string, string][] = [["plan", "Plan d'exécution"], ["modele", "Modèle de calcul"], ["autre", "Autres fichiers"]];

/** Menu Exécution : les dalles à traiter par les dessinateurs, leurs dossiers EXE et MODIF, et le suivi par jour. */
export default function ExecutionPage() {
  const [vue, setVue] = useState<"dalles" | "jours">("dalles");
  const [ouverte, setOuverte] = useState<string | null>(null);
  // Incrémenté à la fermeture d'une fiche : les deux vues se rechargent.
  const [maj, setMaj] = useState(0);
  const [dessinateurs, setDessinateurs] = useState<Personne[]>([]);

  return (
    <div>
      <div role="tablist" aria-label="Vue de l'exécution" className="mb-5 flex gap-6 border-b border-bordure">
        {([["dalles", "Dalles à traiter"], ["jours", "Suivi par jour"]] as const).map(([code, label]) => (
          <button key={code} role="tab" aria-selected={vue === code} onClick={() => setVue(code)}
            className={cn("-mb-px cursor-pointer border-b-2 pb-2.5 text-[14px] font-semibold",
              vue === code ? "border-marine text-marine" : "border-transparent text-secondaire hover:text-texte")}>
            {label}
          </button>
        ))}
      </div>

      {vue === "dalles"
        ? <ListeDalles maj={maj} onOuvrir={setOuverte} onDessinateurs={setDessinateurs} />
        : <SuiviParJour maj={maj} onOuvrir={setOuverte} />}

      {ouverte && <FicheExecution dalleId={ouverte} dessinateurs={dessinateurs} onClose={() => { setOuverte(null); setMaj((m) => m + 1); }} />}
    </div>
  );
}

function ListeDalles({ maj, onOuvrir, onDessinateurs }: { maj: number; onOuvrir: (id: string) => void; onDessinateurs: (p: Personne[]) => void }) {
  const { user } = useAuth();
  const [etat, setEtat] = useState("");
  const [dessinateur, setDessinateur] = useState("");
  const [saisie, setSaisie] = useState("");
  const [q, setQ] = useState("");
  const [page, setPage] = useState(1);
  const [data, setData] = useState<Liste | null>(null);
  const [erreur, setErreur] = useState("");

  useEffect(() => {
    const t = setTimeout(() => { setQ(saisie.trim()); setPage(1); }, 300);
    return () => clearTimeout(t);
  }, [saisie]);

  const charger = useCallback(async () => {
    try {
      const p = new URLSearchParams({ page: String(page) });
      if (etat) p.set("etat", etat);
      if (q) p.set("q", q);
      if (dessinateur) p.set("dessinateur", dessinateur);
      const liste = await api<Liste>(`/execution?${p}`);
      setData(liste); onDessinateurs(liste.dessinateurs);
      setErreur("");
    } catch (e) {
      setErreur(e instanceof ApiError ? e.message : "Chargement impossible.");
    }
  }, [page, etat, q, dessinateur, onDessinateurs]);

  useEffect(() => { charger(); }, [charger, maj]);

  const compteurs = data?.compteurs ?? [];
  const onglets = [{ etat: "", label: "Toutes", nb: compteurs.reduce((s, c) => s + c.nb, 0) }, ...compteurs];
  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;
  const filtre = (v: string, set: (v: string) => void) => { set(v); setPage(1); };
  const estDessinateur = !!user && data?.dessinateurs.some((i) => i.id === user.id);

  return (
    <>
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
        <Select className="w-auto" value={dessinateur} onChange={(e) => filtre(e.target.value, setDessinateur)} aria-label="Dessinateur">
          <option value="">Dessinateur : tous</option>
          {estDessinateur && user && <option value={user.id}>Mes dalles</option>}
          {data?.dessinateurs.filter((i) => i.id !== user?.id).map((i) => <option key={i.id} value={i.id}>{i.nom}</option>)}
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
              <th className="px-4 py-3 font-semibold">Dessinateurs</th>
              <th className="px-4 py-3 font-semibold">Prévue le</th>
              <th className="px-4 py-3 font-semibold">Dossier</th>
              <th className="px-4 py-3 font-semibold">État</th>
            </tr>
          </thead>
          <tbody>
            {data?.items.map((e) => (
              <tr key={e.dalleId} tabIndex={0} onClick={() => onOuvrir(e.dalleId)}
                onKeyDown={(ev) => { if (ev.key === "Enter") onOuvrir(e.dalleId); }}
                className="cursor-pointer border-b border-bordure last:border-0 hover:bg-fond">
                <td className="whitespace-nowrap px-4 py-3 font-display font-bold text-marine">{e.reference}</td>
                <td className="min-w-[220px] px-4 py-3">
                  <div className="font-semibold">{e.projetLibelle}</div>
                  {e.ville && <div className="text-[13px] text-secondaire">{e.ville}</div>}
                </td>
                <td className="whitespace-nowrap px-4 py-3 text-secondaire">{e.niveauLabel}</td>
                <td className="whitespace-nowrap px-4 py-3 text-right font-display">{e.surface > 0 ? formatSurface(e.surface) : "À renseigner"}</td>
                <td className="px-4 py-3">
                  {e.dessinateurs.length === 0 ? <span className="text-secondaire">Aucun</span> : e.dessinateurs.map((i) => i.nom).join(" + ")}
                </td>
                <td className="whitespace-nowrap px-4 py-3">
                  {e.prevue ? <span className={cn(e.enRetard && "font-semibold text-rouge-texte")}>{formatDate(e.prevue)}{e.enRetard && ", en retard"}</span> : <span className="text-secondaire">Non planifiée</span>}
                </td>
                <td className="whitespace-nowrap px-4 py-3">
                  {e.dossier ? <span className="font-semibold">{e.dossier.typeLabel} {e.dossier.lettre}</span> : <span className="text-secondaire">Aucun</span>}
                </td>
                <td className="whitespace-nowrap px-4 py-3"><Badge ton={TON[e.etat]}>{e.etatLabel}</Badge></td>
              </tr>
            ))}
          </tbody>
        </table>
        {data && data.items.length === 0 && (
          <div className="px-6 py-14 text-center">
            <div className="font-display font-bold">Aucune dalle ne correspond</div>
            <div className="text-secondaire">Chaque dalle créée dans un projet apparaît ici. Modifiez la recherche ou les filtres.</div>
          </div>
        )}
        {!data && !erreur && <AttenteLignes />}
      </div>

      {data && (
        <div className="mt-4 flex items-center justify-between gap-3 text-[13px] text-secondaire">
          <span>{data.total} dalle(s)</span>
          {data.total > data.pageSize && (
            <span className="flex items-center gap-3">
              Page {page} sur {pages}
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>Précédent</Button>
              <Button variant="outline" size="sm" disabled={page >= pages} onClick={() => setPage(page + 1)}>Suivant</Button>
            </span>
          )}
        </div>
      )}
    </>
  );
}

const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
const decale = (jour: string, n: number) => { const d = new Date(`${jour}T12:00:00`); d.setDate(d.getDate() + n); return iso(d); };
const jourLong = (jour: string) => {
  const s = new Date(`${jour}T12:00:00`).toLocaleDateString("fr-FR", { weekday: "long", day: "2-digit", month: "long", year: "numeric" });
  return s.charAt(0).toUpperCase() + s.slice(1);
};

/** Suivi par jour : les dossiers ouverts chaque jour et les dalles planifiées ce jour-là, semaine par semaine. */
function SuiviParJour({ maj, onOuvrir }: { maj: number; onOuvrir: (id: string) => void }) {
  const [au, setAu] = useState(() => iso(new Date()));
  const [data, setData] = useState<Jours | null>(null);
  const [erreur, setErreur] = useState("");
  const du = decale(au, -6);

  useEffect(() => {
    api<Jours>(`/execution/jours?du=${du}&au=${au}`).then((j) => { setData(j); setErreur(""); })
      .catch((e) => setErreur(e instanceof ApiError ? e.message : "Chargement impossible."));
  }, [du, au, maj]);

  const lignes = data?.jours.flatMap((j) => j.lignes) ?? [];
  const total = (type: string) => data?.jours.reduce((s, j) => s + (type === "exe" ? j.surfaceExe : j.surfaceModif), 0) ?? 0;

  return (
    <>
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <Button variant="outline" size="sm" onClick={() => setAu(decale(au, -7))} aria-label="Semaine précédente"><ChevronLeft size={16} />Précédente</Button>
        <div className="font-display text-[15px] font-bold">Du {formatDate(du)} au {formatDate(au)}</div>
        <Button variant="outline" size="sm" disabled={!!data && au >= data.aujourdhui} onClick={() => setAu(decale(au, 7))} aria-label="Semaine suivante">Suivante<ChevronRight size={16} /></Button>
        <Input type="date" className="w-auto" aria-label="Dernier jour affiché" value={au} onChange={(e) => e.target.value && setAu(e.target.value)} />
        {data && lignes.length > 0 && (
          <div className="ml-auto text-[13px] text-secondaire">
            {lignes.filter((l) => l.dossierId && l.type === "exe").length} EXE, {formatSurface(total("exe"))} · {lignes.filter((l) => l.dossierId && l.type === "modif").length} MODIF, {formatSurface(total("modif"))}
          </div>
        )}
      </div>

      {erreur && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}
      {!data && !erreur && <div className="rounded-xl border border-bordure bg-white"><AttenteLignes /></div>}

      {data && data.jours.length === 0 && (
        <div className="rounded-xl border border-bordure bg-white px-6 py-14 text-center">
          <div className="font-display font-bold">Aucun dossier sur ces sept jours</div>
          <div className="text-secondaire">Un dossier apparaît le jour de son ouverture, une dalle planifiée le jour prévu. Remontez d&apos;une semaine ou planifiez une dalle.</div>
        </div>
      )}

      <div className="space-y-5">
        {data?.jours.map((j) => (
          <section key={j.jour} className="overflow-x-auto rounded-xl border border-bordure bg-white">
            <h3 className="flex flex-wrap items-baseline gap-3 border-b border-bordure bg-fond px-4 py-2.5 text-[14px] font-bold">
              {jourLong(j.jour)}
              <span className="text-[12px] font-normal text-secondaire">{j.lignes.length} ligne(s)</span>
            </h3>
            <table className="w-full text-left text-[13px]">
              <thead className="whitespace-nowrap border-b border-bordure text-[12px] text-secondaire">
                <tr>
                  <th className="px-4 py-2 font-semibold">Référence</th>
                  <th className="px-4 py-2 font-semibold">Projet</th>
                  <th className="px-4 py-2 font-semibold">Client, BCT, BET</th>
                  <th className="px-4 py-2 font-semibold">Niveau</th>
                  <th className="px-4 py-2 text-right font-semibold">Surface EXE</th>
                  <th className="px-4 py-2 text-right font-semibold">Surface MODIF</th>
                  <th className="px-4 py-2 font-semibold">Dessinateurs</th>
                  <th className="px-4 py-2 font-semibold">Ingénieurs</th>
                  <th className="px-4 py-2 font-semibold">État</th>
                  <th className="px-4 py-2 font-semibold">Envoi</th>
                </tr>
              </thead>
              <tbody>
                {j.lignes.map((l) => (
                  <tr key={l.dossierId ?? l.dalleId} tabIndex={0} onClick={() => onOuvrir(l.dalleId)}
                    onKeyDown={(ev) => { if (ev.key === "Enter") onOuvrir(l.dalleId); }}
                    className="cursor-pointer border-b border-bordure last:border-0 hover:bg-fond">
                    <td className="whitespace-nowrap px-4 py-2.5 font-display font-bold text-marine">{l.reference}</td>
                    <td className="min-w-[180px] px-4 py-2.5">
                      <div className="font-semibold">{l.projet}</div>
                      {l.ville && <div className="text-secondaire">{l.ville}</div>}
                    </td>
                    <td className="min-w-[160px] px-4 py-2.5 text-secondaire">{[l.client, l.bct, l.bet].filter(Boolean).join(", ") || "Non renseignés"}</td>
                    <td className="whitespace-nowrap px-4 py-2.5 text-secondaire">{l.niveauLabel}</td>
                    <td className="whitespace-nowrap px-4 py-2.5 text-right font-display">{l.type === "exe" && l.surface > 0 ? formatSurface(l.surface) : ""}</td>
                    <td className="whitespace-nowrap px-4 py-2.5 text-right font-display">{l.type === "modif" && l.surface > 0 ? formatSurface(l.surface) : ""}</td>
                    <td className="px-4 py-2.5">{l.dessinateurs || <span className="text-secondaire">Aucun</span>}</td>
                    <td className="px-4 py-2.5">{l.ingenieurs || <span className="text-secondaire">Aucun</span>}</td>
                    <td className="whitespace-nowrap px-4 py-2.5">
                      {l.dossierId ? <Badge ton={TON[l.etat]}>{l.typeLabel} : {l.etatLabel.toLowerCase()}</Badge>
                        : <Badge ton={l.enRetard ? "rouge" : TON[l.etat]}>{l.enRetard ? "Planifiée, en retard" : `Planifiée, ${l.etatLabel.toLowerCase()}`}</Badge>}
                    </td>
                    <td className="whitespace-nowrap px-4 py-2.5 text-secondaire">{l.envoyeLe ? formatDate(l.envoyeLe) : ""}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </section>
        ))}
      </div>
    </>
  );
}

function FicheExecution({ dalleId, dessinateurs, onClose }: { dalleId: string; dessinateurs: Personne[]; onClose: () => void }) {
  const [d, setD] = useState<Detail | null>(null);
  const [personnes, setPersonnes] = useState(dessinateurs);
  const [erreur, setErreur] = useState("");
  const [envoi, setEnvoi] = useState(false);
  // Les blocs de fichiers sont rechargés après chaque action : leurs droits de dépôt en dépendent.
  const [version, setVersion] = useState(0);
  const [ouverture, setOuverture] = useState(false);
  const [commentaire, setCommentaire] = useState("");
  const [cloture, setCloture] = useState<Dossier | null>(null);
  const [arret, setArret] = useState<Dossier | null>(null);
  const [motif, setMotif] = useState("");

  useEffect(() => {
    api<Detail>(`/execution/${dalleId}`).then(setD).catch((e) => setErreur(e instanceof ApiError ? e.message : "Chargement impossible."));
  }, [dalleId]);
  // Ouverte depuis le suivi par jour, la fiche charge elle-même la liste des dessinateurs.
  useEffect(() => {
    if (personnes.length === 0) api<Liste>("/execution?page=1").then((l) => setPersonnes(l.dessinateurs)).catch(() => {});
  }, [personnes.length]);

  async function agir(chemin: string, corps?: object, methode = "POST") {
    setEnvoi(true); setErreur("");
    try {
      setD(await api<Detail>(`/execution/${chemin}`, { method: methode, body: JSON.stringify(corps ?? {}) }));
      setVersion((v) => v + 1);
      return true;
    } catch (e) {
      setErreur(e instanceof ApiError ? e.message : "Action impossible.");
      return false;
    } finally {
      setEnvoi(false); setCloture(null);
    }
  }

  const e = d?.execution;
  const libres = e ? personnes.filter((i) => !e.dessinateurs.some((x) => x.userId === i.id)) : [];
  const prochain = e && e.dossiers > 0 ? "une modification" : "le dossier EXE";

  return (
    <Sheet open onOpenChange={(o) => !o && onClose()} title={e ? `Exécution de la dalle ${e.reference}` : "Exécution de dalle"}
      description={e ? `${e.projetLibelle}, ${e.niveauLabel}${e.surface > 0 ? `, ${formatSurface(e.surface)}` : ""}` : undefined}
      footer={e?.peutOuvrir ? <Button disabled={envoi} onClick={() => { setCommentaire(""); setOuverture(true); }}><Plus size={16} />Ouvrir {prochain}</Button> : undefined}>
      {erreur && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}
      {!e ? (!erreur && <AttenteFiche className="border-0 p-0" />) : (
        <div className="space-y-7">
          <div className="flex flex-wrap items-center gap-2">
            <Badge ton={TON[e.etat]}>{e.etatLabel}</Badge>
            {e.fige && <span className="text-[13px] text-secondaire">Plans figés depuis la validation du BCT.</span>}
            <Link href={`/projets/${e.projetId}#dalles`} className="ml-auto text-[13px] font-semibold text-marine hover:underline">Ouvrir le projet {e.projetCode}</Link>
          </div>

          <section>
            <h3 className="text-[15px] font-bold">Dessinateurs de la dalle</h3>
            {e.dessinateurs.length === 0 ? (
              <p className="mt-2 text-[13px] text-secondaire">
                Aucun dessinateur n&apos;est affecté. {e.peutAffecter ? "Choisissez-en un ci-dessous." : "Le dessinateur en chef, le chef de projet ou le directeur affecte les dessinateurs."}
              </p>
            ) : (
              <ul className="mt-2 flex flex-wrap gap-2">
                {e.dessinateurs.map((i) => (
                  <li key={i.userId} className="flex items-center gap-1.5 rounded-full border border-bordure-forte bg-fond py-1 pl-3 pr-1.5 text-[13px] font-semibold">
                    {i.nom}
                    <span className="font-normal text-secondaire">depuis le {formatDate(i.depuis)}</span>
                    {e.peutAffecter ? (
                      <button type="button" disabled={envoi} aria-label={`Retirer ${i.nom} de la dalle`} title="Retirer de la dalle"
                        onClick={() => agir(`${dalleId}/dessinateurs/${i.userId}/retirer`)}
                        className="cursor-pointer rounded-full p-1 text-secondaire hover:bg-rouge-fond hover:text-rouge-texte"><X size={14} /></button>
                    ) : <span className="w-1.5" />}
                  </li>
                ))}
              </ul>
            )}
            {e.dessinateurs.length > 1 && <p className="mt-2 text-[12px] text-attenue">Un dossier partagé compte à parts égales pour chacun, dossier et surface.</p>}
            {e.peutAffecter && (
              <div className="mt-3 flex flex-wrap items-end gap-3">
                {libres.length > 0 && (
                  <Select className="w-auto" value="" disabled={envoi} aria-label="Affecter un dessinateur"
                    onChange={(ev) => { if (ev.target.value) agir(`${dalleId}/dessinateurs`, { userId: ev.target.value }); }}>
                    <option value="">Affecter un dessinateur</option>
                    {libres.map((i) => <option key={i.id} value={i.id}>{i.nom}</option>)}
                  </Select>
                )}
                <Field label="Jour prévu" className="w-auto">
                  <Input type="date" className="w-auto" disabled={envoi} value={e.prevue ?? ""}
                    onChange={(ev) => agir(`${dalleId}/prevue`, { date: ev.target.value || null }, "PUT")} />
                </Field>
              </div>
            )}
            {!e.peutAffecter && e.prevue && <p className="mt-2 text-[13px] text-secondaire">Jour prévu : {formatDate(e.prevue)}.</p>}
          </section>

          <section>
            <h3 className="text-[15px] font-bold">Dossiers</h3>
            <p className="mt-1 text-[13px] text-secondaire">Chaque version des plans est un dossier : la première un EXE, les suivantes des MODIF. Un dossier se termine avec son plan et son modèle de calcul.</p>
            {d.dossiers.length === 0 && <p className="mt-3 rounded-lg border border-dashed border-bordure-forte px-4 py-6 text-center text-[13px] text-secondaire">Aucun dossier ouvert. {e.peutOuvrir ? "Ouvrez le dossier EXE pour déposer les plans." : "Un dessinateur affecté à la dalle ouvre le dossier EXE."}</p>}
            <ol className="mt-3 space-y-4">
              {d.dossiers.map((v) => (
                <li key={v.id} className="rounded-lg border border-bordure p-4">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-display text-[15px] font-bold">{v.typeLabel} {v.lettre}</span>
                    <Badge ton={TON[v.etat]}>{v.etatLabel}</Badge>
                    <span className="text-[12px] text-secondaire">Ouvert par {v.ouvertPar} le {formatDateHeure(v.ouvertLe)}</span>
                  </div>
                  {v.commentaire && <p className="mt-1 text-[13px]">{v.commentaire}</p>}
                  {v.etat === "termine" && (
                    <p className="mt-1 text-[13px] text-secondaire">
                      Terminé par {v.terminePar} le {formatDateHeure(v.termineLe)}{v.surface ? `, ${formatSurface(v.surface)}` : ""}{v.dessinateurs ? `, ${v.dessinateurs}` : ""}.
                    </p>
                  )}
                  {v.etat === "arrete" && <p className="mt-1 text-[13px] text-rouge-texte">Arrêté : {v.arretMotif}</p>}

                  {(v.dernier || v.pieces.length > 0) && PIECES.map(([code, label]) => (
                    <div key={code} className="mt-3">
                      <Documents compact key={`${v.id}-${code}-${version}`} contexte="plan_execution" cibleId={v.id} categorie={code} titre={label} lectureSeule={!v.peutDeposer} />
                    </div>
                  ))}

                  {(v.peutTerminer || v.peutRouvrir || v.peutArreter || v.peutReprendre || v.peutNoterEnvoi) && (
                    <div className="mt-4 flex flex-wrap items-end gap-3 border-t border-bordure pt-3">
                      {v.peutNoterEnvoi && (
                        <Field label="Date d'envoi des plans" className="w-auto">
                          <Input type="date" className="w-auto" disabled={envoi} value={v.envoyeLe ?? ""}
                            onChange={(ev) => agir(`dossiers/${v.id}/envoi`, { date: ev.target.value || null }, "PUT")} />
                        </Field>
                      )}
                      {v.peutTerminer && <Button size="sm" disabled={envoi} onClick={() => setCloture(v)}><Check size={15} />Terminer le dossier</Button>}
                      {v.peutArreter && <Button size="sm" variant="outline" disabled={envoi} onClick={() => { setMotif(""); setArret(v); }}>Arrêter</Button>}
                      {v.peutReprendre && <Button size="sm" variant="outline" disabled={envoi} onClick={() => agir(`dossiers/${v.id}/reprendre`)}>Reprendre le dossier</Button>}
                      {v.peutRouvrir && <Button size="sm" variant="outline" disabled={envoi} onClick={() => agir(`dossiers/${v.id}/rouvrir`)}>Rouvrir le dossier</Button>}
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

      <Modal open={ouverture} onOpenChange={setOuverture} title={`Ouvrir ${prochain}`}
        description="Une nouvelle version des plans est créée pour la dalle : déposez-y le plan d'exécution et le modèle de calcul."
        footer={
          <>
            <Button variant="outline" onClick={() => setOuverture(false)}>Annuler</Button>
            <Button disabled={envoi} onClick={async () => { await agir(`${dalleId}/dossiers`, { commentaire }); setOuverture(false); }}>Ouvrir le dossier</Button>
          </>
        }>
        <Field label="Commentaire (facultatif)">
          <Input value={commentaire} maxLength={1000} onChange={(ev) => setCommentaire(ev.target.value)} placeholder="Objet de la modification, demande du client..." />
        </Field>
      </Modal>

      <Modal open={!!cloture} onOpenChange={(o) => !o && setCloture(null)} title={cloture ? `Terminer le dossier ${cloture.typeLabel} ${cloture.lettre}` : ""}
        description="La surface de la dalle et ses dessinateurs sont relevés sur le dossier, et ses fichiers figés. Le dessinateur en chef, le chef de projet ou le directeur peut le rouvrir."
        footer={
          <>
            <Button variant="outline" onClick={() => setCloture(null)}>Annuler</Button>
            <Button disabled={envoi} onClick={() => cloture && agir(`dossiers/${cloture.id}/terminer`)}>Terminer le dossier</Button>
          </>
        } />

      <Modal open={!!arret} onOpenChange={(o) => !o && setArret(null)} title={arret ? `Arrêter le dossier ${arret.typeLabel} ${arret.lettre}` : ""}
        description="Un dossier arrêté ne compte pas dans la production. Il peut être repris tant qu'aucun autre dossier n'est ouvert."
        footer={
          <>
            <Button variant="outline" onClick={() => setArret(null)}>Annuler</Button>
            <Button disabled={envoi || !motif.trim()} onClick={async () => { if (arret) { await agir(`dossiers/${arret.id}/arreter`, { motif }); setArret(null); } }}>Arrêter le dossier</Button>
          </>
        }>
        <Field label="Motif de l'arrêt" required>
          <Input value={motif} maxLength={500} onChange={(ev) => setMotif(ev.target.value)} placeholder="Projet suspendu, plans du client en attente..." />
        </Field>
      </Modal>
    </Sheet>
  );
}

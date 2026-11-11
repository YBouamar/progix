"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { Upload } from "lucide-react";
import { api } from "@/lib/api";
import { cn, formatDateHeure, formatNombre, formatSurface } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Select } from "@/components/ui/input";
import type { Adapt } from "../../projets/[id]/adapt";
import type { DevisDetail } from "../types";

type Dalle = { id: string; reference: string; niveauLabel: string; surface: number; adapt: Adapt | null };
type Resultat = {
  nom: string; genre: "pt" | "rebar" | null; etat: "rattache" | "attente" | "refuse"; dalleId: string | null; reference: string | null;
  message: string | null; surface: number; ratio: number; proposee: string | null; avertissements: string[];
};
/** Fichier lu mais pas rattaché : le fichier reste dans le navigateur jusqu'au choix de sa dalle. */
type Attente = { cle: number; fichier: File; lu: Resultat; dalleId: string };

const NOM = { pt: "PTTotal", rebar: "RebarTotal" } as const;
const avecPt = (a: Adapt | null) => !!a?.fichierPt;
const avecRebar = (a: Adapt | null) => !!a?.fichierRebar;

/** Vrai si les fichiers lus pour la dalle ne sont pas ceux que le chiffrage du devis reprend. */
export function aReprendre(ligne: DevisDetail["lignes"][number], a: Adapt | null) {
  if (!a) return false;
  const cables = (l: { torons: number; ancrages: number; gaine: number }[]) => [...l].sort((x, y) => x.torons - y.torons).map((s) => `${s.torons}:${s.ancrages}:${s.gaine}`).join("|");
  return (avecPt(a) && (ligne.ratioActif !== a.ratioActif || cables(ligne.systemes) !== cables(a.systemes))) || (avecRebar(a) && ligne.ratioPassif !== a.ratioPassif);
}

/**
 * Étape 1 de la fiche du devis : dépôt des exports ADAPT (PTTotal et RebarTotal) de toutes les dalles, d'un seul geste.
 * Chaque fichier est reconnu et lu par le serveur, puis rattaché à sa dalle par la surface du plancher ; dans le doute, on choisit la dalle ici.
 */
export function FichiersAdapt({ devis, modifiable, occupe, bloque, onGenerer, onSaisir, onImporte }: {
  devis: DevisDetail; modifiable: boolean; occupe: boolean;
  /** Motif qui empêche de générer le chiffrage (brouillon non enregistré), ou null. */
  bloque: string | null;
  onGenerer: () => void; onSaisir: () => void;
  /** Des fichiers viennent d'être rattachés : la fiche du devis se met à jour. */
  onImporte: () => void;
}) {
  const [dalles, setDalles] = useState<Dalle[] | null>(null);
  const [peutImporter, setPeutImporter] = useState(false);
  const [erreur, setErreur] = useState("");
  const [attente, setAttente] = useState<Attente[]>([]);
  const [refus, setRefus] = useState<Resultat[]>([]);
  const [notes, setNotes] = useState<string[]>([]);
  const [bilan, setBilan] = useState("");
  const [envoi, setEnvoi] = useState(false);
  const [survol, setSurvol] = useState(false);
  const champ = useRef<HTMLInputElement>(null);
  const champLigne = useRef<HTMLInputElement>(null);
  const cible = useRef<string | null>(null);
  const compteur = useRef(0);

  const ids = devis.lignes.map((l) => l.dalleId);
  useEffect(() => {
    api<{ dalles: Dalle[]; peutImporterAdapt: boolean }>(`/projets/${devis.projetId}/dalles`)
      .then((r) => { setDalles(r.dalles); setPeutImporter(r.peutImporterAdapt); })
      .catch(() => setErreur("Les dalles du projet n'ont pas pu être chargées."));
  }, [devis.projetId]);

  const ouvert = modifiable && peutImporter;

  /** Envoie des fichiers ; `dalleId` désigne la dalle de tous, sinon le serveur rapproche chacun par sa surface. */
  const envoyer = useCallback(async (fichiers: File[], dalleId?: string) => {
    if (fichiers.length === 0) return;
    setEnvoi(true); setErreur(""); setBilan(""); setRefus([]); setNotes([]);
    const corps = new FormData();
    fichiers.forEach((f) => { corps.append("fichiers", f); corps.append("dalles", dalleId ?? ""); });
    ids.forEach((i) => corps.append("candidates", i));
    try {
      const res = await fetch(`/api/projets/${devis.projetId}/dalles/adapt`, { method: "POST", body: corps, credentials: "include" });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) throw new Error(res.status === 403 ? "Votre rôle ne permet pas d'importer les fichiers ADAPT." : data.message ?? "Lecture des fichiers impossible.");
      const lus = data.fichiers as Resultat[];
      const maj = data.dalles as Dalle[];
      setDalles((prec) => (prec ?? []).map((d) => maj.find((m) => m.id === d.id) ?? d));
      const enAttente = lus.flatMap((lu, i) => (lu.etat === "attente" ? [{ cle: ++compteur.current, fichier: fichiers[i], lu, dalleId: lu.proposee ?? "" }] : []));
      setAttente((prec) => [...prec, ...enAttente]);
      setRefus(lus.filter((l) => l.etat === "refuse"));
      setNotes(lus.flatMap((l) => l.avertissements.map((m) => `${l.reference} : ${m}`)));
      const n = lus.filter((l) => l.etat === "rattache").length;
      if (n > 0) onImporte();
      setBilan(n === 0 ? "" : n === 1 ? "1 fichier lu et rattaché à sa dalle." : `${n} fichiers lus et rattachés à leur dalle.`);
      return lus;
    } catch (e) {
      setErreur(e instanceof Error ? e.message : "Lecture des fichiers impossible.");
    } finally { setEnvoi(false); }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [devis.projetId, ids.join(",")]);

  async function rattacher(a: Attente) {
    const lus = await envoyer([a.fichier], a.dalleId);
    if (lus?.[0]?.etat === "rattache") setAttente((prec) => prec.filter((x) => x.cle !== a.cle));
  }

  if (!dalles) return <div className="mt-5 rounded-xl border border-bordure bg-white p-8 text-center text-secondaire" role={erreur ? "alert" : "status"}>{erreur || "Chargement des dalles..."}</div>;

  const lignes = devis.lignes.map((l) => ({ l, d: dalles.find((x) => x.id === l.dalleId) ?? null }));
  const lusNb = lignes.reduce((n, { d }) => n + (avecPt(d?.adapt ?? null) ? 1 : 0) + (avecRebar(d?.adapt ?? null) ? 1 : 0), 0);
  const sansPt = lignes.filter(({ d }) => !avecPt(d?.adapt ?? null)).length;
  const reprise = lignes.some(({ l, d }) => aReprendre(l, d?.adapt ?? null));

  return (
    <div className="mt-5 space-y-5">
      {ouvert && (
        <section
          onDragOver={(e) => { e.preventDefault(); setSurvol(true); }} onDragLeave={() => setSurvol(false)}
          onDrop={(e) => { e.preventDefault(); setSurvol(false); if (!envoi) void envoyer(Array.from(e.dataTransfer.files)); }}
          className={cn("flex flex-wrap items-center gap-5 rounded-xl border-2 border-dashed bg-white px-6 py-7", survol ? "border-marine bg-fond" : "border-bordure-forte")}>
          <Upload size={36} className="shrink-0 text-marine" aria-hidden />
          <div className="min-w-[260px] flex-1">
            <h3 className="text-[16px] font-bold">Déposez ici les fichiers PTTotal et RebarTotal</h3>
            <p className="mt-1 max-w-[70ch] text-[13px] text-secondaire">
              Plusieurs fichiers à la fois, pour toutes les dalles (.rtf ou .rtf.doc). PROGIX reconnaît le type de chaque fichier, lit la surface, les ratios, les types d&apos;ancrage, les ancrages et les gaines, puis vous demande la dalle quand la surface ne suffit pas à la désigner.
            </p>
          </div>
          <input ref={champ} type="file" multiple accept=".doc,.rtf" className="hidden"
            onChange={(e) => { const f = Array.from(e.target.files ?? []); e.target.value = ""; void envoyer(f); }} />
          <input ref={champLigne} type="file" accept=".doc,.rtf" className="hidden"
            onChange={(e) => { const f = Array.from(e.target.files ?? []); e.target.value = ""; if (cible.current) void envoyer(f, cible.current); }} />
          <Button disabled={envoi} onClick={() => champ.current?.click()}>{envoi ? "Lecture..." : "Parcourir"}</Button>
        </section>
      )}

      {erreur && <div role="alert" className="rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}
      {bilan && <div role="status" className="rounded-lg bg-vert-fond px-4 py-3 text-vert-texte">{bilan}</div>}
      {refus.length > 0 && (
        <div role="alert" className="space-y-1 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">
          <p className="font-bold">{refus.length === 1 ? "Un fichier n'a pas été repris" : `${refus.length} fichiers n'ont pas été repris`}</p>
          {refus.map((r, i) => <p key={i} className="text-[13px]"><span className="font-semibold">{r.nom}</span> : {r.message}</p>)}
        </div>
      )}
      {notes.length > 0 && (
        <div role="status" className="space-y-1 rounded-lg bg-orange-fond px-4 py-3 text-[13px] text-orange-texte">
          {notes.map((m, i) => <p key={i}>{m}</p>)}
        </div>
      )}

      {attente.map((a) => (
        <section key={a.cle} className="flex flex-wrap items-end gap-3 rounded-xl bg-orange-fond px-5 py-4 text-orange-texte">
          <div className="min-w-[260px] flex-1">
            <div className="font-bold">Un fichier attend sa dalle</div>
            <div className="text-[13px]">
              {a.lu.nom}, {a.lu.genre ? NOM[a.lu.genre] : "export"} : surface lue {formatSurface(a.lu.surface)}, ratio {a.lu.genre === "rebar" ? "passif" : "actif"} {formatNombre(a.lu.ratio)} kg/m²
            </div>
          </div>
          <label className="flex flex-col gap-1 text-[12px]">Dalle
            <Select className="h-10 w-64 text-texte" value={a.dalleId} onChange={(e) => setAttente((prec) => prec.map((x) => (x.cle === a.cle ? { ...x, dalleId: e.target.value } : x)))}>
              <option value="">Choisir la dalle...</option>
              {lignes.map(({ l, d }) => <option key={l.dalleId} value={l.dalleId}>{l.reference}, {l.emplacement}, {formatSurface(d?.surface ?? l.surface)}</option>)}
            </Select>
          </label>
          <Button variant="outline" disabled={envoi || !a.dalleId} onClick={() => rattacher(a)}>Rattacher</Button>
          <Button variant="ghost" disabled={envoi} onClick={() => setAttente((prec) => prec.filter((x) => x.cle !== a.cle))}>Écarter</Button>
        </section>
      ))}

      <section className="overflow-x-auto rounded-xl border border-bordure bg-white">
        <table className="w-full min-w-[980px] text-left">
          <thead className="bg-fond text-[12px] text-secondaire">
            <tr>
              <th className="px-4 py-3 font-semibold">Dalle</th>
              <th className="px-4 py-3 font-semibold">PTTotal</th>
              <th className="px-4 py-3 font-semibold">RebarTotal</th>
              <th className="px-4 py-3 text-right font-semibold">Surface</th>
              <th className="px-4 py-3 text-right font-semibold">Ratio actif</th>
              <th className="px-4 py-3 text-right font-semibold">Ratio passif</th>
              <th className="px-4 py-3 font-semibold">Ancrages et gaines lus</th>
              <th className="px-4 py-3 font-semibold">État</th>
            </tr>
          </thead>
          <tbody>
            {lignes.map(({ l, d }) => {
              const a = d?.adapt ?? null;
              const pt = avecPt(a), rebar = avecRebar(a);
              const surface = d?.surface ?? l.surface;
              const ecart = a ? Math.round((a.surfacePlancher - surface) * 100) / 100 : 0;
              const fichier = (genre: "pt" | "rebar") => {
                const present = genre === "pt" ? pt : rebar;
                const nom = genre === "pt" ? a?.fichierPt : a?.fichierRebar;
                const id = genre === "pt" ? a?.fichierPtId : a?.fichierRebarId;
                const choisir = () => { cible.current = l.dalleId; champLigne.current?.click(); };
                return (
                  <td className="max-w-[220px] px-4 py-3.5 align-top text-[13px]">
                    {present ? (
                      <>
                        {id
                          ? <a href={`/api/fichiers/${id}/contenu`} className="block truncate font-semibold text-marine hover:underline" title={`Ouvrir ${nom}`}>{nom}</a>
                          : <div className="truncate font-semibold" title={nom ?? undefined}>{nom}</div>}
                        <div className="text-vert-texte">Lu le {formatDateHeure(a!.importeLe)}</div>
                        {ouvert && <button type="button" disabled={envoi} onClick={choisir} className="cursor-pointer text-[12px] font-semibold text-marine hover:underline disabled:opacity-50" aria-label={`Remplacer le ${NOM[genre]} de la dalle ${l.reference}`}>Remplacer</button>}
                      </>
                    ) : (
                      <>
                        <div className="font-semibold text-orange-texte">Manquant</div>
                        {ouvert && <button type="button" disabled={envoi} onClick={choisir} className="cursor-pointer text-[12px] font-semibold text-marine hover:underline disabled:opacity-50" aria-label={`Déposer le ${NOM[genre]} de la dalle ${l.reference}`}>Déposer le fichier</button>}
                      </>
                    )}
                  </td>
                );
              };
              return (
                <tr key={l.id} className="border-t border-bordure">
                  <td className="whitespace-nowrap px-4 py-3.5 align-top">
                    <div className="font-display font-bold text-marine">{l.reference}</div>
                    <div className="text-[13px] text-secondaire">{l.emplacement}</div>
                  </td>
                  {fichier("pt")}
                  {fichier("rebar")}
                  <td className="whitespace-nowrap px-4 py-3.5 text-right align-top">
                    <div className="font-display font-bold">{surface > 0 ? formatSurface(surface) : "À renseigner"}</div>
                    {a && <div className={cn("text-[12px]", ecart !== 0 && surface > 0 ? "text-orange-texte" : "text-secondaire")}>
                      Fichier : {formatNombre(a.surfacePlancher)}{ecart !== 0 && surface > 0 ? `, écart de ${formatNombre(Math.abs(ecart))}` : ""}
                    </div>}
                  </td>
                  <td className="px-4 py-3.5 text-right align-top font-display font-bold">{pt ? formatNombre(a!.ratioActif) : <span className="font-sans font-normal text-secondaire">À lire</span>}</td>
                  <td className="px-4 py-3.5 text-right align-top font-display font-bold">{rebar ? formatNombre(a!.ratioPassif) : <span className="font-sans font-normal text-secondaire">À lire</span>}</td>
                  <td className="px-4 py-3.5 align-top text-[13px]">
                    {pt ? a!.systemes.map((s) => <div key={s.torons}>S{s.torons} : {s.ancrages} ancrage{s.ancrages > 1 ? "s" : ""}, {formatNombre(s.gaine)} m</div>) : <span className="text-secondaire">À lire dans PTTotal</span>}
                  </td>
                  <td className="px-4 py-3.5 align-top">
                    {pt && rebar ? <Badge ton="vert">Complet</Badge>
                      : pt ? <Badge ton="orange">RebarTotal manquant</Badge>
                      : rebar ? <Badge ton="orange">PTTotal manquant</Badge>
                      : <Badge>Aucun fichier</Badge>}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </section>

      <p className="max-w-[80ch] text-[13px] text-secondaire">
        La surface reste celle de la dalle : un écart avec le fichier est signalé, jamais appliqué d&apos;office. Une dalle sans surface prend celle du fichier.
        Un type d&apos;ancrage absent de la liste ({devis.typesAncrage.map((t) => `S${t}`).join(", ")}) refuse le fichier et dit pourquoi.
      </p>

      {modifiable && (
        <div className="flex flex-wrap items-center gap-3 rounded-xl border border-bordure bg-white px-5 py-4">
          <div className="min-w-[260px] flex-1 text-[13px] text-secondaire">
            {lusNb === 0 ? "Aucun fichier lu pour l'instant. Vous pouvez aussi saisir les ratios, les ancrages et les gaines à la main."
              : reprise ? `${lusNb} fichier${lusNb > 1 ? "s" : ""} lu${lusNb > 1 ? "s" : ""} sur ${lignes.length * 2}. Le chiffrage ne reprend pas encore toutes ces données : générez-le.${sansPt > 0 ? " Le prix se calcule avec le ratio actif : les dalles sans PTTotal resteront à compléter." : ""}`
              : `${lusNb} fichier${lusNb > 1 ? "s" : ""} lu${lusNb > 1 ? "s" : ""} sur ${lignes.length * 2}. Le chiffrage reprend déjà ces données.`}
          </div>
          <Button variant="outline" onClick={onSaisir}>{lusNb === 0 ? "Saisir à la main" : "Voir le chiffrage"}</Button>
          <Button disabled={occupe || envoi || lusNb === 0 || !!bloque} title={bloque ?? (lusNb === 0 ? "Déposez d'abord au moins un fichier ADAPT" : undefined)} onClick={onGenerer}>
            {reprise || lusNb === 0 ? "Générer le chiffrage" : "Régénérer le chiffrage"}
          </Button>
        </div>
      )}
    </div>
  );
}

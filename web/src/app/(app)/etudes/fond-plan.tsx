"use client";

import { useCallback, useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { cn, formatSurface, formatTaille } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Select } from "@/components/ui/input";
import { Sheet } from "@/components/ui/sheet";
import { AttenteFiche } from "@/components/ui/attente";

type Fichier = { id: string; nom: string; taille: number };
type Role = { code: string; label: string };
type Calque = { nom: string; role: string; propose: string; formes: number; dansNiveau: number };
type Niveau = { x: number; y: number; nom: string; surface: number; poteaux: number; voiles: number; reservations: number; choisi: boolean };
type Points = [number, number][];
type Apercu = { largeur: number; hauteur: number; surface: number; dalle: Points; poteaux: Points[]; voiles: Points[]; reservations: Points[]; ronds: { x: number; y: number; r: number }[] };
type Analyse = { roles: Role[]; calques: Calque[]; niveaux: Niveau[]; apercu: Apercu | null };

const estPlan = (nom: string) => /\.(dwg|dxf)$/i.test(nom);

/**
 * Génération du fond de plan à partir du plan de coffrage déposé : choix du niveau, rôle des calques, aperçu, puis dépôt.
 * Le programme propose ; l'ingénieur confirme ou corrige avant de déposer.
 */
export function GenerationFondPlan({ dalleId, reference, onClose, onDepose }: { dalleId: string; reference: string; onClose: () => void; onDepose: () => void }) {
  const [fichiers, setFichiers] = useState<Fichier[] | null>(null);
  const [fichierId, setFichierId] = useState("");
  const [corrections, setCorrections] = useState<Record<string, string>>({});
  const [repere, setRepere] = useState<{ x: number; y: number } | null>(null);
  const [a, setA] = useState<Analyse | null>(null);
  const [erreur, setErreur] = useState("");
  const [travail, setTravail] = useState(false);
  const [tous, setTous] = useState(false);

  useEffect(() => {
    api<{ fichiers: Fichier[] }>(`/fichiers?contexte=etude_dalle&cibleId=${dalleId}&categorie=coffrage`)
      .then((r) => { const plans = r.fichiers.filter((f) => estPlan(f.nom)); setFichiers(plans); if (plans.length > 0) setFichierId(plans[0].id); })
      .catch(() => setFichiers([]));
  }, [dalleId]);

  const analyser = useCallback(async () => {
    if (!fichierId) return;
    setTravail(true); setErreur("");
    try {
      setA(await api<Analyse>(`/etudes/${dalleId}/fond-plan/analyse`, { method: "POST", body: JSON.stringify({ fichierId, calques: corrections, niveau: repere }) }));
    } catch (e) {
      setErreur(e instanceof ApiError ? e.message : "Analyse impossible.");
    } finally {
      setTravail(false);
    }
  }, [dalleId, fichierId, corrections, repere]);

  useEffect(() => { analyser(); }, [analyser]);

  async function deposer() {
    setTravail(true); setErreur("");
    try {
      await api(`/etudes/${dalleId}/fond-plan/deposer`, { method: "POST", body: JSON.stringify({ fichierId, calques: corrections, niveau: repere }) });
      onDepose();
    } catch (e) {
      setErreur(e instanceof ApiError ? e.message : "Dépôt impossible.");
      setTravail(false);
    }
  }

  const choisi = a?.niveaux.find((n) => n.choisi);
  const calques = a ? a.calques.filter((c) => tous || c.role !== "" || c.dansNiveau > 0) : [];
  const caches = a ? a.calques.length - calques.length : 0;

  return (
    <Sheet large open onOpenChange={(o) => !o && onClose()} title={`Fond de plan de la dalle ${reference}`}
      description="Le programme lit le plan de coffrage et propose le fond de plan. Vérifiez le niveau, le rôle des calques et l'aperçu avant de déposer."
      footer={
        <>
          <Button variant="outline" onClick={onClose}>Annuler</Button>
          <Button disabled={!a?.apercu || travail} onClick={deposer}>{travail && a?.apercu ? "Un instant..." : "Déposer le fond de plan"}</Button>
        </>
      }>
      {fichiers && fichiers.length === 0 && (
        <div className="rounded-lg border border-dashed border-bordure-forte px-6 py-10 text-center">
          <div className="font-display font-bold">Aucun plan de coffrage AutoCAD sur cette étude</div>
          <div className="text-secondaire">Déposez le plan de coffrage au format DWG ou DXF dans la pièce n° 1, puis revenez ici.</div>
        </div>
      )}

      {fichiers && fichiers.length > 1 && (
        <Select className="mb-4 w-auto" value={fichierId} aria-label="Plan de coffrage"
          onChange={(e) => { setFichierId(e.target.value); setCorrections({}); setRepere(null); setA(null); }}>
          {fichiers.map((f) => <option key={f.id} value={f.id}>{f.nom} ({formatTaille(f.taille)})</option>)}
        </Select>
      )}

      {erreur && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}
      {fichierId && !a && !erreur && (
        <>
          <p className="mb-3 text-[13px] text-secondaire">Lecture du plan de coffrage. Un grand plan demande une dizaine de secondes.</p>
          <AttenteFiche className="border-0 p-0" />
        </>
      )}

      {a && (
        <div className={cn("grid gap-6 lg:grid-cols-[minmax(0,5fr)_minmax(0,6fr)]", travail && "opacity-60")}>
          <div className="space-y-6">
            <section>
              <h3 className="text-[15px] font-bold">1. Niveau</h3>
              {a.niveaux.length === 0 ? (
                <p className="mt-2 rounded-lg bg-orange-fond px-4 py-3 text-[13px] text-orange-texte">
                  Aucun niveau n&apos;a été trouvé. Un niveau se reconnaît à un contour de dalle qui porte au moins trois poteaux ou voiles :
                  donnez le rôle « Dalle » au calque du contour et « Poteaux » au calque des poteaux, dans la liste ci-dessous.
                </p>
              ) : (
                <div className="mt-2 space-y-2">
                  {a.niveaux.map((n) => (
                    <button key={`${n.x}-${n.y}`} type="button" aria-pressed={n.choisi} onClick={() => setRepere({ x: n.x, y: n.y })}
                      className={cn("flex w-full cursor-pointer items-center justify-between gap-3 rounded-lg border px-4 py-2.5 text-left",
                        n.choisi ? "border-marine bg-marine/5" : "border-bordure-forte bg-white hover:bg-fond")}>
                      <span className="font-semibold">{n.nom}</span>
                      <span className="text-[13px] text-secondaire">
                        <span className="font-display">{formatSurface(n.surface)}</span>, {n.poteaux} poteau{n.poteaux > 1 ? "x" : ""}, {n.voiles} voile{n.voiles > 1 ? "s" : ""}, {n.reservations} réservation{n.reservations > 1 ? "s" : ""}
                      </span>
                    </button>
                  ))}
                </div>
              )}
            </section>

            <section>
              <h3 className="text-[15px] font-bold">2. Rôle des calques</h3>
              <p className="mt-1 text-[13px] text-secondaire">
                Le rôle est proposé d&apos;après le nom du calque. Corrigez-le si un élément manque ou est mal classé : l&apos;aperçu se met à jour.
              </p>
              <div className="mt-3 max-h-[46vh] overflow-auto rounded-lg border border-bordure">
                <table className="w-full text-left text-[13px]">
                  <thead className="sticky top-0 border-b border-bordure bg-fond text-[12px] text-secondaire">
                    <tr><th className="px-3 py-2 font-semibold">Calque</th><th className="px-3 py-2 text-right font-semibold">{choisi ? "Formes dans le niveau" : "Formes"}</th><th className="px-3 py-2 font-semibold">Rôle</th></tr>
                  </thead>
                  <tbody>
                    {calques.map((c) => (
                      <tr key={c.nom} className="border-b border-bordure last:border-0">
                        <td className="px-3 py-1.5">
                          <span className={cn(c.role !== "" && "font-semibold")}>{c.nom}</span>
                          {c.role !== c.propose && <span className="ml-2 text-[12px] text-orange-texte">corrigé</span>}
                        </td>
                        <td className="px-3 py-1.5 text-right font-display text-secondaire">{choisi ? c.dansNiveau : c.formes}</td>
                        <td className="px-3 py-1.5">
                          <Select className="h-8 w-36 text-[13px]" value={c.role} aria-label={`Rôle du calque ${c.nom}`}
                            onChange={(e) => setCorrections((r) => ({ ...r, [c.nom]: e.target.value }))}>
                            {a.roles.map((r) => <option key={r.code} value={r.code}>{r.label}</option>)}
                          </Select>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {(caches > 0 || tous) && (
                <button type="button" onClick={() => setTous(!tous)} className="mt-2 cursor-pointer text-[13px] font-semibold text-marine hover:underline">
                  {tous ? "Masquer les calques sans forme dans le niveau" : `Afficher les ${caches} autres calques`}
                </button>
              )}
            </section>
          </div>

          <section>
            <h3 className="text-[15px] font-bold">3. Aperçu{choisi ? ` : ${choisi.nom}` : ""}</h3>
            {a.apercu ? <Dessin a={a.apercu} /> : (
              <div className="mt-2 rounded-lg border border-dashed border-bordure-forte px-6 py-16 text-center text-secondaire">
                {a.niveaux.length > 1 ? "Choisissez un niveau pour voir son fond de plan." : "L'aperçu apparaît dès qu'un niveau est trouvé."}
              </div>
            )}
          </section>
        </div>
      )}
    </Sheet>
  );
}

const chemin = (p: Points) => p.map(([x, y], i) => `${i ? "L" : "M"}${x} ${y}`).join(" ") + " Z";

/** Dessin du fond de plan, en mètres, l'axe vertical vers le haut comme dans AutoCAD. */
function Dessin({ a }: { a: Apercu }) {
  const marge = Math.max(a.largeur, a.hauteur) * 0.03;
  const legende: [string, string, number][] = [
    ["Dalle", "border-2 border-texte bg-white", 1],
    ["Poteaux", "bg-rouge-vif", a.poteaux.length + a.ronds.length],
    ["Voiles", "bg-marine", a.voiles.length],
    ["Réservations", "border border-orange-texte bg-orange-fond", a.reservations.length],
  ];
  return (
    <div className="mt-2">
      <div className="rounded-lg border border-bordure bg-white p-3">
        <svg viewBox={`${-marge} ${-marge} ${a.largeur + 2 * marge} ${a.hauteur + 2 * marge}`} role="img"
          aria-label={`Fond de plan : dalle de ${formatSurface(a.surface)}, ${a.poteaux.length + a.ronds.length} poteaux, ${a.voiles.length} voiles, ${a.reservations.length} réservations`}
          className="max-h-[58vh] w-full">
          <g transform={`translate(0 ${a.hauteur}) scale(1 -1)`}>
            <path d={chemin(a.dalle)} fill="var(--color-fond)" stroke="var(--color-texte)" strokeWidth="1.6" vectorEffect="non-scaling-stroke" />
            {a.reservations.map((p, i) => <path key={`r${i}`} d={chemin(p)} fill="var(--color-orange-fond)" stroke="var(--color-orange-texte)" strokeWidth="1" vectorEffect="non-scaling-stroke" />)}
            {a.voiles.map((p, i) => <path key={`v${i}`} d={chemin(p)} fill="var(--color-marine)" fillOpacity="0.85" stroke="var(--color-marine)" strokeWidth="0.6" vectorEffect="non-scaling-stroke" />)}
            {a.poteaux.map((p, i) => <path key={`p${i}`} d={chemin(p)} fill="var(--color-rouge-vif)" />)}
            {a.ronds.map((c, i) => <circle key={`c${i}`} cx={c.x} cy={c.y} r={c.r} fill="var(--color-rouge-vif)" />)}
          </g>
        </svg>
      </div>
      <ul className="mt-3 flex flex-wrap gap-x-5 gap-y-1.5 text-[13px]">
        {legende.map(([label, classe, nb]) => (
          <li key={label} className="flex items-center gap-2">
            <span aria-hidden className={cn("h-3 w-3 shrink-0 rounded-sm", classe)} />
            {label}
            <span className="font-display font-bold">{label === "Dalle" ? formatSurface(a.surface) : nb}</span>
          </li>
        ))}
        <li className="text-secondaire">{a.largeur.toLocaleString("fr-FR", { maximumFractionDigits: 2 })} m par {a.hauteur.toLocaleString("fr-FR", { maximumFractionDigits: 2 })} m</li>
      </ul>
      <p className="mt-2 text-[12px] text-attenue">
        Le fichier déposé est un DXF en mètres, aux calques ADAPT-DALLE, ADAPT-POTEAUX, ADAPT-VOILE et ADAPT-RESERVATION. Les voiles et les réservations sont à contrôler dans AutoCAD avant l&apos;import dans ADAPT.
      </p>
    </div>
  );
}

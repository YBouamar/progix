"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { cn, formatDH, formatNombre } from "@/lib/utils";
import { BarresHorizontales, Colonnes, COULEUR_UNIQUE, COULEURS_SERIES } from "@/components/graphes";
import { Select } from "@/components/ui/input";

type Ligne = { id: string; nom: string; mois: number[]; total: number; surface?: number; enCours?: number };
type ParAn = { annee: number; total: number; surface: number };
type Vue = { annee: number; annees: number[]; moisCourant: number | null; dessinateurs: Ligne[] | null; ingenieurs: Ligne[] | null; dessinateursParAn: ParAn[] | null; ingenieursParAn: ParAn[] | null };
type Paiements = {
  annee: number; modes: { mode: string; label: string; encaisse: number; nombre: number; enAttente: number; rembourse: number; annules: number }[];
  mois: { mois: number; parMode: Record<string, number> }[];
};

const MOIS = ["Janv.", "Févr.", "Mars", "Avr.", "Mai", "Juin", "Juil.", "Août", "Sept.", "Oct.", "Nov.", "Déc."];

/** Statistiques de performance : dalles finies par dessinateur, projets suivis par ingénieur, par mois et sur l'année. */
export default function Performance() {
  const [annee, setAnnee] = useState<number | null>(null);
  const [v, setV] = useState<Vue | null>(null);
  const [erreur, setErreur] = useState("");
  const [paiements, setPaiements] = useState<Paiements | null>(null);

  useEffect(() => {
    api<Vue>(`/statistiques/performance${annee ? `?annee=${annee}` : ""}`).then(setV)
      .catch((e) => setErreur(e instanceof ApiError && e.status === 403 ? "Votre rôle ne donne pas accès à ces statistiques." : "Chargement impossible."));
  }, [annee]);
  // L'état des paiements ne s'affiche qu'aux rôles qui voient les chiffres financiers (refus de l'API sinon).
  useEffect(() => {
    if (!v) return;
    api<Paiements>(`/statistiques/paiements?annee=${v.annee}`).then(setPaiements).catch(() => setPaiements(null));
  }, [v]);

  if (erreur) return <p className="text-rouge-texte">{erreur}</p>;
  if (!v) return <p className="text-secondaire">Chargement...</p>;

  return (
    <div>
      <div className="flex flex-wrap items-center gap-3">
        <Select className="w-40" aria-label="Année" value={v.annee} onChange={(e) => setAnnee(Number(e.target.value))}>
          {v.annees.map((a) => <option key={a} value={a}>Année {a}</option>)}
        </Select>
        <span className="text-[13px] text-secondaire">Les projets supprimés ne sont pas comptés.</span>
      </div>

      <Graphes v={v} paiements={paiements} />

      {v.dessinateurs && (
        <Tableau titre="Dessinateurs : dalles finies" aide="Une dalle est finie quand l'étape « Plans d'exécution » est validée. Elle compte pour le dessinateur affecté à la dalle, au mois de cette validation."
          lignes={v.dessinateurs} moisCourant={v.moisCourant} colonneTotal={`Total ${v.annee}`}
          extra={[{ label: "Surface étudiée", rendre: (l) => `${formatNombre(l.surface ?? 0)} m²`, somme: sommeSurface }]} sommeAnnuelle />
      )}
      {v.ingenieurs && (
        <Tableau titre="Ingénieurs : projets suivis" aide="Un projet compte pour un ingénieur chaque mois où il y est affecté. Le total de l'année compte chaque projet une seule fois. La surface suivie est celle des dalles des mêmes projets."
          lignes={v.ingenieurs} moisCourant={v.moisCourant} colonneTotal={`Projets ${v.annee}`}
          extra={[
            { label: "En cours", rendre: (l) => String(l.enCours ?? 0) },
            { label: "Surface suivie", rendre: (l) => `${formatNombre(l.surface ?? 0)} m²`, somme: sommeSurface },
          ]} sommeAnnuelle />
      )}
    </div>
  );
}

const entier = (n: number) => n.toLocaleString("fr-FR", { maximumFractionDigits: 0 });
const MOIS_LONGS = ["Janvier", "Février", "Mars", "Avril", "Mai", "Juin", "Juillet", "Août", "Septembre", "Octobre", "Novembre", "Décembre"];

function Carte({ titre, aide, children, large }: { titre: string; aide?: string; children: React.ReactNode; large?: boolean }) {
  return (
    <section className={cn("rounded-xl border border-bordure bg-white px-5 py-4", large && "lg:col-span-2")}>
      <h3 className="text-[14px] font-bold">{titre}</h3>
      {aide && <p className="mb-3 mt-0.5 text-[12px] text-secondaire">{aide}</p>}
      {children}
    </section>
  );
}

/** Graphes de la page : production par personne, par mois, par an, et état des paiements par mode. */
function Graphes({ v, paiements }: { v: Vue; paiements: Paiements | null }) {
  const mois = (l: Ligne[]) => MOIS.map((_, i) => l.reduce((s, x) => s + x.mois[i], 0));
  const surligne = v.moisCourant ? v.moisCourant - 1 : null;
  const libelleMois = (i: number) => `${MOIS_LONGS[i]} ${v.annee}`;
  const parAn = (l: ParAn[]) => [...l].sort((a, b) => a.annee - b.annee);
  return (
    <div className="mt-5 grid gap-4 lg:grid-cols-2">
      {v.dessinateurs && (
        <>
          <Carte titre={`Dalles finies par dessinateur, ${v.annee}`} aide="Plans d'exécution validés dans l'année ; la surface étudiée est dans l'infobulle.">
            <BarresHorizontales unite="Dalles finies" format={entier} vide="Aucun dessinateur."
              donnees={v.dessinateurs.map((l) => ({ id: l.id, label: l.nom, valeur: l.total, detail: `${formatNombre(l.surface ?? 0)} m² étudiés` }))} />
          </Carte>
          <Carte titre={`Dalles finies par mois, ${v.annee}`} aide="Tous dessinateurs confondus.">
            <Colonnes categories={MOIS} series={[{ id: "dalles", label: "Dalles finies", couleur: COULEUR_UNIQUE, valeurs: mois(v.dessinateurs) }]} format={entier} surligne={surligne} detailCategorie={libelleMois} />
          </Carte>
        </>
      )}
      {v.ingenieurs && (
        <>
          <Carte titre={`Surface suivie par ingénieur, ${v.annee}`} aide="Surface des dalles des projets suivis dans l'année ; le nombre de projets est dans l'infobulle.">
            <BarresHorizontales unite="Surface suivie" format={(n) => `${entier(n)} m²`} vide="Aucun ingénieur."
              donnees={[...v.ingenieurs].sort((a, b) => (b.surface ?? 0) - (a.surface ?? 0)).map((l) => ({ id: l.id, label: l.nom, valeur: l.surface ?? 0, detail: `${l.total} projet(s), ${l.enCours ?? 0} en cours` }))} />
          </Carte>
          <Carte titre={`Projets suivis par mois, ${v.annee}`} aide="Cumul des ingénieurs : un projet suivi par deux ingénieurs compte deux fois.">
            <Colonnes categories={MOIS} series={[{ id: "projets", label: "Projets suivis", couleur: COULEUR_UNIQUE, valeurs: mois(v.ingenieurs) }]} format={entier} surligne={surligne} detailCategorie={libelleMois} />
          </Carte>
        </>
      )}
      {v.dessinateursParAn && v.dessinateursParAn.length > 0 && (
        <Carte titre="Dalles finies par an" aide="Année sélectionnée mise en évidence ; surface étudiée dans l'infobulle.">
          <Colonnes categories={parAn(v.dessinateursParAn).map((a) => String(a.annee))} format={entier} surligne={parAn(v.dessinateursParAn).findIndex((a) => a.annee === v.annee)}
            series={[{ id: "an", label: "Dalles finies", couleur: COULEUR_UNIQUE, valeurs: parAn(v.dessinateursParAn).map((a) => a.total) }]}
            detailCategorie={(i) => `${parAn(v.dessinateursParAn!)[i].annee} · ${formatNombre(parAn(v.dessinateursParAn!)[i].surface)} m²`} />
        </Carte>
      )}
      {v.ingenieursParAn && v.ingenieursParAn.length > 0 && (
        <Carte titre="Projets suivis par an" aide="Chaque projet compté une fois par an ; surface suivie dans l'infobulle.">
          <Colonnes categories={parAn(v.ingenieursParAn).map((a) => String(a.annee))} format={entier} surligne={parAn(v.ingenieursParAn).findIndex((a) => a.annee === v.annee)}
            series={[{ id: "an", label: "Projets suivis", couleur: COULEUR_UNIQUE, valeurs: parAn(v.ingenieursParAn).map((a) => a.total) }]}
            detailCategorie={(i) => `${parAn(v.ingenieursParAn!)[i].annee} · ${formatNombre(parAn(v.ingenieursParAn!)[i].surface)} m²`} />
        </Carte>
      )}
      {paiements && <EtatPaiements p={paiements} annee={v.annee} surligne={surligne} libelleMois={libelleMois} />}
    </div>
  );
}

/** État des paiements de l'année par mode, et encaissé par mois et par mode. */
function EtatPaiements({ p, annee, surligne, libelleMois }: { p: Paiements; annee: number; surligne: number | null; libelleMois: (i: number) => string }) {
  const couleur = (mode: string) => COULEURS_SERIES[Math.max(0, p.modes.findIndex((m) => m.mode === mode))];
  const total = p.modes.reduce((s, m) => s + m.encaisse, 0);
  return (
    <>
      <Carte titre={`État des paiements par mode, ${annee}`} aide="Encaissé dans l'année (un effet compte à son échéance), remboursements à part.">
        <BarresHorizontales unite="Encaissé" format={formatDH}
          donnees={p.modes.map((m) => ({ id: m.mode, label: m.label, valeur: m.encaisse, couleur: couleur(m.mode), detail: `${m.nombre} règlement(s)${total > 0 ? `, ${Math.round((m.encaisse / total) * 100)} % du total` : ""}` }))} />
        <table className="mt-3 w-full text-[12px]">
          <thead className="text-secondaire">
            <tr><th className="py-1 text-left font-semibold">Mode</th><th className="py-1 text-right font-semibold">Règlements</th><th className="py-1 text-right font-semibold">Effets en attente</th><th className="py-1 text-right font-semibold">Remboursé</th><th className="py-1 text-right font-semibold">Annulés</th></tr>
          </thead>
          <tbody>
            {p.modes.map((m) => (
              <tr key={m.mode} className="border-t border-bordure">
                <td className="py-1"><span className="mr-1.5 inline-block size-2 rounded-sm align-middle" style={{ background: couleur(m.mode) }} />{m.label}</td>
                <td className="py-1 text-right font-display">{m.nombre}</td>
                <td className={cn("py-1 text-right font-display", m.enAttente > 0 && "text-orange-texte")}>{m.enAttente > 0 ? formatDH(m.enAttente) : "-"}</td>
                <td className="py-1 text-right font-display">{m.rembourse > 0 ? formatDH(m.rembourse) : "-"}</td>
                <td className="py-1 text-right font-display">{m.annules || "-"}</td>
              </tr>
            ))}
            <tr className="border-t border-bordure font-semibold"><td className="py-1">Total encaissé</td><td colSpan={4} className="py-1 text-right font-display">{formatDH(total)}</td></tr>
          </tbody>
        </table>
      </Carte>
      <Carte titre={`Encaissé par mois et par mode, ${annee}`} aide="Remboursements non déduits ; détail exact par la vue tableau.">
        <Colonnes categories={MOIS} format={formatDH} surligne={surligne} detailCategorie={libelleMois} tableau
          series={p.modes.map((m) => ({ id: m.mode, label: m.label, couleur: couleur(m.mode), valeurs: p.mois.map((x) => x.parMode[m.mode] ?? 0) }))} />
      </Carte>
    </>
  );
}

const sommeSurface = (lignes: Ligne[]) => `${formatNombre(lignes.reduce((s, l) => s + (l.surface ?? 0), 0))} m²`;

type Extra = { label: string; rendre: (l: Ligne) => string; somme?: (lignes: Ligne[]) => string };

function Tableau({ titre, aide, lignes, moisCourant, colonneTotal, extra, sommeAnnuelle }: {
  titre: string; aide: string; lignes: Ligne[]; moisCourant: number | null; colonneTotal: string;
  extra: Extra[]; sommeAnnuelle?: boolean;
}) {
  const max = Math.max(1, ...lignes.flatMap((l) => l.mois));
  return (
    <div className="mt-5 rounded-xl border border-bordure bg-white">
      <div className="px-6 py-4">
        <h3 className="text-[15px] font-bold">{titre}</h3>
        <p className="mt-0.5 text-[13px] text-secondaire">{aide}</p>
      </div>
      <div className="overflow-x-auto border-t border-bordure">
        <table className="w-full text-left text-[13px]">
          <thead className="bg-fond text-[11px] uppercase tracking-wide text-secondaire">
            <tr>
              <th className="px-6 py-2.5 font-semibold">Nom</th>
              {MOIS.map((m, i) => <th key={m} className={cn("px-2 py-2.5 text-right font-semibold", moisCourant === i + 1 && "text-marine")}>{m}</th>)}
              <th className="border-l border-bordure px-3 py-2.5 text-right font-semibold">{colonneTotal}</th>
              {extra.map((ex) => <th key={ex.label} className="px-6 py-2.5 text-right font-semibold">{ex.label}</th>)}
            </tr>
          </thead>
          <tbody>
            {lignes.length === 0 && <tr><td colSpan={14 + extra.length} className="px-6 py-6 text-center text-secondaire">Aucune personne à afficher.</td></tr>}
            {lignes.map((l) => (
              <tr key={l.id} className="border-t border-bordure">
                <td className="whitespace-nowrap px-6 py-2.5 font-semibold">{l.nom}</td>
                {l.mois.map((n, i) => (
                  <td key={i} className="px-2 py-2.5 text-right">
                    <span className={cn("inline-block min-w-7 rounded-md px-1.5 py-0.5 text-center font-display", n === 0 ? "text-attenue" : "font-bold text-marine")}
                      style={n > 0 ? { backgroundColor: `rgba(27, 42, 107, ${0.06 + 0.16 * (n / max)})` } : undefined}>{n}</span>
                  </td>
                ))}
                <td className="border-l border-bordure px-3 py-2.5 text-right font-display font-bold">{l.total}</td>
                {extra.map((ex) => <td key={ex.label} className="whitespace-nowrap px-6 py-2.5 text-right font-display">{ex.rendre(l)}</td>)}
              </tr>
            ))}
          </tbody>
          {sommeAnnuelle && lignes.length > 0 && (
            <tfoot className="bg-fond font-semibold">
              <tr className="border-t border-bordure">
                <td className="px-6 py-2.5">Total</td>
                {MOIS.map((_, i) => <td key={i} className="px-2 py-2.5 text-right font-display">{lignes.reduce((s, l) => s + l.mois[i], 0)}</td>)}
                <td className="border-l border-bordure px-3 py-2.5 text-right font-display font-bold">{lignes.reduce((s, l) => s + l.total, 0)}</td>
                {extra.map((ex) => <td key={ex.label} className="whitespace-nowrap px-6 py-2.5 text-right font-display">{ex.somme?.(lignes) ?? ""}</td>)}
              </tr>
            </tfoot>
          )}
        </table>
      </div>
    </div>
  );
}

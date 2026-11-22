"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { api } from "@/lib/api";
import { cn, formatDH, formatNombre } from "@/lib/utils";
import { Field, Select } from "@/components/ui/input";
import { periodes, type NegoceTdb } from "./donnees";

/** Carte d'indicateur : libellé, valeur, évolution éventuelle et règle de calcul. */
export function Carte({ label, valeur, evolution, aide, ton, href }: { label: string; valeur: string; evolution?: { avant: number; v: number; hausseBonne: boolean } | null; aide?: string; ton?: "rouge" | "orange"; href?: string }) {
  const delta = evolution && evolution.avant > 0 ? (evolution.v - evolution.avant) / evolution.avant * 100 : null;
  const bon = delta === null || delta === 0 ? null : (delta > 0) === evolution!.hausseBonne;
  const corps = (
    <div className="h-full rounded-xl border border-bordure bg-white p-5">
      <div className="flex items-start justify-between gap-2">
        <div className="text-[13px] text-secondaire">{label}</div>
        {delta !== null && <span className={cn("font-display text-[13px] font-bold", bon === null ? "text-secondaire" : bon ? "text-vert-texte" : "text-orange-texte")}>{delta > 0 ? "+" : ""}{delta.toLocaleString("fr-FR", { maximumFractionDigits: 1 })} %</span>}
      </div>
      <div className={cn("mt-1 font-display text-xl font-bold", ton === "rouge" && "text-rouge-texte", ton === "orange" && "text-orange-texte")}>{valeur}</div>
      {aide && <div className="mt-1 text-[12px] text-attenue">{aide}</div>}
    </div>
  );
  return href ? <Link href={href} className="block transition-opacity hover:opacity-80">{corps}</Link> : corps;
}

function Classement({ titre, lignes, vide }: { titre: string; lignes: { nom: string; valeur: number }[]; vide: string }) {
  const max = Math.max(1, ...lignes.map((l) => l.valeur));
  return (
    <div className="rounded-xl border border-bordure bg-white p-5">
      <div className="font-display font-bold">{titre}</div>
      {lignes.length === 0 ? <p className="mt-3 text-[13px] text-secondaire">{vide}</p> : (
        <div className="mt-4 space-y-3">
          {lignes.map((l) => (
            <div key={l.nom} className="flex items-center gap-3 text-[13px]">
              <span className="w-40 shrink-0 truncate" title={l.nom}>{l.nom}</span>
              <span className="h-2.5 flex-1 overflow-hidden rounded-full bg-fond"><span className="block h-full rounded-full bg-marine" style={{ width: `${l.valeur / max * 100}%` }} /></span>
              <span className="w-28 shrink-0 text-right font-display font-bold">{formatDH(l.valeur)}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

/**
 * Indicateurs des ventes directes, des achats et du stock. Le serveur ne renvoie que les blocs que la personne a le droit de lire :
 * l'écran affiche ce qui arrive, sans rien déduire du rôle. Sans période fournie, il propose son propre sélecteur.
 */
export function KpiNegoce({ debut, fin, entite, titre = "Ventes directes, achats et stock" }: { debut?: string; fin?: string; entite?: string; titre?: string }) {
  const liste = useMemo(periodes, []);
  const [choix, setChoix] = useState("mois");
  const propre = !debut || !fin;
  const x = liste.find((l) => l.id === choix) ?? liste[0];
  const du = propre ? x.debut : debut, au = propre ? x.fin : fin;
  const [d, setD] = useState<NegoceTdb | null>(null);
  const [erreur, setErreur] = useState(false);

  useEffect(() => {
    if (!du || !au || au < du) return;
    const q = new URLSearchParams({ debut: du, fin: au }); if (entite) q.set("entite", entite);
    api<NegoceTdb>(`/negoce/tableau-de-bord?${q}`).then((r) => { setD(r); setErreur(false); }).catch(() => { setD(null); setErreur(true); });
  }, [du, au, entite]);

  if (erreur || !d) return erreur ? null : <div className="mb-5 h-24 animate-pulse rounded-xl bg-fond" />;
  const { ventes: v, pipeline: p, achats: a, stock: s } = d;
  if (!v && !p && !a && !s) return null;
  const cur = v?.periode, prev = v?.precedente;

  return (
    <section className="mb-5">
      <div className="mb-3 flex flex-wrap items-end justify-between gap-3">
        <h2 className="font-display text-[15px] font-bold">{titre}</h2>
        {propre && (
          <Field label="Période"><Select className="w-auto capitalize" value={choix} onChange={(e) => setChoix(e.target.value)}>{liste.map((l) => <option key={l.id} value={l.id}>{l.label}</option>)}</Select></Field>
        )}
      </div>

      {v && cur && prev && (
        <div className="mb-4 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <Carte label="Chiffre d'affaires, ventes directes" valeur={formatDH(cur.chiffreAffaires)} evolution={{ v: cur.chiffreAffaires, avant: prev.chiffreAffaires, hausseBonne: true }} aide={`${v.nbFactures} facture(s), TTC après avoirs`} />
          <Carte label="Encaissé, ventes directes" valeur={formatDH(cur.encaisse)} evolution={{ v: cur.encaisse, avant: prev.encaisse, hausseBonne: true }} aide="Paiements reçus sur la période" />
          {cur.margeBrute !== null
            ? <Carte label="Marge brute" valeur={formatDH(cur.margeBrute)} evolution={prev.margeBrute !== null ? { v: cur.margeBrute, avant: prev.margeBrute, hausseBonne: true } : null} aide="Prix de vente moins coût moyen, HT, sur les livraisons" ton={cur.margeBrute < 0 ? "rouge" : undefined} />
            : <Carte label="Créances clients" valeur={formatDH(v.creances.total)} aide={`${v.creances.nb} facture(s) non soldée(s)`} href="/ventes-directes/factures" />}
          <Carte label="Impayés en retard" valeur={formatDH(v.retards.total)} aide={`${v.retards.nb} facture(s) échue(s), sur ${formatDH(v.creances.total)} de créances`} ton={v.retards.nb > 0 ? "rouge" : undefined} href="/ventes-directes/factures" />
        </div>
      )}

      {p && (
        <div className="mb-4 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <Carte label="Devis ouverts" valeur={`${p.devisOuverts.nb} devis`} aide={`${formatDH(p.devisOuverts.total)} TTC${p.devisOuverts.expires > 0 ? `, dont ${p.devisOuverts.expires} expiré(s)` : ""}`} ton={p.devisOuverts.expires > 0 ? "orange" : undefined} href="/ventes-directes/devis" />
          <Carte label="Taux de conversion des devis" valeur={p.tauxConversion === null ? "-" : `${p.tauxConversion.toLocaleString("fr-FR", { maximumFractionDigits: 1 })} %`} aide={`Devis de la période devenus commande, sur ${p.devisPeriode} devis`} />
          <Carte label="Commandes à livrer" valeur={`${p.aLivrer} sur ${p.commandesOuvertes}`} aide="Commandes ouvertes avec une quantité restant à livrer" href="/ventes-directes/livraisons" />
          <Carte label="Livré, à facturer" valeur={formatDH(p.aFacturer.montantHt)} aide={`${p.aFacturer.nb} commande(s), HT`} ton={p.aFacturer.nb > 0 ? "orange" : undefined} href="/ventes-directes/factures" />
        </div>
      )}

      {a && (
        <div className="mb-4 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <Carte label="Achats commandés" valeur={formatDH(a.commande)} evolution={{ v: a.commande, avant: a.commandePrecedente, hausseBonne: false }} aide="Commandes d'achat de la période, TTC" href="/achats-stock/commandes" />
          <Carte label="Commandes d'achat en cours" valeur={`${a.enCours.nb} commande(s)`} aide={`${formatDH(a.enCours.total)} TTC, dont ${a.enCours.partielles} partiellement reçue(s)`} href="/achats-stock/commandes" />
          {a.resteAPayer && <Carte label="Reste à payer aux fournisseurs" valeur={formatDH(a.resteAPayer.total)} aide={`${a.resteAPayer.nb} commande(s) non soldée(s)`} ton={a.resteAPayer.nb > 0 ? "orange" : undefined} href="/achats-stock/commandes" />}
        </div>
      )}

      {s && (
        <div className="mb-4 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <Carte label="Articles en stock" valeur={`${s.articles} article(s)`} aide={`${s.mouvements.entrees} entrée(s) et ${s.mouvements.sorties} sortie(s) sur la période`} href="/achats-stock/stock" />
          <Carte label="Sous le seuil d'alerte" valeur={`${s.sousSeuil} article(s)`} aide={s.ruptures > 0 ? `Dont ${s.ruptures} en rupture` : "Aucune rupture"} ton={s.sousSeuil > 0 ? "rouge" : undefined} href="/achats-stock/stock" />
          {s.valeur !== null && <Carte label="Valeur du stock" valeur={formatDH(s.valeur)} aide="Au coût moyen pondéré" href="/achats-stock/stock" />}
        </div>
      )}

      <div className="grid gap-4 xl:grid-cols-2">
        {v && <Classement titre="Meilleurs clients de la période" vide="Aucune facture sur la période." lignes={v.topClients.map((c) => ({ nom: c.client, valeur: c.montant }))} />}
        {v && <Classement titre="Articles les plus vendus (HT)" vide="Aucune vente sur la période." lignes={v.topArticles.map((c) => ({ nom: c.article, valeur: c.montantHt }))} />}
        {a && <Classement titre="Principaux fournisseurs de la période" vide="Aucun achat sur la période." lignes={a.topFournisseurs.map((c) => ({ nom: c.fournisseur, valeur: c.montant }))} />}
        {s && s.aSurveiller.length > 0 && (
          <div className="overflow-x-auto rounded-xl border border-bordure bg-white">
            <div className="flex items-center justify-between gap-3 border-b border-bordure px-5 py-3">
              <div className="font-display font-bold">Stock à surveiller</div><Link href="/achats-stock/stock" className="text-[13px] font-semibold text-marine hover:underline">Ouvrir le stock</Link>
            </div>
            <table className="w-full text-left text-[13px]">
              <thead className="border-b border-bordure bg-fond text-[12px] text-secondaire"><tr><th className="px-5 py-2.5 font-semibold">Article</th><th className="px-5 py-2.5 text-right font-semibold">Stock</th><th className="px-5 py-2.5 text-right font-semibold">Seuil</th></tr></thead>
              <tbody>
                {s.aSurveiller.map((x) => (
                  <tr key={x.id} className="border-b border-bordure last:border-0">
                    <td className="px-5 py-2.5 font-semibold">{x.designation}</td>
                    <td className={cn("px-5 py-2.5 text-right font-display font-bold", x.stock <= 0 && "text-rouge-texte")}>{formatNombre(x.stock)} {x.unite}</td>
                    <td className="px-5 py-2.5 text-right font-display">{formatNombre(x.seuil)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </section>
  );
}

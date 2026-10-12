"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { api, ApiError } from "@/lib/api";
import { cn, formatDate, lireNombre } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field, Input, Select } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";

type Commande = {
  dalleId: string; projetId: string; reference: string; projet: string; entite: string; demandee: string;
  contenu: string; validations: string; etat: string; etatLabel: string;
};
type Commandes = { compteurs: { etat: string; label: string; nb: number }[]; commandes: Commande[] };
type Article = { code: string; article: string; stockInitial: number; mouvements: number; consomme: number; restant: number; seuil: number; bas: boolean };
type Stock = { entite: string; articles: Article[]; mouvements: { date: string; article: string; sens: string; quantite: number; motif: string; par: string }[] };

const ETATS: [string, string][] = [["validee", "Validée"], ["fabrication", "En fabrication"], ["livree", "Livrée"], ["recue", "Reçue"]];
const n0 = (n: number) => n.toLocaleString("fr-FR", { maximumFractionDigits: 2 }).replace(/[  ]/g, " ");
const signe = (n: number) => `${n > 0 ? "+ " : n < 0 ? "− " : ""}${n0(Math.abs(n))}`;

export default function LogistiquePage() {
  const [onglet, setOnglet] = useState<"commandes" | "stock" | "synthese">("commandes");
  return (
    <div>
      <div className="mb-5 flex gap-6 border-b border-bordure">
        {([["commandes", "Commandes usine"], ["stock", "État de stock"], ["synthese", "Synthèse annuelle"]] as const).map(([id, label]) => (
          <button key={id} role="tab" aria-selected={onglet === id} onClick={() => setOnglet(id)}
            className={cn("-mb-px cursor-pointer border-b-2 pb-2.5 text-[14px] font-semibold", onglet === id ? "border-marine text-marine" : "border-transparent text-secondaire hover:text-texte")}>
            {label}
          </button>
        ))}
      </div>
      {onglet === "commandes" ? <CommandesUsine /> : onglet === "stock" ? <EtatStock /> : <SyntheseAnnuelle />}
    </div>
  );
}

function CommandesUsine() {
  const router = useRouter();
  const [etat, setEtat] = useState("");
  const [data, setData] = useState<Commandes | null>(null);
  const [erreur, setErreur] = useState("");

  const charger = useCallback(() => {
    api<Commandes>(`/logistique/commandes${etat ? `?etat=${etat}` : ""}`).then((r) => { setData(r); setErreur(""); })
      .catch((e) => setErreur(e instanceof ApiError ? e.message : "Chargement impossible."));
  }, [etat]);
  useEffect(() => { charger(); }, [charger]);

  async function changer(c: Commande, etat: string) {
    try {
      await api(`/logistique/commandes/${c.dalleId}/etat`, { method: "POST", body: JSON.stringify({ etat }) });
      charger();
    } catch (e) {
      setErreur(e instanceof ApiError ? e.message : "Action impossible.");
    }
  }

  const compteurs = data?.compteurs ?? [];
  const onglets = [{ etat: "", label: "Toutes", nb: compteurs.reduce((s, c) => s + c.nb, 0) }, ...compteurs];

  return (
    <div>
      <div className="mb-4 flex flex-wrap gap-2">
        {onglets.map((o) => (
          <button key={o.etat} aria-pressed={etat === o.etat} onClick={() => setEtat(o.etat)}
            className={cn("flex cursor-pointer items-center gap-2 rounded-full border px-3.5 py-1.5 text-[13px] font-semibold",
              etat === o.etat ? "border-marine bg-marine text-white" : "border-bordure-forte bg-white hover:bg-fond")}>
            {o.label}<span className={cn("font-display text-[12px]", etat === o.etat ? "text-white/80" : "text-attenue")}>{o.nb}</span>
          </button>
        ))}
      </div>
      {erreur && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}
      <div className="overflow-x-auto rounded-xl border border-bordure bg-white">
        <table className="w-full text-left">
          <thead className="border-b border-bordure bg-fond text-[12px] uppercase tracking-wide text-secondaire">
            <tr>
              <th className="px-5 py-3 font-semibold">Dalle</th>
              <th className="px-5 py-3 font-semibold">Projet</th>
              <th className="px-5 py-3 font-semibold">Entité</th>
              <th className="px-5 py-3 font-semibold">Demandée</th>
              <th className="px-5 py-3 font-semibold">Contenu</th>
              <th className="px-5 py-3 font-semibold">Validations</th>
              <th className="px-5 py-3 font-semibold">État</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {data?.commandes.map((c) => (
              <tr key={c.dalleId} tabIndex={0} role="link" aria-label={`Détail de la commande ${c.reference}`}
                onClick={() => router.push(`/logistique/${c.dalleId}`)}
                onKeyDown={(e) => { if (e.key === "Enter") router.push(`/logistique/${c.dalleId}`); }}
                className="cursor-pointer border-b border-bordure last:border-0 hover:bg-fond focus:outline-none focus:bg-fond">
                <td className="whitespace-nowrap px-5 py-3" onClick={(e) => e.stopPropagation()}>
                  <Link href={`/projets/${c.projetId}`} className="font-display font-bold text-marine hover:underline">{c.reference}</Link>
                </td>
                <td className="px-5 py-3">{c.projet}</td>
                <td className="px-5 py-3 font-display">{c.entite}</td>
                <td className="whitespace-nowrap px-5 py-3 font-display text-secondaire">{formatDate(c.demandee)}</td>
                <td className="px-5 py-3 text-secondaire">{c.contenu}</td>
                <td className={cn("px-5 py-3 text-[13px]", c.etat === "attente" ? "font-semibold text-rouge-texte" : "text-secondaire")}>{c.validations}</td>
                <td className="whitespace-nowrap px-5 py-3"><Badge ton={c.etat === "recue" ? "vert" : c.etat === "attente" ? "neutre" : "orange"}>{c.etatLabel}</Badge></td>
                <td className="whitespace-nowrap px-3 py-3 text-right" onClick={(e) => e.stopPropagation()}>
                  {c.etat !== "attente" && (
                    <Select className="h-8 w-40 text-[13px]" aria-label={`État de la commande ${c.reference}`} value={c.etat} onChange={(e) => changer(c, e.target.value)}>
                      {ETATS.map(([code, label]) => <option key={code} value={code}>{label}</option>)}
                    </Select>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {data && data.commandes.length === 0 && (
          <div className="px-6 py-14 text-center">
            <div className="font-display font-bold">Aucune commande usine</div>
            <div className="text-secondaire">Une commande apparaît ici dès qu&apos;elle est soumise à validation, à l&apos;étape 4 du suivi d&apos;une dalle.</div>
          </div>
        )}
      </div>
      {data && <div className="mt-3 text-[13px] text-secondaire">{data.commandes.length} commande(s)</div>}
    </div>
  );
}

function EtatStock() {
  const [entite, setEntite] = useState("APT");
  const [stock, setStock] = useState<Stock | null>(null);
  const [erreur, setErreur] = useState("");
  const [mvt, setMvt] = useState<{ code: string; sens: string; quantite: string; motif: string } | null>(null);
  const [reglage, setReglage] = useState<{ code: string; article: string; initial: string; seuil: string } | null>(null);
  const [nouveau, setNouveau] = useState({ type: "ancrage", torons: "" });

  useEffect(() => { api<Stock>(`/logistique/stock?entite=${entite}`).then(setStock).catch(() => setErreur("Chargement impossible.")); }, [entite]);

  async function envoyer(chemin: string, methode: string, corps: object, apres: () => void) {
    setErreur("");
    try { setStock(await api<Stock>(chemin, { method: methode, body: JSON.stringify(corps) })); apres(); }
    catch (e) { setErreur(e instanceof ApiError ? e.message : "Action impossible."); }
  }

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-center gap-2">
        {[["APT", "Africa PT / MK4"], ["BBR", "BECOMAR"]].map(([code, label]) => (
          <button key={code} aria-pressed={entite === code} onClick={() => setEntite(code)}
            className={cn("cursor-pointer rounded-full border px-3.5 py-1.5 text-[13px] font-semibold", entite === code ? "border-marine bg-marine text-white" : "border-bordure-forte bg-white hover:bg-fond")}>
            {code} · {label}
          </button>
        ))}
        <Button className="ml-auto" onClick={() => setMvt({ code: stock?.articles[0]?.code ?? "toron", sens: "entree", quantite: "", motif: "" })}>Mouvement manuel</Button>
      </div>
      {erreur && !mvt && !reglage && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}

      <div className="overflow-x-auto rounded-xl border border-bordure bg-white">
        <table className="w-full text-left">
          <thead className="border-b border-bordure bg-fond text-[12px] uppercase tracking-wide text-secondaire">
            <tr>
              <th className="px-5 py-3 font-semibold">Article</th>
              <th className="px-5 py-3 text-right font-semibold">Stock initial</th>
              <th className="px-5 py-3 text-right font-semibold">Mouvements manuels</th>
              <th className="px-5 py-3 text-right font-semibold">Consommé</th>
              <th className="px-5 py-3 text-right font-semibold">Restant</th>
              <th className="px-5 py-3 text-right font-semibold">Seuil</th>
              <th className="px-5 py-3 font-semibold">Niveau</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {stock?.articles.map((a) => (
              <tr key={a.code} className="border-b border-bordure last:border-0">
                <td className="px-5 py-3 font-semibold">{a.article}</td>
                <td className="px-5 py-3 text-right font-display">{n0(a.stockInitial)}</td>
                <td className="px-5 py-3 text-right font-display">{signe(a.mouvements)}</td>
                <td className="px-5 py-3 text-right font-display">{n0(a.consomme)}</td>
                <td className={cn("px-5 py-3 text-right font-display font-bold", a.bas && "text-rouge-texte")}>{n0(a.restant)}</td>
                <td className="px-5 py-3 text-right font-display text-secondaire">{n0(a.seuil)}</td>
                <td className="px-5 py-3"><Badge ton={a.bas ? "rouge" : "vert"}>{a.bas ? "Stock bas" : "Suffisant"}</Badge></td>
                <td className="px-3 py-3 text-right">
                  <Button size="sm" variant="ghost" onClick={() => setReglage({ code: a.code, article: a.article, initial: String(a.stockInitial).replace(".", ","), seuil: String(a.seuil).replace(".", ",") })}>Régler</Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="mt-2 text-[12px] text-attenue">
        Restant = stock initial + mouvements manuels − consommé. Le stock baisse dès qu&apos;une commande usine passe à l&apos;état « Livrée », et remonte si elle revient en arrière. Les accessoires viennent du Paramétrage.
      </p>

      <div className="mt-3 flex flex-wrap items-end gap-2">
        <span className="pb-2 text-[13px] font-semibold text-secondaire">Ajouter un article</span>
        <Select className="h-9 w-32" aria-label="Type d'article" value={nouveau.type} onChange={(e) => setNouveau({ ...nouveau, type: e.target.value })}>
          <option value="ancrage">Ancrage</option><option value="gaine">Gaine</option>
        </Select>
        <Input className="h-9 w-36" inputMode="numeric" placeholder="Système (torons)" aria-label="Système" value={nouveau.torons} onChange={(e) => setNouveau({ ...nouveau, torons: e.target.value })} />
        <Button size="sm" variant="outline" disabled={!(parseInt(nouveau.torons, 10) >= 1)}
          onClick={() => envoyer("/logistique/stock/articles", "PUT", { entite, code: `${nouveau.type}:${parseInt(nouveau.torons, 10)}`, stockInitial: 0, seuil: 0 }, () => setNouveau({ ...nouveau, torons: "" }))}>Ajouter</Button>
      </div>

      <div className="mt-6 rounded-xl border border-bordure bg-white p-6">
        <h3 className="mb-3 text-[15px] font-bold">Derniers mouvements manuels</h3>
        {stock?.mouvements.length === 0 && <div className="text-[13px] text-attenue">Aucun mouvement manuel.</div>}
        <ul className="divide-y divide-bordure text-[13px]">
          {stock?.mouvements.map((m, i) => (
            <li key={i} className="flex flex-wrap gap-x-4 gap-y-1 py-2">
              <span className="font-display text-secondaire">{formatDate(m.date)}</span>
              <span className="font-semibold">{m.article}</span>
              <span className="font-display">{signe(m.quantite)}</span>
              <span className="flex-1">{m.motif}{m.sens === "correction" && " (correction d'inventaire)"}</span>
              <span className="text-secondaire">{m.par}</span>
            </li>
          ))}
        </ul>
      </div>

      <Modal open={!!mvt} onOpenChange={(o) => !o && setMvt(null)} title="Nouveau mouvement manuel" description={`Entité ${entite}`}
        footer={
          <>
            <Button variant="outline" onClick={() => setMvt(null)}>Annuler</Button>
            <Button disabled={!mvt?.motif.trim() || !lireNombre(mvt?.quantite ?? "")}
              onClick={() => mvt && envoyer("/logistique/stock/mouvements", "POST", { entite, code: mvt.code, sens: mvt.sens, quantite: lireNombre(mvt.quantite), motif: mvt.motif }, () => setMvt(null))}>Enregistrer</Button>
          </>
        }>
        {mvt && (
          <div className="space-y-3">
            <Field label="Article"><Select value={mvt.code} onChange={(e) => setMvt({ ...mvt, code: e.target.value })}>{stock?.articles.map((a) => <option key={a.code} value={a.code}>{a.article}</option>)}</Select></Field>
            <Field label="Sens">
              <Select value={mvt.sens} onChange={(e) => setMvt({ ...mvt, sens: e.target.value })}>
                <option value="entree">Entrée en stock</option><option value="sortie">Sortie de stock</option><option value="correction">Correction d&apos;inventaire</option>
              </Select>
            </Field>
            <Field label={mvt.sens === "correction" ? "Écart, positif ou négatif" : "Quantité"}><Input inputMode="decimal" value={mvt.quantite} onChange={(e) => setMvt({ ...mvt, quantite: e.target.value })} /></Field>
            <Field label="Motif" required><Input value={mvt.motif} onChange={(e) => setMvt({ ...mvt, motif: e.target.value })} /></Field>
            {erreur && <div role="alert" className="text-[13px] text-rouge-texte">{erreur}</div>}
          </div>
        )}
      </Modal>

      <Modal open={!!reglage} onOpenChange={(o) => !o && setReglage(null)} title={reglage?.article ?? ""} description="Stock initial et seuil d'alerte de l'article."
        footer={
          <>
            <Button variant="outline" onClick={() => setReglage(null)}>Annuler</Button>
            <Button onClick={() => reglage && envoyer("/logistique/stock/articles", "PUT", { entite, code: reglage.code, stockInitial: lireNombre(reglage.initial), seuil: lireNombre(reglage.seuil) }, () => setReglage(null))}>Enregistrer</Button>
          </>
        }>
        {reglage && (
          <div className="grid grid-cols-2 gap-3">
            <Field label="Stock initial"><Input inputMode="decimal" value={reglage.initial} onChange={(e) => setReglage({ ...reglage, initial: e.target.value })} /></Field>
            <Field label="Seuil d'alerte"><Input inputMode="decimal" value={reglage.seuil} onChange={(e) => setReglage({ ...reglage, seuil: e.target.value })} /></Field>
            {erreur && <div role="alert" className="col-span-2 text-[13px] text-rouge-texte">{erreur}</div>}
          </div>
        )}
      </Modal>
    </div>
  );
}

type LigneSynthese = { libelle: string; commandes: number; surface: number; ancrages: number; gaine: number; torons: number; tonnes: number };
type Synthese = { annee: number; kgParMetre: number; total: LigneSynthese; entites: LigneSynthese[]; mois: LigneSynthese[] };

/** Matière livrée dans l'année : par entité et par mois, avec le tonnage de torons. */
function SyntheseAnnuelle() {
  const courante = new Date().getFullYear();
  const [annee, setAnnee] = useState(courante);
  const [s, setS] = useState<Synthese | null>(null);
  useEffect(() => { api<Synthese>(`/logistique/synthese?annee=${annee}`).then(setS).catch(() => setS(null)); }, [annee]);
  const nb = (v: number, d = 0) => v.toLocaleString("fr-FR", { minimumFractionDigits: d, maximumFractionDigits: d });
  const ligne = (l: LigneSynthese, gras?: boolean) => (
    <tr key={l.libelle} className={cn("border-b border-bordure last:border-0", gras && "bg-fond font-bold")}>
      <td className="px-5 py-2.5 capitalize">{l.libelle}</td><td className="px-5 py-2.5 text-right font-display">{nb(l.commandes)}</td>
      <td className="px-5 py-2.5 text-right font-display">{nb(l.surface, 2)}</td><td className="px-5 py-2.5 text-right font-display">{nb(l.ancrages)}</td>
      <td className="px-5 py-2.5 text-right font-display">{nb(l.gaine, 1)}</td><td className="px-5 py-2.5 text-right font-display">{nb(l.torons, 1)}</td>
      <td className="px-5 py-2.5 text-right font-display font-bold">{nb(l.tonnes, 3)}</td>
    </tr>
  );
  const entete = (titre: string) => (
    <thead className="border-b border-bordure bg-fond text-[12px] uppercase tracking-wide text-secondaire">
      <tr><th className="px-5 py-2.5 font-semibold">{titre}</th><th className="px-5 py-2.5 text-right font-semibold">Commandes livrées</th><th className="px-5 py-2.5 text-right font-semibold">Surface m²</th>
        <th className="px-5 py-2.5 text-right font-semibold">Ancrages</th><th className="px-5 py-2.5 text-right font-semibold">Gaine m</th><th className="px-5 py-2.5 text-right font-semibold">Torons m</th><th className="px-5 py-2.5 text-right font-semibold">Torons, tonnes</th></tr>
    </thead>
  );
  return (
    <div>
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <select aria-label="Année" className="h-10 rounded-lg border border-bordure-forte bg-white px-3" value={annee} onChange={(e) => setAnnee(Number(e.target.value))}>
          {[courante, courante - 1, courante - 2].map((a) => <option key={a} value={a}>{a}</option>)}
        </select>
        <span className="text-[13px] text-secondaire">Commandes usine livrées dans l&apos;année. Tonnage calculé à {s ? nb(s.kgParMetre, 2) : "1,18"} kg par mètre de toron T15,7.</span>
      </div>
      {s && (
        <>
          <div className="mb-5 overflow-x-auto rounded-xl border border-bordure bg-white">
            <table className="w-full text-left text-[13px]">{entete("Entité")}<tbody>{s.entites.map((l) => ligne(l))}{ligne({ ...s.total, libelle: `Total ${s.annee}` }, true)}</tbody></table>
          </div>
          <div className="overflow-x-auto rounded-xl border border-bordure bg-white">
            <table className="w-full text-left text-[13px]">{entete("Mois")}<tbody>{s.mois.map((l) => ligne(l))}</tbody></table>
          </div>
        </>
      )}
    </div>
  );
}

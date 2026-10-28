"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { FileDown, Plus, Search } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { cn, formatDate, formatDH, formatNombre, lireNombre } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field, Input, Select } from "@/components/ui/input";
import { Sheet } from "@/components/ui/sheet";

type Ligne = { libelle: string; surface: number | null; prixM2: number | null; montantHT: number };
type Facture = {
  id: string; numero: string; ancienNumero: string | null; entite: string; type: string; projetId: string; projetCode: string; projet: string;
  client: string; clientIdentifiants: string | null; clientAdresse: string | null; dateFacture: string; echeance: string; origine: string;
  totalHT: number; acompteDeduit: number; netHT: number; tauxTva: number; tva: number; totalTTC: number; regle: number;
  reste: number; statut: string; statutLabel: string; emisPar: string; emisLe: string; lignes: Ligne[] | null;
  signePar: string | null; signeLe: string | null;
  encaissements: Encaissement[] | null; plafondEspeces: number; avoirsTTC: number; du: number; avoirs: Avoir[] | null; effetsEnAttente: number; aCouvrir: number;
};
type Avoir = { id: string; numero: string; ancienNumero: string | null; dateAvoir: string; motif: string; total: boolean; montantHT: number; tva: number; totalTTC: number; emisPar: string; signePar: string | null; signeLe: string | null; annule: boolean; motifAnnulation: string | null; annulePar: string | null };
type AvoirListe = { id: string; numero: string; entite: string; dateAvoir: string; motif: string; total: boolean; totalTTC: number; annule: boolean; factureId: string; factureNumero: string; projet: string; client: string };

const ANNULATION_AVOIR = ["directeur", "chef_projet", "responsable_administratif"];
type Encaissement = {
  id: string; datePaiement: string; mode: string; modeLabel: string; montant: number; reference: string | null; banque: string | null; numeroRecu: string | null; echeanceEffet: string | null; enAttente: boolean; remboursement: boolean;
  saisiPar: string; annule: boolean; motifAnnulation: string | null; annulePar: string | null; dalles?: { reference: string; montant: number }[];
};
const TON: Record<string, "neutre" | "orange" | "vert" | "rouge" | "violet"> = { impayee: "neutre", partielle: "orange", retard: "rouge", payee: "vert", annulee: "violet" };
type Liste = {
  indicateurs: { facture: number; encaisse: number; reste: number; retard: number };
  compteurs: { statut: string; label: string; nb: number }[]; items: Facture[]; total: number;
};
type Bon = { id: string; numero: string; dateLivraison: string; surface: number; facturable: boolean; totalHT: number; acompte: number; lignes: { reference: string; emplacement: string; surface: number; prixM2: number | null; montantHT: number }[] };
type Preparation = {
  projetId: string; code: string; libelle: string; entite: string; client: string; clientIdentifiants: string | null; exonereTva: boolean;
  refus: string | null; tauxTva: number; delaiMaxJours: number;
  devis: { id: string; numero: string; version: number; netHT: number; acompteDejaFacture: number }[]; bons: Bon[];
};
type ProjetOption = { id: string; code: string; libelle: string; client: string };

const PAGE = 20;
const ANNEE = new Date().getFullYear();
const ENTITES: Record<string, string> = { APT: "Africa PT / MK4", BBR: "BECOMAR" };
const iso = (d: Date) => d.toLocaleDateString("fr-CA");
const plus = (date: string, jours: number) => { const d = new Date(date); d.setDate(d.getDate() + jours); return iso(d); };

export default function FacturationPage() {
  const [statut, setStatut] = useState("");
  const [saisie, setSaisie] = useState("");
  const [q, setQ] = useState("");
  const [annee, setAnnee] = useState(String(ANNEE));
  const [page, setPage] = useState(1);
  const [data, setData] = useState<Liste | null>(null);
  const [erreur, setErreur] = useState("");
  const [creation, setCreation] = useState(false);
  const [ouverte, setOuverte] = useState<Facture | null>(null);
  const [avoirs, setAvoirs] = useState<AvoirListe[] | null>(null);
  const vueAvoirs = statut === "avoirs";

  useEffect(() => { const t = setTimeout(() => { setQ(saisie.trim()); setPage(1); }, 300); return () => clearTimeout(t); }, [saisie]);

  const charger = useCallback(() => {
    const p = new URLSearchParams({ page: String(page), pageSize: String(PAGE) });
    if (statut && statut !== "avoirs") p.set("statut", statut);
    if (statut === "avoirs") {
      const pa = new URLSearchParams(); if (q) pa.set("q", q); if (annee) pa.set("annee", annee);
      api<AvoirListe[]>(`/avoirs?${pa}`).then(setAvoirs).catch(() => setAvoirs([]));
    }
    if (q) p.set("q", q);
    if (annee) p.set("annee", annee);
    api<Liste>(`/factures?${p}`).then((r) => { setData(r); setErreur(""); }).catch((e) => setErreur(e instanceof ApiError ? e.message : "Chargement impossible."));
  }, [page, statut, q, annee]);
  useEffect(() => { charger(); }, [charger]);

  const ouvrir = (id: string) => api<Facture>(`/factures/${id}`).then(setOuverte).catch(() => undefined);
  const k = data?.indicateurs;
  const onglets = [{ statut: "", label: "Toutes", nb: (data?.compteurs ?? []).reduce((s, c) => s + c.nb, 0) }, ...(data?.compteurs ?? [])];
  const pages = data ? Math.max(1, Math.ceil(data.total / PAGE)) : 1;

  return (
    <div>
      <div className="mb-5 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {([[`Facturé${annee ? ` en ${annee}` : ""}, TTC`, k?.facture, ""], ["Encaissé", k?.encaisse, "text-vert-texte"], ["Reste à encaisser", k?.reste, ""], ["Dont en retard", k?.retard, "text-rouge-texte"]] as [string, number | undefined, string][]).map(([label, v, c]) => (
          <div key={label} className="rounded-xl border border-bordure bg-white p-5">
            <div className="text-[13px] text-secondaire">{label}</div>
            <div className={cn("mt-1 font-display text-xl font-bold", c)}>{v === undefined ? "" : formatDH(v)}</div>
          </div>
        ))}
      </div>

      <div className="mb-4 flex flex-wrap items-center gap-2">
        {onglets.map((o) => (
          <button key={o.statut} aria-pressed={statut === o.statut} onClick={() => { setStatut(o.statut); setPage(1); }}
            className={cn("flex cursor-pointer items-center gap-2 rounded-full border px-3.5 py-1.5 text-[13px] font-semibold", statut === o.statut ? "border-marine bg-marine text-white" : "border-bordure-forte bg-white hover:bg-fond")}>
            {o.label}<span className={cn("font-display text-[12px]", statut === o.statut ? "text-white/80" : "text-attenue")}>{o.nb}</span>
          </button>
        ))}
        <button aria-pressed={vueAvoirs} onClick={() => { setStatut("avoirs"); setPage(1); }}
          className={cn("cursor-pointer rounded-full border px-3.5 py-1.5 text-[13px] font-semibold", vueAvoirs ? "border-marine bg-marine text-white" : "border-bordure-forte bg-white hover:bg-fond")}>Avoirs</button>
        <Button className="ml-auto" onClick={() => setCreation(true)}><Plus size={16} />Nouvelle facture</Button>
      </div>
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <div className="relative w-full max-w-xs">
          <Search size={16} className="pointer-events-none absolute left-3 top-3 text-attenue" />
          <Input className="pl-9" placeholder="Numéro, projet ou client" value={saisie} onChange={(e) => setSaisie(e.target.value)} />
        </div>
        <Select className="w-auto" aria-label="Période" value={annee} onChange={(e) => { setAnnee(e.target.value); setPage(1); }}>
          <option value={ANNEE}>Période : {ANNEE}</option><option value={ANNEE - 1}>{ANNEE - 1}</option><option value="">Toutes</option>
        </Select>
      </div>
      {erreur && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}

      {vueAvoirs && (
        <div className="overflow-x-auto rounded-xl border border-bordure bg-white">
          <table className="w-full text-left">
            <thead className="border-b border-bordure bg-fond text-[12px] uppercase tracking-wide text-secondaire">
              <tr><th className="px-5 py-3 font-semibold">Avoir</th><th className="px-5 py-3 font-semibold">Date</th><th className="px-5 py-3 font-semibold">Facture</th>
                <th className="px-5 py-3 font-semibold">Projet et client</th><th className="px-5 py-3 font-semibold">Motif</th><th className="px-5 py-3 text-right font-semibold">Montant TTC</th></tr>
            </thead>
            <tbody>
              {avoirs?.map((a) => (
                <tr key={a.id} onClick={() => ouvrir(a.factureId)} className="cursor-pointer border-b border-bordure last:border-0 hover:bg-fond">
                  <td className="whitespace-nowrap px-5 py-3"><div className="font-display font-bold text-marine">{a.numero}</div><div className="text-[12px] text-attenue">{a.entite}</div></td>
                  <td className="whitespace-nowrap px-5 py-3 font-display">{formatDate(a.dateAvoir)}</td>
                  <td className="whitespace-nowrap px-5 py-3"><div className="font-display">{a.factureNumero}</div><div className="text-[12px] text-secondaire">{a.total ? "Avoir total" : "Avoir partiel"}{a.annule ? " · annulé" : ""}</div></td>
                  <td className="px-5 py-3"><div className="font-semibold">{a.projet}</div><div className="text-[13px] text-secondaire">{a.client}</div></td>
                  <td className="px-5 py-3 text-[13px] text-secondaire">{a.motif}</td>
                  <td className={cn("whitespace-nowrap px-5 py-3 text-right font-display font-bold", a.annule && "text-attenue line-through")}>{formatDH(-a.totalTTC)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {avoirs && avoirs.length === 0 && <div className="px-6 py-14 text-center"><div className="font-display font-bold">Aucun avoir</div></div>}
        </div>
      )}
      <div className={cn("overflow-x-auto rounded-xl border border-bordure bg-white", vueAvoirs && "hidden")}>
        <table className="w-full text-left">
          <thead className="border-b border-bordure bg-fond text-[12px] uppercase tracking-wide text-secondaire">
            <tr>
              <th className="px-5 py-3 font-semibold">Document</th>
              <th className="px-5 py-3 font-semibold">Émise le, échéance</th>
              <th className="px-5 py-3 font-semibold">Projet et client</th>
              <th className="px-5 py-3 font-semibold">Origine</th>
              <th className="px-5 py-3 text-right font-semibold">Montant TTC</th>
              <th className="px-5 py-3 text-right font-semibold">Réglé</th>
              <th className="px-5 py-3 font-semibold">Paiement</th>
            </tr>
          </thead>
          <tbody>
            {data?.items.map((f) => (
              <tr key={f.id} onClick={() => ouvrir(f.id)} className="cursor-pointer border-b border-bordure last:border-0 hover:bg-fond">
                <td className="whitespace-nowrap px-5 py-3"><div className="font-display font-bold text-marine">{f.numero}</div><div className="text-[12px] text-attenue">{f.entite}</div></td>
                <td className="whitespace-nowrap px-5 py-3">
                  <div className="font-display">{formatDate(f.dateFacture)}</div>
                  <div className={cn("text-[12px]", f.statut === "retard" ? "font-semibold text-rouge-texte" : "text-secondaire")}>{f.statut === "retard" ? "Échue le" : "Échéance"} {formatDate(f.echeance)}</div>
                </td>
                <td className="px-5 py-3"><div className="font-semibold">{f.projet}</div><div className="text-[13px] text-secondaire">{f.client}</div></td>
                <td className="px-5 py-3 text-[13px] text-secondaire">{f.origine}</td>
                <td className="whitespace-nowrap px-5 py-3 text-right font-display font-bold">{formatDH(f.totalTTC)}{f.avoirsTTC > 0 && <div className="text-[12px] font-normal text-violet-texte">Avoirs {formatDH(-f.avoirsTTC)}</div>}</td>
                <td className="whitespace-nowrap px-5 py-3 text-right font-display">{formatDH(f.regle)}</td>
                <td className="px-5 py-3"><Badge ton={TON[f.statut] ?? "neutre"}>{f.statutLabel}</Badge></td>
              </tr>
            ))}
          </tbody>
        </table>
        {data && data.items.length === 0 && (
          <div className="px-6 py-14 text-center"><div className="font-display font-bold">Aucune facture</div><div className="text-secondaire">Modifiez la recherche, la période ou le statut.</div></div>
        )}
      </div>
      {data && !vueAvoirs && (
        <div className="mt-3 flex items-center justify-between gap-3 text-[13px] text-secondaire">
          <span>{data.total} document(s) · une facture émise ne se modifie pas : une erreur se corrige par un avoir</span>
          {data.total > PAGE && (
            <span className="flex items-center gap-3">Page {page} sur {pages}
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>Précédent</Button>
              <Button variant="outline" size="sm" disabled={page >= pages} onClick={() => setPage(page + 1)}>Suivant</Button>
            </span>
          )}
        </div>
      )}

      {creation && <Creation onClose={() => setCreation(false)} onEmise={(f) => { setCreation(false); charger(); setOuverte(f); }} />}
      {ouverte && (
        <Sheet open onOpenChange={(o) => !o && setOuverte(null)} title={ouverte.numero}
          description={`Émise le ${formatDate(ouverte.emisLe)} par ${ouverte.emisPar}${ouverte.ancienNumero ? ` · ancien numéro ${ouverte.ancienNumero}` : ""}`}>
          <Apercu entite={ouverte.entite} numero={ouverte.numero} date={ouverte.dateFacture} echeance={ouverte.echeance} client={ouverte.client}
            identifiants={ouverte.clientIdentifiants} projet={`${ouverte.projetCode} · ${ouverte.projet}`} lignes={ouverte.lignes ?? []}
            totalHT={ouverte.totalHT} acompte={ouverte.acompteDeduit} tauxTva={ouverte.tauxTva} acompteSeul={ouverte.type === "acompte"} />
          <SignatureDoc signePar={ouverte.signePar} signeLe={ouverte.signeLe} quoi="la facture" url={`/factures/${ouverte.id}`} onSigne={(f) => setOuverte(f)} />
          <Avoirs facture={ouverte} onChange={(f) => { setOuverte(f); charger(); }} />
          <Reglements facture={ouverte} />
        </Sheet>
      )}
    </div>
  );
}

function Apercu({ entite, numero, date, echeance, client, identifiants, projet, lignes, totalHT, acompte, tauxTva, acompteSeul }: {
  entite: string; numero: string; date: string; echeance: string; client: string; identifiants: string | null; projet: string;
  lignes: Ligne[]; totalHT: number; acompte: number; tauxTva: number; acompteSeul: boolean;
}) {
  const net = totalHT - acompte, tva = Math.round(net * tauxTva) / 100;
  return (
    <div className="rounded-lg border border-bordure p-5">
      <div className="flex items-start justify-between gap-4">
        <div className="font-display text-lg font-bold text-marine">{ENTITES[entite] ?? entite}</div>
        <div className="text-right">
          <div className="font-display font-bold">Facture</div>
          <div className="font-display text-[13px] text-secondaire">{numero} · {formatDate(date)}</div>
          <div className="text-[12px] text-secondaire">Échéance le {formatDate(echeance)}</div>
        </div>
      </div>
      <dl className="mt-4 space-y-1 text-[13px]">
        <div><dt className="inline text-secondaire">Client : </dt><dd className="inline font-semibold">{client}{identifiants ? ` · ${identifiants}` : ""}</dd></div>
        <div><dt className="inline text-secondaire">Projet : </dt><dd className="inline font-semibold">{projet}</dd></div>
      </dl>
      <table className="mt-4 w-full text-left text-[13px]">
        <thead className="border-b border-bordure-forte text-[11px] uppercase tracking-wide text-secondaire">
          <tr><th className="py-1.5 font-semibold">Désignation</th><th className="py-1.5 pl-4 text-right whitespace-nowrap font-semibold">m²</th><th className="py-1.5 text-right font-semibold">DH/m²</th><th className="py-1.5 text-right font-semibold">HT</th></tr>
        </thead>
        <tbody>
          {lignes.map((l, i) => (
            <tr key={i} className="border-b border-bordure">
              <td className="py-1.5">{l.libelle}</td>
              <td className="py-1.5 pl-4 text-right whitespace-nowrap font-display">{l.surface === null ? "" : formatNombre(l.surface)}</td>
              <td className="py-1.5 pl-4 text-right whitespace-nowrap font-display">{l.prixM2 === null ? "" : formatNombre(l.prixM2)}</td>
              <td className="py-1.5 pl-4 text-right whitespace-nowrap font-display">{formatNombre(l.montantHT)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {lignes.length === 0 && <div className="mt-3 text-[13px] text-attenue">Sélectionnez au moins un bon de livraison.</div>}
      <dl className="ml-auto mt-4 w-full max-w-xs space-y-1 text-[13px]">
        {!acompteSeul && <div className="flex justify-between"><dt className="text-secondaire">Total HT des dalles</dt><dd className="font-display">{formatDH(totalHT)}</dd></div>}
        {acompte > 0 && <div className="flex justify-between"><dt className="text-secondaire">Acompte déduit au prorata</dt><dd className="font-display">− {formatDH(acompte)}</dd></div>}
        <div className="flex justify-between"><dt className="text-secondaire">Net HT</dt><dd className="font-display">{formatDH(net)}</dd></div>
        <div className="flex justify-between"><dt className="text-secondaire">{tauxTva === 0 ? "TVA 0 %, client exonéré" : `TVA ${tauxTva} %`}</dt><dd className="font-display">{formatDH(tva)}</dd></div>
        <div className="flex justify-between border-t border-bordure pt-1.5 text-[15px] font-bold"><dt>Total TTC</dt><dd className="font-display">{formatDH(net + tva)}</dd></div>
      </dl>
    </div>
  );
}

function Creation({ onClose, onEmise }: { onClose: () => void; onEmise: (f: Facture) => void }) {
  const [recherche, setRecherche] = useState("");
  const [options, setOptions] = useState<ProjetOption[]>([]);
  const [prep, setPrep] = useState<Preparation | null>(null);
  const [choix, setChoix] = useState<string[]>([]);
  const [date, setDate] = useState(iso(new Date()));
  const [echeance, setEcheance] = useState(plus(iso(new Date()), 30));
  const [modeReglement, setModeReglement] = useState("virement");
  const [erreur, setErreur] = useState("");
  const [envoi, setEnvoi] = useState(false);

  useEffect(() => {
    if (prep) return;
    const t = setTimeout(() => {
      api<{ items: ProjetOption[] }>(`/projets?pageSize=8${recherche.trim() ? `&q=${encodeURIComponent(recherche.trim())}` : ""}`).then((r) => setOptions(r.items)).catch(() => setOptions([]));
    }, 200);
    return () => clearTimeout(t);
  }, [recherche, prep]);

  const choisir = (p: ProjetOption) => api<Preparation>(`/factures/preparation?projetId=${p.id}`)
    .then((r) => { setPrep(r); setChoix([]); setErreur(""); }).catch(() => setErreur("Chargement impossible."));

  const calcul = useMemo(() => {
    if (!prep) return { lignes: [] as Ligne[], totalHT: 0, acompte: 0 };
    const bons = prep.bons.filter((b) => choix.includes(b.id));
    return {
      lignes: bons.flatMap((b) => b.lignes.map((l) => ({ libelle: `${l.reference} · ${l.emplacement}`, surface: l.surface, prixM2: l.prixM2, montantHT: l.montantHT }))),
      totalHT: bons.reduce((s, b) => s + b.totalHT, 0), acompte: bons.reduce((s, b) => s + b.acompte, 0),
    };
  }, [prep, choix]);

  async function emettre() {
    if (!prep) return;
    setEnvoi(true); setErreur("");
    try {
      onEmise(await api<Facture>("/factures", {
        method: "POST",
        body: JSON.stringify({ projetId: prep.projetId, type: "livraison", bonIds: choix, dateFacture: date, echeance, modeReglement }),
      }));
    } catch (e) { setErreur(e instanceof ApiError ? e.message : "Émission impossible."); setEnvoi(false); }
  }

  return (
    <Sheet open onOpenChange={(o) => !o && onClose()} title="Nouvelle facture" description="1. Projet · 2. Bons de livraison · 3. Aperçu avant émission"
      footer={<><Button variant="outline" onClick={onClose}>Annuler</Button><Button disabled={envoi || !prep || !!prep.refus || calcul.lignes.length === 0} onClick={emettre}>Émettre la facture</Button></>}>
      {erreur && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}
      {!prep ? (
        <Field label="Projet">
          <Input autoFocus placeholder="Rechercher par code, libellé ou client" value={recherche} onChange={(e) => setRecherche(e.target.value)} />
          <ul className="mt-2 divide-y divide-bordure rounded-lg border border-bordure">
            {options.map((p) => (
              <li key={p.id}><button type="button" onClick={() => choisir(p)} className="flex w-full cursor-pointer items-center gap-3 px-3 py-2 text-left hover:bg-fond">
                <span className="font-display font-bold text-marine">{p.code}</span><span className="flex-1 truncate">{p.libelle}</span><span className="text-[12px] text-secondaire">{p.client}</span>
              </button></li>
            ))}
            {options.length === 0 && <li className="px-3 py-2 text-[13px] text-attenue">Aucun projet.</li>}
          </ul>
        </Field>
      ) : (
        <div className="space-y-5">
          <div className="flex items-center justify-between gap-3 rounded-lg bg-fond px-4 py-3">
            <div><span className="font-display font-bold text-marine">{prep.code}</span> · {prep.libelle}<div className="text-[13px] text-secondaire">{prep.client}{prep.exonereTva ? " · exonéré de TVA" : ""}</div></div>
            <Button variant="ghost" size="sm" onClick={() => setPrep(null)}>Changer</Button>
          </div>
          {prep.refus ? <div role="alert" className="rounded-lg bg-orange-fond px-4 py-3 text-orange-texte">{prep.refus}</div> : (
            <>
              <div>
                  <div className="mb-1.5 text-[13px] font-semibold text-secondaire">Bons de livraison non facturés</div>
                  <ul className="divide-y divide-bordure rounded-lg border border-bordure">
                    {prep.bons.map((b) => {
                      const coche = choix.includes(b.id);
                      return (
                        <li key={b.id}>
                          <label className={cn("flex items-center gap-3 px-3 py-2.5", b.facturable ? "cursor-pointer" : "bg-fond text-secondaire", coche && "bg-marine/5")}>
                            <input type="checkbox" className="h-4 w-4 accent-marine" disabled={!b.facturable} checked={coche} onChange={(e) => setChoix(e.target.checked ? [...choix, b.id] : choix.filter((x) => x !== b.id))} />
                            <span className="font-display font-bold">{b.numero}</span>
                            <span className="flex-1 truncate text-[13px]">{formatDate(b.dateLivraison)} · {b.lignes.map((l) => l.reference.split("-").pop()).join(", ")} · {formatNombre(b.surface)} m²</span>
                            <span className="font-display text-[13px]">{b.facturable ? `${formatDH(b.totalHT)} HT` : "Dalle absente du devis validé"}</span>
                          </label>
                        </li>
                      );
                    })}
                    {prep.bons.length === 0 && <li className="px-3 py-2 text-[13px] text-attenue">Aucun bon de livraison à facturer pour ce projet.</li>}
                  </ul>
                  <p className="mt-1.5 text-[12px] text-attenue">Une facture s&apos;établit uniquement à partir de bons de livraison. Un bon ne peut être facturé qu&apos;une fois ; plusieurs bons peuvent être regroupés sur une facture. Le prix au m² est repris du devis validé, la surface du bon (celle de la commande usine).</p>
                </div>

              <div className="grid grid-cols-2 gap-4">
                <Field label="Date de facture"><Input type="date" value={date} onChange={(e) => { setDate(e.target.value); if (e.target.value) setEcheance(plus(e.target.value, 30)); }} /></Field>
                <Field label="Échéance de paiement"><Input type="date" value={echeance} onChange={(e) => setEcheance(e.target.value)} /></Field>
                <p className="col-span-2 -mt-2 text-[12px] text-attenue">Délai légal : moins de {prep.delaiMaxJours} jours, soit le {date ? formatDate(plus(date, prep.delaiMaxJours - 1)) : ""} au plus tard. Une date au-delà est refusée.</p>
                <Field label="Mode de règlement, imprimé sur la facture" className="col-span-2">
                  <Select value={modeReglement} onChange={(e) => setModeReglement(e.target.value)}><option value="virement">Virement bancaire</option><option value="cheque">Chèque</option><option value="effet">Effet</option><option value="especes">Espèces</option></Select>
                </Field>
                {modeReglement === "especes" && (
                  <p className={cn("col-span-2 text-[12px]", (calcul.totalHT - calcul.acompte) * (1 + prep.tauxTva / 100) > 5000 ? "font-semibold text-rouge-texte" : "text-secondaire")}>
                    Espèces : 5 000 DH TTC au plus par client et par mois, toutes factures en espèces du mois comprises.
                    {(calcul.totalHT - calcul.acompte) * (1 + prep.tauxTva / 100) > 5000 && " Cette facture dépasse le plafond : choisissez un autre mode de règlement."}
                  </p>
                )}
              </div>

              <div>
                <div className="mb-1.5 text-[13px] font-semibold text-secondaire">Aperçu avant émission</div>
                <Apercu entite={prep.entite} numero="Numéro attribué à l'émission" date={date} echeance={echeance} client={prep.client} identifiants={prep.clientIdentifiants}
                  projet={`${prep.code} · ${prep.libelle}`} lignes={calcul.lignes} totalHT={calcul.totalHT} acompte={calcul.acompte} tauxTva={prep.tauxTva} acompteSeul={false} />
                <p className="mt-1.5 text-[12px] text-attenue">Une fois émise, la facture ne peut plus être modifiée ni supprimée. Une erreur se corrige par un avoir.</p>
              </div>
            </>
          )}
        </div>
      )}
    </Sheet>
  );
}

/** Règlement d'une facture : imputé depuis les paiements de ses dalles, saisis sur le projet. */
function Reglements({ facture }: { facture: Facture }) {
  const liste = facture.encaissements ?? [];
  return (
    <div className="mt-6">
      <div className="mb-3 flex items-center justify-between gap-3">
        <h3 className="font-display font-bold">Règlements</h3>
        <Badge ton={TON[facture.statut] ?? "neutre"}>{facture.statutLabel}</Badge>
      </div>
      <div className="mb-4 grid grid-cols-3 gap-3 text-[13px]">
        {([[facture.avoirsTTC > 0 ? "Dû après avoirs" : "Total TTC", facture.du, ""], ["Réglé", facture.regle, "text-vert-texte"], ["Reste à payer", Math.max(0, facture.reste), facture.reste > 0 ? "" : "text-vert-texte"]] as [string, number, string][]).map(([l, v, c]) => (
          <div key={l} className="rounded-lg border border-bordure px-3 py-2"><div className="text-secondaire">{l}</div><div className={cn("font-display font-bold", c)}>{formatDH(v)}</div></div>
        ))}
      </div>
      {facture.effetsEnAttente > 0 && <p className="mb-4 text-[12px] text-orange-texte">Effets en attente d&apos;échéance : {formatDH(facture.effetsEnAttente)}. Ils seront comptés comme réglés à leur date d&apos;échéance.</p>}
      <p className="mb-3 text-[12px] text-secondaire">
        Les paiements se saisissent sur le projet, par dalle (onglet « Devis et contrats », Paiements par dalle) ; ils sont imputés aux factures de leurs dalles, les plus anciennes d&apos;abord.{" "}
        <a href={`/projets/${facture.projetId}#commercial`} className="font-semibold text-marine hover:underline">Ouvrir les paiements du projet</a>
      </p>
      {liste.length === 0 ? <p className="text-[13px] text-secondaire">Aucun paiement reçu sur les dalles de cette facture.</p> : (
        <ul className="divide-y divide-bordure rounded-lg border border-bordure">
          {liste.map((e) => (
            <li key={e.id} className="px-3 py-2.5 text-[13px]">
              <div className="flex items-center justify-between gap-3">
                <div className={cn(e.annule && "text-attenue line-through")}>
                  <span className="font-display">{formatDate(e.datePaiement)}</span> · <span className="font-semibold">{e.remboursement ? `Remboursement, ${e.modeLabel.toLowerCase()}` : e.modeLabel}</span>
                  {e.reference ? ` · ${e.reference}` : ""}{e.numeroRecu ? ` · ${e.numeroRecu}` : ""}
                </div>
                <div className="flex items-center gap-3">
                  <span className={cn("whitespace-nowrap font-display font-bold", e.annule && "text-attenue line-through", e.enAttente && "text-orange-texte")}>{formatDH(e.remboursement ? -e.montant : e.montant)}</span>
                  {e.numeroRecu && <a href={`/api/encaissements/${e.id}/recu`} target="_blank" rel="noreferrer" className="text-[12px] font-semibold text-marine hover:underline">Reçu</a>}
                </div>
              </div>
              {e.dalles && e.dalles.length > 0 && <div className="text-[12px] text-secondaire">{e.dalles.map((x) => `${x.reference} : ${formatDH(x.montant)}`).join(" · ")}</div>}
              {e.annule && <div className="text-[12px] text-attenue">Annulé par {e.annulePar} : {e.motifAnnulation}</div>}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function Avoirs({ facture, onChange }: { facture: Facture; onChange: (f: Facture) => void }) {
  const [ouvert, setOuvert] = useState(false);
  const [total, setTotal] = useState(true);
  const [montant, setMontant] = useState("");
  const [date, setDate] = useState(iso(new Date()));
  const [motif, setMotif] = useState("");
  const [erreur, setErreur] = useState("");
  const [envoi, setEnvoi] = useState(false);
  const liste = facture.avoirs ?? [];
  const annulee = facture.statut === "annulee";
  const { user } = useAuth();
  const peutAnnuler = !!user && ANNULATION_AVOIR.includes(user.role);
  const [aAnnuler, setAAnnuler] = useState<string | null>(null);
  const [aSigner, setASigner] = useState<string | null>(null);
  const [motifAnnulation, setMotifAnnulation] = useState("");
  const [erreurAnnulation, setErreurAnnulation] = useState("");
  const annuler = async () => {
    setErreurAnnulation("");
    try { onChange(await api<Facture>(`/avoirs/${aAnnuler}/annuler`, { method: "POST", body: JSON.stringify({ motif: motifAnnulation }) })); setAAnnuler(null); }
    catch (e) { setErreurAnnulation(e instanceof ApiError ? e.message : "Annulation impossible."); }
  };

  const emettre = async () => {
    setEnvoi(true); setErreur("");
    try {
      onChange(await api<Facture>(`/factures/${facture.id}/avoirs`, { method: "POST", body: JSON.stringify({ total, montantHT: total ? null : lireNombre(montant), dateAvoir: date, motif }) }));
      setOuvert(false); setMotif(""); setMontant("");
    } catch (e) { setErreur(e instanceof ApiError ? e.message : "Émission impossible."); }
    finally { setEnvoi(false); }
  };
  const ht = lireNombre(montant) ?? 0;

  return (
    <div className="mt-6">
      <div className="mb-3 flex items-center justify-between gap-3">
        <h3 className="font-display font-bold">Avoirs</h3>
        {!annulee && !ouvert && <Button variant="outline" size="sm" onClick={() => setOuvert(true)}>Émettre un avoir</Button>}
      </div>
      {liste.length === 0 && !ouvert && <p className="text-[13px] text-secondaire">Aucun avoir sur cette facture.</p>}
      {liste.length > 0 && (
        <ul className="divide-y divide-bordure rounded-lg border border-bordure">
          {liste.map((a) => (
            <li key={a.id} className="px-3 py-2.5 text-[13px]">
              <div className="flex items-center justify-between gap-3">
                <div className={cn(a.annule && "text-attenue line-through")}><span className="font-display font-bold text-marine">{a.numero}</span> · <span className="font-display">{formatDate(a.dateAvoir)}</span> · {a.total ? "avoir total" : "avoir partiel"}</div>
                <div className="flex items-center gap-3">
                  <span className={cn("whitespace-nowrap font-display font-bold", a.annule && "text-attenue line-through")}>{formatDH(-a.totalTTC)}</span>
                  {a.signePar
                    ? <a href={`/api/avoirs/${a.id}/pdf`} target="_blank" rel="noreferrer" className="text-[12px] font-semibold text-marine hover:underline">PDF</a>
                    : <button className="cursor-pointer text-[12px] font-semibold text-marine hover:underline" onClick={() => setASigner(aSigner === a.id ? null : a.id)}>Signer</button>}
                  {!a.annule && peutAnnuler && <button className="cursor-pointer text-[12px] font-semibold text-rouge-texte hover:underline" onClick={() => { setAAnnuler(a.id); setMotifAnnulation(""); setErreurAnnulation(""); }}>Annuler</button>}
                </div>
              </div>
              <div className="text-[12px] text-attenue">{a.motif} · {formatDH(a.montantHT)} HT, TVA {formatDH(a.tva)} · émis par {a.emisPar}{a.ancienNumero ? ` · ancien numéro ${a.ancienNumero}` : ""}{a.signePar ? ` · signé par ${a.signePar}` : " · non signé"}{a.annule ? ` · annulé par ${a.annulePar} : ${a.motifAnnulation}` : ""}</div>
              {aSigner === a.id && !a.signePar && <SignatureDoc signePar={null} signeLe={null} quoi="l'avoir" url={`/avoirs/${a.id}`} onSigne={(f) => { setASigner(null); onChange(f); }} />}
              {aAnnuler === a.id && (
                <div className="mt-2">
                  <div className="flex items-end gap-2">
                    <Field label="Motif de l'annulation" required className="flex-1"><Input autoFocus value={motifAnnulation} onChange={(ev) => setMotifAnnulation(ev.target.value)} /></Field>
                    <Button variant="outline" onClick={() => setAAnnuler(null)}>Fermer</Button>
                    <Button disabled={!motifAnnulation.trim()} onClick={annuler}>Confirmer</Button>
                  </div>
                  {erreurAnnulation && <div role="alert" className="mt-2 rounded-lg bg-rouge-fond px-3 py-2 text-[12px] text-rouge-texte">{erreurAnnulation}</div>}
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
      {annulee && <p className="mt-2 text-[12px] text-secondaire">Facture annulée par un avoir total : ses bons de livraison ou son acompte peuvent être refacturés.</p>}
      {ouvert && (
        <div className="mt-3 rounded-lg border border-bordure p-4">
          <div className="mb-3 flex gap-2">
            {([[true, "Avoir total"], [false, "Avoir partiel"]] as [boolean, string][]).map(([v, l]) => (
              <button key={l} aria-pressed={total === v} onClick={() => setTotal(v)}
                className={cn("cursor-pointer rounded-full border px-3.5 py-1.5 text-[13px] font-semibold", total === v ? "border-marine bg-marine text-white" : "border-bordure-forte bg-white hover:bg-fond")}>{l}</button>
            ))}
          </div>
          <div className="grid grid-cols-2 gap-3">
            <Field label="Date de l'avoir"><Input type="date" value={date} onChange={(e) => setDate(e.target.value)} /></Field>
            {total
              ? <Field label="Montant TTC"><Input disabled value={formatDH(facture.du)} /></Field>
              : <Field label="Montant hors taxes, en DH" required><Input inputMode="decimal" value={montant} onChange={(e) => setMontant(e.target.value)} /></Field>}
            <Field label="Motif" required className="col-span-2"><Input value={motif} onChange={(e) => setMotif(e.target.value)} /></Field>
          </div>
          <p className="mt-3 text-[12px] text-secondaire">
            {total
              ? "L'avoir total annule la facture. Ses bons de livraison, ou son acompte, redeviennent facturables."
              : ht > 0 ? `Soit ${formatDH(Math.round(ht * (100 + facture.tauxTva)) / 100)} TTC avec la TVA à ${formatNombre(facture.tauxTva)} %.` : "La TVA de la facture s'applique à l'avoir."}
            {" "}Un avoir émis ne se modifie pas.
          </p>
          {erreur && <div role="alert" className="mt-3 rounded-lg bg-rouge-fond px-4 py-3 text-[13px] text-rouge-texte">{erreur}</div>}
          <div className="mt-4 flex justify-end gap-2">
            <Button variant="outline" onClick={() => { setOuvert(false); setErreur(""); }}>Fermer</Button>
            <Button disabled={envoi || !motif.trim() || (!total && ht <= 0)} onClick={emettre}>Émettre l&apos;avoir</Button>
          </div>
        </div>
      )}
    </div>
  );
}

/** Signature par mot de passe : tant qu'un document n'est pas signé, il ne peut être ni imprimé ni envoyé. */
function SignatureDoc({ signePar, signeLe, quoi, url, onSigne }: { signePar: string | null; signeLe: string | null; quoi: string; url: string; onSigne: (f: Facture) => void }) {
  const [motDePasse, setMotDePasse] = useState("");
  const [erreur, setErreur] = useState("");
  const [envoi, setEnvoi] = useState(false);
  if (signePar) return (
    <div className="mt-3 flex flex-wrap items-center justify-between gap-3">
      <span className="text-[12px] text-secondaire">Signé électroniquement par {signePar}{signeLe ? ` le ${new Date(signeLe).toLocaleString("fr-FR", { dateStyle: "short", timeStyle: "short" })}` : ""}</span>
      <a href={`/api${url}/pdf`} target="_blank" rel="noreferrer" className="inline-flex items-center gap-2 rounded-lg border border-bordure-forte px-3.5 py-2 text-[13px] font-semibold hover:bg-fond"><FileDown size={15} />Télécharger en PDF</a>
    </div>
  );
  const signer = async () => {
    setEnvoi(true); setErreur("");
    try { onSigne(await api<Facture>(`${url}/signer`, { method: "POST", body: JSON.stringify({ motDePasse }) })); setMotDePasse(""); }
    catch (e) { setErreur(e instanceof ApiError ? e.message : "Signature impossible."); }
    finally { setEnvoi(false); }
  };
  return (
    <div className="mt-3 rounded-lg border border-bordure bg-fond p-4">
      <div className="text-[13px] font-semibold">Signer {quoi}</div>
      <p className="text-[12px] text-secondaire">Un document se signe avant de pouvoir être imprimé ou envoyé. Votre nom, la date et un QR code de vérification y figureront.</p>
      <div className="mt-2 flex items-end gap-2">
        <Field label="Votre mot de passe" required className="flex-1"><Input type="password" autoComplete="current-password" value={motDePasse} onChange={(e) => setMotDePasse(e.target.value)} onKeyDown={(e) => { if (e.key === "Enter" && motDePasse) signer(); }} /></Field>
        <Button disabled={envoi || !motDePasse} onClick={signer}>Signer</Button>
      </div>
      {erreur && <div role="alert" className="mt-2 rounded-lg bg-rouge-fond px-3 py-2 text-[12px] text-rouge-texte">{erreur}</div>}
    </div>
  );
}

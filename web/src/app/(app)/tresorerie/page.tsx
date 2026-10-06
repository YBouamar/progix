"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { FileDown, Plus, Search } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { cn, formatDate, formatDH, lireNombre } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field, Input, Select } from "@/components/ui/input";
import { Sheet } from "@/components/ui/sheet";

type Kpi = { encaisse: number; depenses: number; solde: number };
type Mouvement = { id: string; date: string; type: string; libelle: string; objet: string; entite: string; mode: string; modeLabel: string; montant: number; factureId: string | null; saisiPar: string; recu: string | null };
type Effet = { id: string; echeance: string; recuLe: string; client: string; facture: string; reference: string | null; banque: string | null; montant: number; entite: string };
type Journal = { mois: string; kpis: Kpi; precedent: Kpi; compteurs: { type: string; label: string; nb: number }[]; mouvements: Mouvement[]; effetsAEchoir: Effet[] };

const iso = (d: Date) => d.toLocaleDateString("fr-CA");
const MOIS = Array.from({ length: 12 }, (_, i) => { const d = new Date(); d.setDate(1); d.setMonth(d.getMonth() - i); return { v: iso(d).slice(0, 7), l: d.toLocaleDateString("fr-FR", { month: "long", year: "numeric" }) }; });
const TYPES: Record<string, [string, "vert" | "orange" | "neutre"]> = { enc: ["Encaissement", "vert"], remb: ["Remboursement", "orange"], dep: ["Dépense de dalle", "orange"], gen: ["Dépense générale", "neutre"] };
const ANNULATION = ["responsable_administratif", "chef_projet", "directeur"];
const signe = (v: number) => `${v > 0 ? "+ " : v < 0 ? "- " : ""}${formatDH(Math.abs(v))}`;

export default function TresoreriePage() {
  const { user } = useAuth();
  const [mois, setMois] = useState(MOIS[0].v);
  const [entite, setEntite] = useState("");
  const [type, setType] = useState("");
  const [mode, setMode] = useState("");
  const [saisie, setSaisie] = useState("");
  const [q, setQ] = useState("");
  const [data, setData] = useState<Journal | null>(null);
  const [erreur, setErreur] = useState("");
  const [depense, setDepense] = useState(false);
  const [encaisser, setEncaisser] = useState(false);
  const [aAnnuler, setAAnnuler] = useState<Mouvement | null>(null);
  const [motif, setMotif] = useState("");

  useEffect(() => { const t = setTimeout(() => setQ(saisie.trim()), 300); return () => clearTimeout(t); }, [saisie]);
  const charger = useCallback(() => {
    const p = new URLSearchParams({ mois });
    if (entite) p.set("entite", entite);
    if (type) p.set("type", type);
    if (mode) p.set("mode", mode);
    if (q) p.set("q", q);
    api<Journal>(`/tresorerie?${p}`).then((r) => { setData(r); setErreur(""); }).catch((e) => setErreur(e instanceof ApiError ? e.message : "Chargement impossible."));
  }, [mois, entite, type, mode, q]);
  useEffect(() => { charger(); }, [charger]);
  const filtres = () => { const p = new URLSearchParams({ mois }); if (entite) p.set("entite", entite); if (type) p.set("type", type); if (mode) p.set("mode", mode); if (q) p.set("q", q); return p.toString(); };

  const annuler = async () => {
    try { await api(`/depenses/${aAnnuler!.id}/annuler`, { method: "POST", body: JSON.stringify({ motif }) }); setAAnnuler(null); charger(); }
    catch (e) { setErreur(e instanceof ApiError ? e.message : "Annulation impossible."); setAAnnuler(null); }
  };

  const k = data?.kpis, p = data?.precedent;
  const ecart = k && p ? k.solde - p.solde : 0;
  const libelleMois = MOIS.find((m) => m.v === mois)?.l ?? mois;
  const cartes: [string, string, string, string][] = k && p ? [
    ["Encaissé", formatDH(k.encaisse), "text-vert-texte", `Paiements reçus en ${libelleMois}, remboursements déduits`],
    ["Dépenses", formatDH(k.depenses), "", "Dépenses de dalle et dépenses générales"],
    ["Solde du mois", signe(k.solde), k.solde < 0 ? "text-rouge-texte" : "", "Encaissé moins dépenses"],
    ["Écart au mois précédent", signe(ecart), ecart < 0 ? "text-rouge-texte" : "text-vert-texte", `Solde du mois précédent : ${signe(p.solde)}`],
  ] : [];
  const onglets = [{ type: "", label: "Tout", nb: (data?.compteurs ?? []).reduce((s, c) => s + c.nb, 0) }, ...(data?.compteurs ?? [])];
  const totalEffets = (data?.effetsAEchoir ?? []).reduce((s, e) => s + e.montant, 0);

  return (
    <div>
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <Select className="w-auto capitalize" aria-label="Mois" value={mois} onChange={(e) => setMois(e.target.value)}>
          {MOIS.map((m) => <option key={m.v} value={m.v}>{m.l}</option>)}
        </Select>
        <Select className="w-auto" aria-label="Entité" value={entite} onChange={(e) => setEntite(e.target.value)}>
          <option value="">Entité : toutes</option><option value="APT">Africa PT / MK4</option><option value="BBR">BECOMAR</option>
        </Select>
        <Button className="ml-auto" onClick={() => setEncaisser(true)}><Plus size={16} />Encaisser un paiement</Button>
        <a href={`/api/tresorerie/export.xlsx?${filtres()}`} className="inline-flex items-center gap-2 rounded-lg border border-bordure-forte bg-white px-3.5 py-2 text-[13px] font-semibold hover:bg-fond"><FileDown size={15} />Excel</a>
        <a href={`/api/tresorerie/export.pdf?${filtres()}`} target="_blank" rel="noreferrer" className="inline-flex items-center gap-2 rounded-lg border border-bordure-forte bg-white px-3.5 py-2 text-[13px] font-semibold hover:bg-fond"><FileDown size={15} />PDF</a>
        <Button variant="outline" onClick={() => setDepense(true)}><Plus size={16} />Dépense générale</Button>
      </div>

      <div className="mb-5 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {cartes.map(([label, v, c, how]) => (
          <div key={label} className="rounded-xl border border-bordure bg-white p-5">
            <div className="text-[13px] text-secondaire">{label}</div>
            <div className={cn("mt-1 font-display text-xl font-bold", c)}>{v}</div>
            <div className="mt-1 text-[12px] text-attenue">{how}</div>
          </div>
        ))}
      </div>

      {data && data.effetsAEchoir.length > 0 && (
        <div className="mb-5 rounded-xl border border-bordure bg-white">
          <div className="flex items-center justify-between gap-3 border-b border-bordure px-5 py-3">
            <div className="font-display font-bold">Effets à échoir</div>
            <div className="text-[13px] text-secondaire">{data.effetsAEchoir.length} effet(s) · <span className="font-display font-bold text-orange-texte">{formatDH(totalEffets)}</span> · comptés à leur échéance</div>
          </div>
          <ul className="divide-y divide-bordure">
            {[...data.effetsAEchoir].sort((a, b) => a.echeance.localeCompare(b.echeance)).map((e) => (
              <li key={e.id} className="flex flex-wrap items-center justify-between gap-3 px-5 py-2.5 text-[13px]">
                <div><span className="font-display font-semibold">Échéance le {formatDate(e.echeance)}</span> · {e.client} · {e.facture}{e.reference ? ` · effet ${e.reference}` : ""}{e.banque ? ` · ${e.banque}` : ""} <span className="text-attenue">· reçu le {formatDate(e.recuLe)} · {e.entite}</span></div>
                <div className="font-display font-bold">{formatDH(e.montant)}</div>
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className="mb-4 flex flex-wrap items-center gap-2">
        {onglets.map((o) => (
          <button key={o.type} aria-pressed={type === o.type} onClick={() => setType(o.type)}
            className={cn("flex cursor-pointer items-center gap-2 rounded-full border px-3.5 py-1.5 text-[13px] font-semibold", type === o.type ? "border-marine bg-marine text-white" : "border-bordure-forte bg-white hover:bg-fond")}>
            {o.label}<span className={cn("font-display text-[12px]", type === o.type ? "text-white/80" : "text-attenue")}>{o.nb}</span>
          </button>
        ))}
        <div className="relative ml-auto w-full max-w-xs">
          <Search size={16} className="pointer-events-none absolute left-3 top-3 text-attenue" />
          <Input className="pl-9" placeholder="Client, facture, libellé ou projet" value={saisie} onChange={(e) => setSaisie(e.target.value)} />
        </div>
        <Select className="w-auto" aria-label="Mode" value={mode} onChange={(e) => setMode(e.target.value)}>
          <option value="">Mode : tous</option><option value="virement">Virement</option><option value="cheque">Chèque</option><option value="especes">Espèces</option><option value="effet">Effet</option>
        </Select>
      </div>
      {erreur && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}

      <div className="overflow-x-auto rounded-xl border border-bordure bg-white">
        <table className="w-full text-left">
          <thead className="border-b border-bordure bg-fond text-[12px] uppercase tracking-wide text-secondaire">
            <tr>
              <th className="px-5 py-3 font-semibold">Date</th><th className="px-5 py-3 font-semibold">Type</th><th className="px-5 py-3 font-semibold">Libellé</th>
              <th className="px-5 py-3 font-semibold">Projet</th><th className="px-5 py-3 font-semibold">Mode</th><th className="px-5 py-3 text-right font-semibold">Montant</th><th className="px-5 py-3" />
            </tr>
          </thead>
          <tbody>
            {data?.mouvements.map((m) => (
              <tr key={m.id} className="border-b border-bordure last:border-0">
                <td className="whitespace-nowrap px-5 py-3 font-display">{formatDate(m.date)}</td>
                <td className="px-5 py-3"><Badge ton={TYPES[m.type]?.[1] ?? "neutre"}>{TYPES[m.type]?.[0] ?? m.type}</Badge></td>
                <td className="px-5 py-3"><div className="font-semibold">{m.libelle}</div><div className="text-[12px] text-attenue">{m.recu ? `${m.recu} · signé par` : "Saisi par"} {m.saisiPar}</div></td>
                <td className="whitespace-nowrap px-5 py-3 text-[13px] text-secondaire"><div className="font-display">{m.objet}</div><div className="text-[12px] text-attenue">{m.entite}</div></td>
                <td className="px-5 py-3 text-[13px]">{m.modeLabel}</td>
                <td className={cn("whitespace-nowrap px-5 py-3 text-right font-display font-bold", m.montant > 0 && "text-vert-texte")}>{signe(m.montant)}</td>
                <td className="whitespace-nowrap px-5 py-3 text-right text-[12px]">
                  {m.recu && <a href={`/api/encaissements/${m.id}/recu`} target="_blank" rel="noreferrer" className="font-semibold text-marine hover:underline">Reçu</a>}
                  {(m.type === "gen" || m.type === "dep") && user && ANNULATION.includes(user.role) && <button className="cursor-pointer font-semibold text-rouge-texte hover:underline" onClick={() => { setAAnnuler(m); setMotif(""); }}>Annuler</button>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {data && data.mouvements.length === 0 && (
          <div className="px-6 py-14 text-center"><div className="font-display font-bold">Aucun mouvement</div><div className="text-secondaire">Changez de mois ou de filtre.</div></div>
        )}
      </div>
      {data && <div className="mt-3 text-[13px] text-secondaire">{data.mouvements.length} mouvement(s) en {libelleMois} · un encaissement s&apos;annule depuis sa facture, dans <Link href="/facturation" className="font-semibold text-marine hover:underline">Facturation</Link></div>}

      {encaisser && <Encaisser onClose={() => setEncaisser(false)} onFait={() => { setEncaisser(false); charger(); }} />}
      {depense && <NouvelleDepense entite={entite} onClose={() => setDepense(false)} onFaite={() => { setDepense(false); charger(); }} />}
      {aAnnuler && (
        <Sheet open onOpenChange={(o) => !o && setAAnnuler(null)} title="Annuler la dépense" description={`${aAnnuler.libelle} · ${formatDH(-aAnnuler.montant)}`}>
          <Field label="Motif de l'annulation" required><Input autoFocus value={motif} onChange={(e) => setMotif(e.target.value)} /></Field>
          <div className="mt-4 flex justify-end gap-2"><Button variant="outline" onClick={() => setAAnnuler(null)}>Fermer</Button><Button disabled={!motif.trim()} onClick={annuler}>Confirmer l&apos;annulation</Button></div>
        </Sheet>
      )}
    </div>
  );
}

function NouvelleDepense({ entite, onClose, onFaite }: { entite: string; onClose: () => void; onFaite: () => void }) {
  const [ent, setEnt] = useState(entite || "APT");
  const [date, setDate] = useState(iso(new Date()));
  const [libelle, setLibelle] = useState("");
  const [montant, setMontant] = useState("");
  const [mode, setMode] = useState("virement");
  const [reference, setReference] = useState("");
  const [erreur, setErreur] = useState("");
  const [envoi, setEnvoi] = useState(false);
  const m = lireNombre(montant) ?? 0;
  const enregistrer = async () => {
    setEnvoi(true); setErreur("");
    try { await api("/depenses", { method: "POST", body: JSON.stringify({ entite: ent, dateDepense: date, libelle, montant: m, mode, reference }) }); onFaite(); }
    catch (e) { setErreur(e instanceof ApiError ? e.message : "Enregistrement impossible."); setEnvoi(false); }
  };
  return (
    <Sheet open onOpenChange={(o) => !o && onClose()} title="Nouvelle dépense générale" description="Dépense hors projet : loyer, salaires, véhicules, fournitures.">
      <div className="grid grid-cols-2 gap-3">
        <Field label="Entité" required>
          <Select value={ent} onChange={(e) => setEnt(e.target.value)}><option value="APT">Africa PT / MK4</option><option value="BBR">BECOMAR</option></Select>
        </Field>
        <Field label="Date" required><Input type="date" max={iso(new Date())} value={date} onChange={(e) => setDate(e.target.value)} /></Field>
        <Field label="Libellé" required className="col-span-2"><Input autoFocus value={libelle} onChange={(e) => setLibelle(e.target.value)} /></Field>
        <Field label="Montant, en DH" required><Input inputMode="decimal" value={montant} onChange={(e) => setMontant(e.target.value)} /></Field>
        <Field label="Mode de paiement">
          <Select value={mode} onChange={(e) => setMode(e.target.value)}><option value="virement">Virement</option><option value="cheque">Chèque</option><option value="especes">Espèces</option><option value="effet">Effet</option></Select>
        </Field>
        <Field label="Référence" className="col-span-2"><Input value={reference} onChange={(e) => setReference(e.target.value)} /></Field>
      </div>
      {erreur && <div role="alert" className="mt-3 rounded-lg bg-rouge-fond px-4 py-3 text-[13px] text-rouge-texte">{erreur}</div>}
      <div className="mt-4 flex justify-end gap-2">
        <Button variant="outline" onClick={onClose}>Fermer</Button>
        <Button disabled={envoi || !libelle.trim() || m <= 0} onClick={enregistrer}>Enregistrer la dépense</Button>
      </div>
    </Sheet>
  );
}

type AEncaisser = { id: string; numero: string; entite: string; clientId: string; client: string; projet: string; echeance: string; aCouvrir: number };

function Encaisser({ onClose, onFait }: { onClose: () => void; onFait: () => void }) {
  const [factures, setFactures] = useState<AEncaisser[] | null>(null);
  const [clientId, setClientId] = useState("");
  const [factureId, setFactureId] = useState("");
  const [date, setDate] = useState(iso(new Date()));
  const [mode, setMode] = useState("virement");
  const [montant, setMontant] = useState("");
  const [reference, setReference] = useState("");
  const [banque, setBanque] = useState("");
  const [echeanceEffet, setEcheanceEffet] = useState("");
  const [especes, setEspeces] = useState<{ plafond: number; deja: number; disponible: number } | null>(null);
  const [erreur, setErreur] = useState("");
  const [envoi, setEnvoi] = useState(false);
  const [motDePasse, setMotDePasse] = useState("");

  useEffect(() => { api<AEncaisser[]>("/factures/a-encaisser").then(setFactures).catch(() => setFactures([])); }, []);
  const clients = Array.from(new Map((factures ?? []).map((f) => [f.clientId, f.client])).entries());
  const duClient = (factures ?? []).filter((f) => f.clientId === clientId);
  const facture = duClient.find((f) => f.id === factureId);
  useEffect(() => { setFactureId(duClient.length === 1 ? duClient[0].id : ""); }, [clientId]); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => { setMontant(facture ? String(facture.aCouvrir).replace(".", ",") : ""); }, [factureId]); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => {
    if (mode !== "especes" || !date || !factureId) { setEspeces(null); return; }
    api<{ plafond: number; deja: number; disponible: number }>(`/factures/${factureId}/especes?date=${date}`).then(setEspeces).catch(() => setEspeces(null));
  }, [mode, date, factureId]);

  const m = lireNombre(montant) ?? 0;
  const depasse = mode === "especes" && !!especes && m > especes.disponible;
  const libelleRef = mode === "cheque" ? "Numéro du chèque" : mode === "effet" ? "Numéro de l'effet" : "Référence du virement";
  const enregistrer = async () => {
    setEnvoi(true); setErreur("");
    try {
      await api(`/factures/${factureId}/encaissements`, { method: "POST", body: JSON.stringify({ datePaiement: date, mode, montant: m, reference, banque, echeanceEffet: mode === "effet" && echeanceEffet ? echeanceEffet : null, motDePasse }) });
      onFait();
    } catch (e) { setErreur(e instanceof ApiError ? e.message : "Enregistrement impossible."); setEnvoi(false); }
  };

  return (
    <Sheet open onOpenChange={(o) => !o && onClose()} title="Encaisser un paiement" description="Un règlement porte sur une seule facture.">
      {factures && factures.length === 0 ? <p className="text-secondaire">Aucune facture en attente de règlement.</p> : (
        <div className="grid grid-cols-2 gap-3">
          <Field label="Client" required className="col-span-2">
            <Select value={clientId} onChange={(e) => setClientId(e.target.value)}>
              <option value="">Choisir un client</option>{clients.map(([id, nom]) => <option key={id} value={id}>{nom}</option>)}
            </Select>
          </Field>
          <Field label="Facture" required className="col-span-2">
            <Select value={factureId} disabled={!clientId} onChange={(e) => setFactureId(e.target.value)}>
              <option value="">Choisir une facture</option>
              {duClient.map((f) => <option key={f.id} value={f.id}>{f.numero} ({f.entite}) · {f.projet} · reste dû {formatDH(f.aCouvrir)}</option>)}
            </Select>
          </Field>
          <Field label="Date du paiement"><Input type="date" max={iso(new Date())} value={date} onChange={(e) => setDate(e.target.value)} /></Field>
          <Field label="Mode de paiement">
            <Select value={mode} onChange={(e) => setMode(e.target.value)}><option value="virement">Virement</option><option value="cheque">Chèque</option><option value="especes">Espèces</option><option value="effet">Effet</option></Select>
          </Field>
          <Field label="Montant, en DH" required><Input inputMode="decimal" value={montant} onChange={(e) => setMontant(e.target.value)} /></Field>
          {mode !== "especes" && <Field label={libelleRef} required={mode === "cheque" || mode === "effet"}><Input value={reference} onChange={(e) => setReference(e.target.value)} /></Field>}
          {mode === "effet" && <Field label="Échéance de l'effet" required><Input type="date" value={echeanceEffet} onChange={(e) => setEcheanceEffet(e.target.value)} /></Field>}
          {mode !== "especes" && <Field label="Banque" className={mode === "effet" ? "" : "col-span-2"}><Input value={banque} onChange={(e) => setBanque(e.target.value)} /></Field>}
        </div>
      )}
      {factures && factures.length > 0 && (
        <div className="mt-3">
          <Field label="Votre mot de passe" required><Input type="password" autoComplete="current-password" value={motDePasse} onChange={(e) => setMotDePasse(e.target.value)} /></Field>
          <p className="mt-1 text-[12px] text-attenue">Il signe électroniquement le reçu de paiement, qui porte votre nom, la date et un QR code de vérification.</p>
        </div>
      )}
      {mode === "especes" && especes && (
        <div className={cn("mt-3 rounded-lg px-4 py-3 text-[13px]", depasse ? "bg-rouge-fond text-rouge-texte" : "bg-orange-fond text-orange-texte")}>
          <div className="font-semibold">{depasse ? "Plafond espèces dépassé, encaissement refusé." : `Plafond espèces : ${formatDH(especes.plafond)} par client et par mois.`}</div>
          Ce client a déjà réglé {formatDH(especes.deja)} en espèces ce mois. Reste possible en espèces : {formatDH(especes.disponible)}.{depasse ? " Choisissez un autre mode de paiement ou réduisez le montant." : ""}
        </div>
      )}
      {erreur && <div role="alert" className="mt-3 rounded-lg bg-rouge-fond px-4 py-3 text-[13px] text-rouge-texte">{erreur}</div>}
      <div className="mt-4 flex justify-end gap-2">
        <Button variant="outline" onClick={onClose}>Fermer</Button>
        <Button disabled={envoi || !factureId || m <= 0 || depasse || !motDePasse} onClick={enregistrer}>Enregistrer l&apos;encaissement</Button>
      </div>
    </Sheet>
  );
}

"use client";

import { useCallback, useEffect, useState } from "react";
import { Plus, Search } from "lucide-react";
import { api, ApiError, type Paged } from "@/lib/api";
import { cn, formatDate, formatSurface } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field, Input, Select } from "@/components/ui/input";
import { Sheet } from "@/components/ui/sheet";
import { SignatureDoc } from "@/components/signature-doc";

type Bon = {
  id: string; numero: string; ancienNumero: string | null; dateLivraison: string; projetId: string; projetCode: string; projet: string; entite: string; client: string;
  surface: number; emisPar: string; emisLe: string; signePar: string | null; signeLe: string | null; facture: string | null; dalles: { reference: string; emplacement: string; surface: number }[];
};
type DalleLivrable = { id: string; reference: string; emplacement: string; surface: number; livrable: boolean; etat: string };
type Preparation = { projetId: string; code: string; libelle: string; entite: string; client: string; refus: string | null; dalles: DalleLivrable[] };
type ProjetOption = { id: string; code: string; libelle: string; client: string };

const PAGE = 20;
const ANNEE = new Date().getFullYear();
const ENTITES: Record<string, string> = { APT: "Africa PT / MK4", BBR: "BECOMAR" };

export default function LivraisonsPage() {
  const [saisie, setSaisie] = useState("");
  const [q, setQ] = useState("");
  const [annee, setAnnee] = useState(String(ANNEE));
  const [page, setPage] = useState(1);
  const [data, setData] = useState<Paged<Bon> | null>(null);
  const [erreur, setErreur] = useState("");
  const [creation, setCreation] = useState(false);
  const [ouvert, setOuvert] = useState<Bon | null>(null);

  useEffect(() => { const t = setTimeout(() => { setQ(saisie.trim()); setPage(1); }, 300); return () => clearTimeout(t); }, [saisie]);

  const charger = useCallback(() => {
    const p = new URLSearchParams({ page: String(page), pageSize: String(PAGE) });
    if (q) p.set("q", q);
    if (annee) p.set("annee", annee);
    api<Paged<Bon>>(`/livraisons?${p}`).then((r) => { setData(r); setErreur(""); })
      .catch((e) => setErreur(e instanceof ApiError ? e.message : "Chargement impossible."));
  }, [page, q, annee]);
  useEffect(() => { charger(); }, [charger]);

  const pages = data ? Math.max(1, Math.ceil(data.total / PAGE)) : 1;

  return (
    <div>
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <div className="relative w-full max-w-xs">
          <Search size={16} className="pointer-events-none absolute left-3 top-3 text-attenue" />
          <Input className="pl-9" placeholder="Numéro, projet" value={saisie} onChange={(e) => setSaisie(e.target.value)} />
        </div>
        <Select className="w-auto" aria-label="Période" value={annee} onChange={(e) => { setAnnee(e.target.value); setPage(1); }}>
          <option value={ANNEE}>Période : {ANNEE}</option><option value={ANNEE - 1}>{ANNEE - 1}</option><option value="">Toutes</option>
        </Select>
        <Button className="ml-auto" onClick={() => setCreation(true)}><Plus size={16} />Nouveau bon de livraison</Button>
      </div>
      {erreur && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}

      <div className="overflow-x-auto rounded-xl border border-bordure bg-white">
        <table className="w-full text-left">
          <thead className="border-b border-bordure bg-fond text-[12px] uppercase tracking-wide text-secondaire">
            <tr>
              <th className="px-5 py-3 font-semibold">Bon de livraison</th>
              <th className="px-5 py-3 font-semibold">Date</th>
              <th className="px-5 py-3 font-semibold">Projet et client</th>
              <th className="px-5 py-3 font-semibold">Dalles livrées</th>
              <th className="px-5 py-3 text-right font-semibold">Surface</th>
              <th className="px-5 py-3 font-semibold">Facturation</th>
            </tr>
          </thead>
          <tbody>
            {data?.items.map((b) => (
              <tr key={b.id} onClick={() => setOuvert(b)} className="cursor-pointer border-b border-bordure last:border-0 hover:bg-fond">
                <td className="whitespace-nowrap px-5 py-3"><div className="font-display font-bold text-marine">{b.numero}</div><div className="text-[12px] text-attenue">{b.entite}</div></td>
                <td className="whitespace-nowrap px-5 py-3 font-display text-secondaire">{formatDate(b.dateLivraison)}</td>
                <td className="px-5 py-3"><div className="font-semibold">{b.projet}</div><div className="text-[13px] text-secondaire">{b.client}</div></td>
                <td className="px-5 py-3 font-display text-[13px]">{b.dalles.length === 1 ? b.dalles[0].reference : `${b.dalles.length} dalles : ${b.dalles.map((d) => d.reference.split("-").pop()).join(", ")}`}</td>
                <td className="whitespace-nowrap px-5 py-3 text-right font-display">{formatSurface(b.surface)}</td>
                <td className="px-5 py-3"><Badge ton={b.facture ? "vert" : "neutre"}>{b.facture ? `Facturé · ${b.facture}` : "Non facturé"}</Badge>{!b.signePar && <div className="mt-1 text-[12px] text-orange-texte">À signer</div>}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {data && data.items.length === 0 && (
          <div className="px-6 py-14 text-center">
            <div className="font-display font-bold">Aucun bon de livraison</div>
            <div className="text-secondaire">Modifiez la recherche ou la période.</div>
          </div>
        )}
      </div>
      {data && (
        <div className="mt-3 flex items-center justify-between gap-3 text-[13px] text-secondaire">
          <span>{data.total} bon(s) de livraison · un bon de livraison émis ne se modifie pas et ne se supprime pas</span>
          {data.total > PAGE && (
            <span className="flex items-center gap-3">Page {page} sur {pages}
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>Précédent</Button>
              <Button variant="outline" size="sm" disabled={page >= pages} onClick={() => setPage(page + 1)}>Suivant</Button>
            </span>
          )}
        </div>
      )}

      {creation && <Creation onClose={() => setCreation(false)} onEmis={(b) => { setCreation(false); charger(); setOuvert(b); }} />}
      {ouvert && (
        <Sheet open onOpenChange={(o) => !o && setOuvert(null)} title={ouvert.numero} description={`Émis le ${formatDate(ouvert.emisLe)} par ${ouvert.emisPar}${ouvert.ancienNumero ? ` · ancien numéro ${ouvert.ancienNumero}` : ""}`}>
          <Apercu entite={ouvert.entite} numero={ouvert.numero} date={ouvert.dateLivraison} client={ouvert.client} projet={`${ouvert.projetCode} · ${ouvert.projet}`} dalles={ouvert.dalles} />
          <SignatureDoc<{ signePar: string; signeLe: string }> signePar={ouvert.signePar} signeLe={ouvert.signeLe} quoi="le bon de livraison" url={`/livraisons/${ouvert.id}`}
            onSigne={(r) => { setOuvert({ ...ouvert, signePar: r.signePar, signeLe: r.signeLe }); charger(); }} />
        </Sheet>
      )}
    </div>
  );
}

function Apercu({ entite, numero, date, client, projet, dalles }: {
  entite: string; numero: string; date: string; client: string; projet: string; dalles: { reference: string; emplacement: string; surface: number }[];
}) {
  return (
    <div className="rounded-lg border border-bordure p-5">
      <div className="flex items-start justify-between gap-4">
        <div className="font-display text-lg font-bold text-marine">{ENTITES[entite] ?? entite}</div>
        <div className="text-right">
          <div className="font-display font-bold">Bon de livraison</div>
          <div className="font-display text-[13px] text-secondaire">{numero} · {formatDate(date)}</div>
        </div>
      </div>
      <dl className="mt-4 space-y-1 text-[13px]">
        <div><dt className="inline text-secondaire">Client : </dt><dd className="inline font-semibold">{client}</dd></div>
        <div><dt className="inline text-secondaire">Projet : </dt><dd className="inline font-semibold">{projet}</dd></div>
      </dl>
      <table className="mt-4 w-full text-left text-[13px]">
        <thead className="border-b border-bordure-forte text-[11px] uppercase tracking-wide text-secondaire">
          <tr><th className="py-1.5 font-semibold">Dalle</th><th className="py-1.5 text-right font-semibold">Surface</th></tr>
        </thead>
        <tbody>
          {dalles.map((d) => (
            <tr key={d.reference} className="border-b border-bordure">
              <td className="py-1.5"><span className="font-display font-bold">{d.reference}</span> · {d.emplacement}</td>
              <td className="py-1.5 text-right font-display">{formatSurface(d.surface)}</td>
            </tr>
          ))}
          <tr><td className="py-1.5 font-bold">Total</td><td className="py-1.5 text-right font-display font-bold">{formatSurface(dalles.reduce((s, d) => s + d.surface, 0))}</td></tr>
        </tbody>
      </table>
      {dalles.length === 0 && <div className="mt-3 text-[13px] text-attenue">Sélectionnez au moins une dalle.</div>}
    </div>
  );
}

function Creation({ onClose, onEmis }: { onClose: () => void; onEmis: (b: Bon) => void }) {
  const [recherche, setRecherche] = useState("");
  const [options, setOptions] = useState<ProjetOption[]>([]);
  const [prep, setPrep] = useState<Preparation | null>(null);
  const [date, setDate] = useState(new Date().toLocaleDateString("fr-CA"));
  const [choix, setChoix] = useState<string[]>([]);
  const [erreur, setErreur] = useState("");
  const [envoi, setEnvoi] = useState(false);

  useEffect(() => {
    if (prep) return;
    const t = setTimeout(() => {
      api<{ items: ProjetOption[] }>(`/projets?pageSize=8${recherche.trim() ? `&q=${encodeURIComponent(recherche.trim())}` : ""}`)
        .then((r) => setOptions(r.items)).catch(() => setOptions([]));
    }, 200);
    return () => clearTimeout(t);
  }, [recherche, prep]);

  const choisir = (p: ProjetOption) => {
    api<Preparation>(`/livraisons/dalles?projetId=${p.id}`).then((r) => { setPrep(r); setChoix([]); setErreur(""); }).catch(() => setErreur("Chargement impossible."));
  };

  async function emettre() {
    if (!prep) return;
    setEnvoi(true); setErreur("");
    try {
      onEmis(await api<Bon>("/livraisons", { method: "POST", body: JSON.stringify({ projetId: prep.projetId, dateLivraison: date, dalleIds: choix }) }));
    } catch (e) {
      setErreur(e instanceof ApiError ? e.message : "Émission impossible."); setEnvoi(false);
    }
  }

  const selection = prep?.dalles.filter((d) => choix.includes(d.id)) ?? [];

  return (
    <Sheet open onOpenChange={(o) => !o && onClose()} title="Nouveau bon de livraison" description="1. Projet · 2. Dalles · 3. Aperçu avant émission"
      footer={
        <>
          <Button variant="outline" onClick={onClose}>Annuler</Button>
          <Button disabled={envoi || selection.length === 0 || !date} onClick={emettre}>Émettre le bon de livraison</Button>
        </>
      }>
      {erreur && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}

      {!prep ? (
        <Field label="Projet">
          <Input autoFocus placeholder="Rechercher par code, libellé ou client" value={recherche} onChange={(e) => setRecherche(e.target.value)} />
          <ul className="mt-2 divide-y divide-bordure rounded-lg border border-bordure">
            {options.map((p) => (
              <li key={p.id}>
                <button type="button" onClick={() => choisir(p)} className="flex w-full cursor-pointer items-center gap-3 px-3 py-2 text-left hover:bg-fond">
                  <span className="font-display font-bold text-marine">{p.code}</span><span className="flex-1 truncate">{p.libelle}</span><span className="text-[12px] text-secondaire">{p.client}</span>
                </button>
              </li>
            ))}
            {options.length === 0 && <li className="px-3 py-2 text-[13px] text-attenue">Aucun projet.</li>}
          </ul>
        </Field>
      ) : (
        <div className="space-y-5">
          <div className="flex items-center justify-between gap-3 rounded-lg bg-fond px-4 py-3">
            <div><span className="font-display font-bold text-marine">{prep.code}</span> · {prep.libelle}<div className="text-[13px] text-secondaire">{prep.client}</div></div>
            <Button variant="ghost" size="sm" onClick={() => setPrep(null)}>Changer</Button>
          </div>
          {prep.refus && <div role="alert" className="rounded-lg bg-orange-fond px-4 py-3 text-orange-texte">{prep.refus}</div>}
          <Field label="Date de livraison" className="max-w-[200px]"><Input type="date" value={date} onChange={(e) => setDate(e.target.value)} /></Field>
          <div>
            <div className="mb-1.5 text-[13px] font-semibold text-secondaire">Dalles</div>
            <ul className="divide-y divide-bordure rounded-lg border border-bordure">
              {prep.dalles.map((d) => {
                const coche = choix.includes(d.id), possible = d.livrable && !prep.refus;
                return (
                  <li key={d.id}>
                    <label className={cn("flex items-center gap-3 px-3 py-2.5", possible ? "cursor-pointer" : "bg-fond text-secondaire", coche && "bg-marine/5")}>
                      <input type="checkbox" className="h-4 w-4 accent-marine" disabled={!possible} checked={coche}
                        onChange={(e) => setChoix(e.target.checked ? [...choix, d.id] : choix.filter((x) => x !== d.id))} />
                      <span className="font-display font-bold">{d.reference}</span>
                      <span className="flex-1 truncate text-[13px]">{d.emplacement}</span>
                      <span className="font-display text-[13px]">{formatSurface(d.surface)}</span>
                      <span className={cn("w-44 text-right text-[12px]", d.livrable ? "font-semibold text-vert-texte" : "text-secondaire")}>{d.etat}</span>
                    </label>
                  </li>
                );
              })}
              {prep.dalles.length === 0 && <li className="px-3 py-2 text-[13px] text-attenue">Ce projet n&apos;a aucune dalle.</li>}
            </ul>
            <p className="mt-1.5 text-[12px] text-attenue">
              Une dalle devient livrable dès que sa commande usine est préparée (étape 4). Une dalle déjà livrée ne peut pas figurer sur un second bon de livraison.
            </p>
          </div>
          <div>
            <div className="mb-1.5 text-[13px] font-semibold text-secondaire">Aperçu avant émission</div>
            <Apercu entite={prep.entite} numero="Numéro attribué à l'émission" date={date} client={prep.client} projet={`${prep.code} · ${prep.libelle}`} dalles={selection} />
            <p className="mt-1.5 text-[12px] text-attenue">Une fois émis, le bon de livraison ne peut plus être modifié ni supprimé.</p>
          </div>
        </div>
      )}
    </Sheet>
  );
}

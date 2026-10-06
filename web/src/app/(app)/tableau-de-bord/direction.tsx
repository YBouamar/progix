"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { api } from "@/lib/api";
import { cn, formatDate, formatDH } from "@/lib/utils";
import { Field, Input, Select } from "@/components/ui/input";
import { libellePeriode, periodes, type DevisTdb, type Finances, type ProjetsTdb } from "./donnees";

const POINT: Record<string, string> = { cree: "bg-attenue", etude: "bg-violet-texte", realisation: "bg-orange-texte", termine: "bg-vert-texte" };
type Stock = { entite: string; articles: { article: string; bas: boolean }[] };

export function TableauDirection() {
  const liste = useMemo(periodes, []);
  const [choix, setChoix] = useState("mois");
  const [debut, setDebut] = useState(liste[0].debut);
  const [fin, setFin] = useState(liste[0].fin);
  const [entite, setEntite] = useState("");
  const [f, setF] = useState<Finances | null>(null);
  const [p, setP] = useState<ProjetsTdb | null>(null);
  const [d, setD] = useState<DevisTdb | null>(null);
  const [stock, setStock] = useState<Stock[]>([]);

  useEffect(() => { const x = liste.find((l) => l.id === choix); if (x) { setDebut(x.debut); setFin(x.fin); } }, [choix, liste]);
  useEffect(() => {
    if (!debut || !fin || fin < debut) return;
    const q = new URLSearchParams({ debut, fin }); if (entite) q.set("entite", entite);
    api<Finances>(`/tableau-de-bord/finances?${q}`).then(setF).catch(() => setF(null));
  }, [debut, fin, entite]);
  useEffect(() => {
    const q = entite ? `?entite=${entite}` : "";
    api<ProjetsTdb>(`/tableau-de-bord/projets${q}`).then(setP).catch(() => setP(null));
    api<DevisTdb>(`/devis/tableau-de-bord${q}`).then(setD).catch(() => setD(null));
    Promise.all((entite ? [entite] : ["APT", "BBR"]).map((e) => api<Stock>(`/logistique/stock?entite=${e}`).catch(() => null)))
      .then((r) => setStock(r.filter((x): x is Stock => !!x)));
  }, [entite]);

  const cur = f?.periode, prev = f?.precedente;
  const cartes = cur && prev ? ([
    ["Chiffre d'affaires", cur.chiffreAffaires, prev.chiffreAffaires, "Factures émises sur la période, TTC après avoirs", true],
    ["Encaissé", cur.encaisse, prev.encaisse, "Paiements reçus sur la période, remboursements déduits", true],
    ["Dépenses", cur.depenses, prev.depenses, "Dépenses de dalle et dépenses générales", false],
    ["Marge", cur.marge, prev.marge, cur.chiffreAffaires > 0 ? `Chiffre d'affaires moins dépenses, soit ${(cur.marge / cur.chiffreAffaires * 100).toLocaleString("fr-FR", { maximumFractionDigits: 1 })} %` : "Chiffre d'affaires moins dépenses", true],
  ] as [string, number, number, string, boolean][]) : [];
  const max = Math.max(1, ...(p?.statuts ?? []).map((s) => s.nb));
  const bas = stock.flatMap((s) => s.articles.filter((a) => a.bas).map((a) => `${a.article.split(",")[0]} (${s.entite})`));
  const alertes: { rouge: boolean; texte: string; href: string; cta: string }[] = [];
  if (p && p.validationsEnAttente > 0) alertes.push({ rouge: false, texte: `${p.validationsEnAttente} étape(s) attendent une validation technique ou administrative`, href: "/projets", cta: "Voir" });
  if (f && f.retards.nb > 0) alertes.push({ rouge: true, texte: `${f.retards.nb} facture(s) en retard de paiement, ${formatDH(f.retards.total)}`, href: "/facturation", cta: "Voir" });
  for (const x of d?.expirent ?? []) alertes.push({ rouge: true, texte: `Devis ${x.numero} expire le ${formatDate(x.echeance)}`, href: `/devis/${x.id}`, cta: "Ouvrir" });
  if (d && d.aRelancer.length > 0) alertes.push({ rouge: false, texte: `${d.aRelancer.length} devis à relancer, envoyés depuis 7 jours ou plus`, href: "/devis", cta: "Voir" });
  if (bas.length > 0) alertes.push({ rouge: false, texte: `Stock bas : ${bas.slice(0, 3).join(", ")}${bas.length > 3 ? ` et ${bas.length - 3} autre(s)` : ""}`, href: "/logistique", cta: "Voir le stock" });

  return (
    <div>
      <div className="mb-5 flex flex-wrap items-end gap-3">
        <Field label="Période">
          <Select className="w-auto capitalize" value={choix} onChange={(e) => setChoix(e.target.value)}>
            {liste.map((l) => <option key={l.id} value={l.id}>{l.label}</option>)}<option value="perso">Période personnalisée</option>
          </Select>
        </Field>
        {choix === "perso" && <>
          <Field label="Du"><Input type="date" value={debut} onChange={(e) => setDebut(e.target.value)} /></Field>
          <Field label="Au"><Input type="date" value={fin} min={debut} onChange={(e) => setFin(e.target.value)} /></Field>
        </>}
        <Field label="Entité">
          <Select className="w-auto" value={entite} onChange={(e) => setEntite(e.target.value)}><option value="">Toutes les entités</option><option value="APT">APT</option><option value="BBR">BBR</option></Select>
        </Field>
        {prev && <span className="pb-2.5 text-[13px] text-secondaire">Comparaison avec {libellePeriode(prev)}</span>}
      </div>

      <div className="mb-5 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {cartes.map(([label, v, avant, how, hausseBonne]) => {
          // Un pourcentage d'évolution n'a de sens que par rapport à une base positive.
          const delta = avant > 0 ? (v - avant) / avant * 100 : null;
          const bon = delta === null || delta === 0 ? null : (delta > 0) === hausseBonne;
          return (
            <div key={label} className="rounded-xl border border-bordure bg-white p-5">
              <div className="flex items-start justify-between gap-2">
                <div className="text-[13px] text-secondaire">{label}</div>
                {delta !== null && <span className={cn("font-display text-[13px] font-bold", bon === null ? "text-secondaire" : bon ? "text-vert-texte" : "text-orange-texte")}>{delta > 0 ? "+" : ""}{delta.toLocaleString("fr-FR", { maximumFractionDigits: 1 })} %</span>}
              </div>
              <div className={cn("mt-1 font-display text-xl font-bold", v < 0 && "text-rouge-texte")}>{v < 0 ? "- " : ""}{formatDH(Math.abs(v))}</div>
              <div className="mt-1 text-[12px] text-secondaire">{avant < 0 ? "- " : ""}{formatDH(Math.abs(avant))} sur la période précédente</div>
              <div className="mt-1 text-[12px] text-attenue">{how}</div>
            </div>
          );
        })}
      </div>

      <div className="mb-5 grid gap-4 xl:grid-cols-2">
        <div className="rounded-xl border border-bordure bg-white p-5">
          <div className="font-display font-bold">Projets par statut</div>
          <div className="text-[13px] text-secondaire">{p ? `${p.total} projet(s), dont ${p.bloques.length} bloqué(s)` : ""}</div>
          <div className="mt-4 space-y-3">
            {p?.statuts.map((s) => (
              <Link key={s.code} href={`/projets?statut=${s.code}`} className="flex items-center gap-3 text-[13px] hover:opacity-80">
                <span className={cn("h-2.5 w-2.5 shrink-0 rounded-full", POINT[s.code] ?? "bg-attenue")} />
                <span className="w-32 shrink-0">{s.label}</span>
                <span className="h-2.5 flex-1 overflow-hidden rounded-full bg-fond"><span className={cn("block h-full rounded-full", POINT[s.code] ?? "bg-attenue")} style={{ width: `${s.nb / max * 100}%` }} /></span>
                <span className="w-8 text-right font-display font-bold">{s.nb}</span>
              </Link>
            ))}
          </div>
        </div>
        <div className="rounded-xl border border-bordure bg-white p-5">
          <div className="font-display font-bold">Alertes</div>
          {alertes.length === 0 ? <p className="mt-3 text-[13px] text-secondaire">Aucune alerte.</p> : (
            <ul className="mt-3 divide-y divide-bordure">
              {alertes.map((a, i) => (
                <li key={i} className="flex items-center gap-3 py-2.5 text-[13px]">
                  <span className={cn("h-2.5 w-2.5 shrink-0 rounded-full", a.rouge ? "bg-rouge-texte" : "bg-orange-texte")} />
                  <span className="flex-1">{a.texte}</span>
                  <Link href={a.href} className="shrink-0 font-semibold text-marine hover:underline">{a.cta}</Link>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>

      <div className="overflow-x-auto rounded-xl border border-bordure bg-white">
        <div className="flex items-center justify-between gap-3 border-b border-bordure px-5 py-3">
          <div className="font-display font-bold">Projets bloqués</div><div className="text-[13px] text-secondaire">{p?.bloques.length ?? 0} projet(s)</div>
        </div>
        {p && p.bloques.length === 0 ? <div className="px-5 py-8 text-center text-[13px] text-secondaire">Aucun projet bloqué.</div> : (
          <table className="w-full text-left text-[13px]">
            <thead className="border-b border-bordure bg-fond text-[12px] uppercase tracking-wide text-secondaire">
              <tr><th className="px-5 py-2.5 font-semibold">Code</th><th className="px-5 py-2.5 font-semibold">Libellé</th><th className="px-5 py-2.5 font-semibold">Motif du blocage</th><th className="px-5 py-2.5 font-semibold">Bloqué par, le</th></tr>
            </thead>
            <tbody>
              {p?.bloques.map((b) => (
                <tr key={b.id} className="border-b border-bordure last:border-0">
                  <td className="whitespace-nowrap px-5 py-2.5"><Link href={`/projets/${b.id}`} className="font-display font-bold text-marine hover:underline">{b.code}</Link></td>
                  <td className="px-5 py-2.5 font-semibold">{b.libelle}</td><td className="px-5 py-2.5 text-secondaire">{b.motif}</td>
                  <td className="whitespace-nowrap px-5 py-2.5 text-secondaire">{b.par}{b.le ? `, ${formatDate(b.le)}` : ""}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}

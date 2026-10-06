"use client";

import { useAuth } from "@/lib/auth";
import { Fragment, use, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { ArrowLeft, ChevronDown, ChevronUp, Plus, Trash2, X } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { useRouter } from "next/navigation";
import { cn, formatDate, formatDateHeure, formatDH, formatNombre, lireNombre } from "@/lib/utils";
import { Modal } from "@/components/ui/modal";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field, Input, Select } from "@/components/ui/input";
import { SignatureDoc } from "@/components/signature-doc";
import { Documents } from "@/components/documents";
import { ImportDevis, type LigneImport } from "./import";
import { forfaitPour, LABEL_TYPE, TON_ETAT, type DevisDetail } from "../types";

type LigneForm = { id: string; reference: string; emplacement: string; surface: string; ra: string; rp: string; sys: Record<number, { a: string; g: string }> };
type EcheanceForm = { label: string; pct: string; retenue: boolean };
type Form = {
  dateDevis: string; duree: string; commercialId: string; remise: string; motif: string;
  echeances: EcheanceForm[]; contrat: string; lignes: LigneForm[]; systemes: number[];
};

const fr = (n: number) => (n ? String(n).replace(".", ",") : "");
const ent = (n: number, d = 0) => n.toLocaleString("fr-FR", { minimumFractionDigits: d, maximumFractionDigits: d }).replace(/[  ]/g, " ");
const CELL = "h-8 w-full min-w-0 rounded-md border border-bordure-forte bg-white px-2 text-right font-display text-[13px] focus:border-marine focus:outline-none disabled:border-transparent disabled:bg-transparent";

function versForm(d: DevisDetail): Form {
  const systemes = [...new Set(d.lignes.flatMap((l) => l.systemes.map((s) => s.torons)))].sort((a, b) => a - b);
  return {
    dateDevis: d.dateDevis, duree: String(d.dureeValidite), commercialId: d.commercialId ?? "",
    remise: fr(d.remiseM2) || "0", motif: d.remiseMotif ?? "",
    echeances: d.echeances.map((e) => ({ label: e.label, pct: fr(e.pourcentage), retenue: e.retenue })),
    contrat: d.modalitesContrat ?? "", systemes,
    lignes: d.lignes.map((l) => ({
      id: l.id, reference: l.reference, emplacement: l.emplacement, surface: fr(l.surface), ra: fr(l.ratioActif), rp: fr(l.ratioPassif),
      sys: Object.fromEntries(l.systemes.map((s) => [s.torons, { a: fr(s.ancrages), g: fr(s.gaine) }])),
    })),
  };
}

export default function DevisEditeur({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const [d, setD] = useState<DevisDetail | null>(null);
  const [f, setF] = useState<Form | null>(null);
  const [importOuvert, setImportOuvert] = useState(false);
  const [commerciaux, setCommerciaux] = useState<{ id: string; nom: string }[]>([]);
  const [ouvert, setOuvert] = useState<string | null>(null);
  const [nouveauSys, setNouveauSys] = useState("");
  const [erreurs, setErreurs] = useState<Record<string, string[]>>({});
  const [message, setMessage] = useState<{ ok: boolean; texte: string } | null>(null);
  const [envoi, setEnvoi] = useState(false);
  const [modifie, setModifie] = useState(false);
  const [annulation, setAnnulation] = useState<string | null>(null);
  const router = useRouter();

  useEffect(() => {
    api<DevisDetail>(`/devis/${id}`).then((r) => { setD(r); setF(versForm(r)); })
      .catch((e) => setMessage({ ok: false, texte: e instanceof ApiError && e.status === 404 ? "Devis introuvable." : "Chargement impossible." }));
    api<{ id: string; nom: string }[]>("/devis/commerciaux").then(setCommerciaux).catch(() => undefined);
  }, [id]);

  // Calcul en direct, identique à celui du serveur.
  // Les formules et le détail du calcul du prix sont réservés à la direction et au responsable administratif.
  const { user } = useAuth();
  const voitFormules = ["directeur", "chef_projet", "responsable_administratif"].includes(user?.role ?? "");
  const calcul = useMemo(() => {
    if (!f || !d) return null;
    const lignes = f.lignes.map((l) => ({ l, s: lireNombre(l.surface), ra: lireNombre(l.ra), rp: lireNombre(l.rp) }));
    const surfaceTotale = lignes.reduce((a, x) => a + x.s, 0);
    const forfait = forfaitPour(d.typeConstruction, surfaceTotale);
    let totalHT = 0, actif = 0, passif = 0;
    const rows = lignes.map(({ l, s, ra, rp }) => {
      let sa = 0, sg = 0;
      const detail: [string, string, boolean?][] = [];
      for (const t of f.systemes) {
        const a = lireNombre(l.sys[t]?.a ?? ""), g = lireNombre(l.sys[t]?.g ?? "");
        if (a > 0) { sa += a * 100 * t; detail.push([`Ancrages S${t} : ${ent(a)} × 100 × ${t}`, ent(a * 100 * t)]); }
        if (g > 0) { sg += g * 3 * t; detail.push([`Gaines S${t} : ${ent(g)} m × 3 × ${t}`, ent(g * 3 * t)]); }
      }
      const quotient = s > 0 ? (sa + sg) / s : 0;
      const act = ra * 12 * 1.05;
      const brut = quotient + act + forfait;
      const prix = s > 0 ? Math.floor(brut) + 1 : 0;
      const montant = Math.round(prix * s * 100) / 100;
      totalHT += montant; actif += ra * s; passif += rp * s;
      detail.push([`Somme ÷ surface : ${ent(sa + sg)} ÷ ${ent(s, 2)}`, ent(quotient, 2)]);
      detail.push([`Ratio actif : ${ent(ra, 1)} × 12 × 1,05`, ent(act, 2)]);
      detail.push([`Forfait ${LABEL_TYPE[d.typeConstruction]?.toLowerCase() ?? ""}`, ent(forfait, 2)]);
      detail.push([`ENT(${ent(brut, 2)}) + 1`, `${ent(prix)} DH/m²`, true]);
      detail.push([`Montant HT : ${ent(s, 2)} m² × ${ent(prix)} DH`, formatDH(montant), true]);
      detail.push([`Acier actif : ${ent(ra, 1)} kg/m² × ${ent(s, 2)} m²`, `${ent(ra * s)} kg`]);
      detail.push([`Acier passif : ${ent(rp, 1)} kg/m² × ${ent(s, 2)} m²`, `${ent(rp * s)} kg`]);
      return { id: l.id, prix, montant, detail };
    });
    const remiseTotale = Math.min(totalHT, Math.round(lireNombre(f.remise) * surfaceTotale * 100) / 100);
    const netHT = totalHT - remiseTotale;
    const tva = d.exonereTva ? 0 : Math.round(netHT * 20) / 100;
    const totalPct = f.echeances.reduce((a, e) => a + lireNombre(e.pct), 0);
    return {
      rows, surfaceTotale, forfait, totalHT, remiseTotale, netHT, tva, ttc: netHT + tva, totalPct,
      ratioActif: surfaceTotale > 0 ? actif / surfaceTotale : 0, ratioPassif: surfaceTotale > 0 ? passif / surfaceTotale : 0,
    };
  }, [f, d]);

  if (!d || !f || !calcul) {
    return <div><Retour /><div className="rounded-xl border border-bordure bg-white p-12 text-center text-secondaire">{message?.texte ?? "Chargement..."}</div></div>;
  }

  const lecture = d.etat !== "brouillon";
  const maj = (patch: Partial<Form>) => { setF({ ...f, ...patch }); setModifie(true); setMessage(null); };
  // Report des lignes importées dans le brouillon : ratios et systèmes de chaque dalle reconnue, les autres dalles restent telles quelles.
  const importer = (lignes: LigneImport[]) => {
    const systemes = [...new Set([...f.systemes, ...lignes.flatMap((l) => l.systemes.map((x) => x.torons))])].sort((a, b) => a - b);
    maj({
      systemes,
      lignes: f.lignes.map((l) => {
        const x = lignes.find((i) => i.id === l.id);
        if (!x) return l;
        return { ...l, ra: fr(x.ratioActif ?? 0), rp: x.ratioPassif === null ? l.rp : fr(x.ratioPassif), sys: Object.fromEntries(x.systemes.map((y) => [y.torons, { a: fr(y.ancrages), g: fr(y.gaine) }])) };
      }),
    });
    setImportOuvert(false);
    setMessage({ ok: true, texte: `${lignes.length} dalle(s) importée(s). Vérifiez puis enregistrez le brouillon.` });
  };
  const majLigne = (lid: string, patch: Partial<LigneForm>) => maj({ lignes: f.lignes.map((l) => (l.id === lid ? { ...l, ...patch } : l)) });
  const majSys = (l: LigneForm, t: number, k: "a" | "g", v: string) =>
    majLigne(l.id, { sys: { ...l.sys, [t]: { a: l.sys[t]?.a ?? "", g: l.sys[t]?.g ?? "", [k]: v } } });
  const majEch = (i: number, patch: Partial<EcheanceForm>) => maj({ echeances: f.echeances.map((e, j) => (j === i ? { ...e, ...patch } : e)) });

  const ajouterSysteme = () => {
    const n = parseInt(nouveauSys, 10);
    if (n >= 1 && n <= 99 && !f.systemes.includes(n)) maj({ systemes: [...f.systemes, n].sort((a, b) => a - b) });
    setNouveauSys("");
  };
  const retirerSysteme = (t: number) => maj({
    systemes: f.systemes.filter((x) => x !== t),
    lignes: f.lignes.map((l) => { const sys = { ...l.sys }; delete sys[t]; return { ...l, sys }; }),
  });

  async function transition(chemin: string, corps?: object) {
    setEnvoi(true); setMessage(null);
    try {
      const r = await api<DevisDetail>(`/devis/${id}/${chemin}`, { method: "POST", body: JSON.stringify(corps ?? {}) });
      setAnnulation(null);
      if (r.id !== id) { router.push(`/devis/${r.id}`); return; }
      setD(r); setF(versForm(r)); setModifie(false);
    } catch (e) {
      setAnnulation(null);
      setMessage({ ok: false, texte: e instanceof ApiError ? e.message : "Action impossible." });
    } finally {
      setEnvoi(false);
    }
  }

  async function enregistrer() {
    if (!f) return;
    setEnvoi(true); setErreurs({}); setMessage(null);
    try {
      const corps = JSON.stringify({
        dateDevis: f.dateDevis, dureeValidite: parseInt(f.duree, 10) || 0, commercialId: f.commercialId || null,
        remiseM2: lireNombre(f.remise), remiseMotif: f.motif,
        echeances: f.echeances.map((e) => ({ label: e.label, pourcentage: lireNombre(e.pct), retenue: e.retenue })),
        modalitesContrat: f.contrat,
        lignes: f.lignes.map((l) => ({
          id: l.id, surface: lireNombre(l.surface), ratioActif: lireNombre(l.ra), ratioPassif: lireNombre(l.rp),
          systemes: f.systemes.map((t) => ({ torons: t, ancrages: lireNombre(l.sys[t]?.a ?? ""), gaine: lireNombre(l.sys[t]?.g ?? "") })),
        })),
      });
      const r = await api<DevisDetail>(`/devis/${id}`, { method: "PUT", body: corps });
      setD(r); setF({ ...versForm(r), systemes: f.systemes }); setModifie(false);
      setMessage({ ok: true, texte: "Devis enregistré." });
    } catch (e) {
      if (e instanceof ApiError) {
        setErreurs(e.errors ?? {});
        setMessage({ ok: false, texte: e.errors ? Object.values(e.errors).flat().join(" ") : e.message });
      }
    } finally {
      setEnvoi(false);
    }
  }

  const echeance = f.dateDevis ? (() => { const x = new Date(f.dateDevis); x.setDate(x.getDate() + (parseInt(f.duree, 10) || 0)); return formatDate(x.toISOString()); })() : "";
  const pctOk = Math.abs(calcul.totalPct - 100) < 0.001;
  const aRetenue = f.echeances.some((e) => e.retenue);
  const nbCols = 5 + f.systemes.length * 2 + 3;

  return (
    <div className="pb-24">
      <Retour />
      <div className="rounded-xl border border-bordure bg-white p-6">
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-display text-xl font-bold text-marine">{d.numero}</span>
          <Badge>Version {d.version}</Badge>
          <Badge ton={TON_ETAT[d.etat] ?? "neutre"}>{d.etatLabel}</Badge>
          {d.exonereTva && <Badge ton="orange">Client exonéré de TVA</Badge>}
        </div>
        <div className="mt-1 text-secondaire">
          <Link href={`/projets/${d.projetId}`} className="font-semibold text-texte hover:text-marine">{d.projetCode} · {d.projet}</Link> · {d.client}
        </div>
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <span className="mr-auto text-[13px] text-secondaire">
            {d.info}{d.alerte && <span className="font-semibold text-orange-texte"> · {d.alerte}</span>}
            {d.annuleMotif && <> · Motif : {d.annuleMotif}</>}
            {lecture && " · Lecture seule"}
          </span>
          {d.actions.modifier && <Button size="sm" variant="outline" onClick={() => setImportOuvert(true)}>Importer depuis Excel</Button>}
          {d.actions.envoyer && <Button size="sm" disabled={envoi || modifie} title={modifie ? "Enregistrez d'abord le brouillon" : undefined} onClick={() => transition("envoyer")}>Marquer envoyé</Button>}
          {d.actions.valider && <Button size="sm" disabled={envoi} onClick={() => transition("valider")}>Valider le devis</Button>}
          {d.actions.nouvelleVersion && <Button size="sm" variant="outline" disabled={envoi} onClick={() => transition("nouvelle-version")}>Nouvelle version</Button>}
          {d.actions.annuler && <Button size="sm" variant="outline" disabled={envoi} onClick={() => setAnnulation("")}>Annuler le devis</Button>}
        </div>
        {d.etat !== "brouillon" && (
          <SignatureDoc<DevisDetail> signePar={d.signePar} signeLe={d.signeLe} quoi="le devis" url={`/devis/${d.id}`} peutSigner={d.signable}
            raison="Devis non signé : il ne peut pas encore être imprimé ni envoyé. La signature revient à l'administrateur, au responsable administratif, au chef de projet ou au directeur."
            onSigne={(r) => { setD(r); setF(versForm(r)); }} />
        )}
        {importOuvert && <ImportDevis devisId={d.id} onClose={() => setImportOuvert(false)} onImporter={importer} />}
        <Documents compact key={`plans-${d.etat}`} contexte="plan_principe" cibleId={d.id} lectureSeule={d.etat !== "brouillon"} titre="Plans de principe joints au devis"
          aide="Ils accompagnent le devis remis au client. Dépôt possible tant que le devis est en brouillon." />
        {d.etat === "brouillon" && <p className="mt-3 text-[12px] text-secondaire">Un brouillon ne s&apos;imprime pas : marquez le devis comme envoyé, puis signez-le pour obtenir le PDF.</p>}
        {lecture && message && !message.ok && <div role="alert" className="mt-3 rounded-lg bg-rouge-fond px-4 py-2 text-rouge-texte">{message.texte}</div>}

        <div className="mt-5 grid gap-4 sm:grid-cols-2 lg:grid-cols-5">
          <Field label="Commercial">
            <Select disabled={lecture} value={f.commercialId} onChange={(e) => maj({ commercialId: e.target.value })}>
              <option value="">Non renseigné</option>
              {commerciaux.map((c) => <option key={c.id} value={c.id}>{c.nom}</option>)}
            </Select>
          </Field>
          <Field label="Date du devis"><Input type="date" disabled={lecture} value={f.dateDevis} onChange={(e) => maj({ dateDevis: e.target.value })} /></Field>
          <Field label="Durée de validité (jours)" error={erreurs.dureeValidite?.[0]}>
            <Input inputMode="numeric" disabled={lecture} value={f.duree} onChange={(e) => maj({ duree: e.target.value })} />
          </Field>
          <Field label="Échéance, calculée"><Input disabled value={echeance} /></Field>
          <Field label="Type et forfait"><Input disabled value={`${LABEL_TYPE[d.typeConstruction] ?? ""} · ${calcul.forfait} DH/m²`} /></Field>
        </div>
      </div>

      <div className="mt-5 rounded-xl border border-bordure bg-white">
        <div className="flex flex-wrap items-end justify-between gap-3 px-6 py-4">
          <div>
            <h3 className="text-[15px] font-bold">Calcul du prix par dalle</h3>
            <p className="text-[13px] text-secondaire">Le numéro d&apos;un système est son nombre de torons : ajoutez-en un en saisissant ce nombre. La surface vient de la dalle du projet.</p>
          </div>
          {!lecture && (
            <div className="flex items-center gap-2">
              <Input className="h-9 w-40" inputMode="numeric" placeholder="Nombre de torons" value={nouveauSys}
                onChange={(e) => setNouveauSys(e.target.value)} onKeyDown={(e) => e.key === "Enter" && ajouterSysteme()} />
              <Button size="sm" variant="outline" onClick={ajouterSysteme}><Plus size={14} />Ajouter un système</Button>
            </div>
          )}
        </div>
        <div className="overflow-x-auto border-t border-bordure">
          <table className="w-full text-left text-[13px]">
            <thead className="bg-fond text-[11px] uppercase tracking-wide text-secondaire">
              <tr>
                <th rowSpan={2} className="sticky left-0 z-10 bg-fond px-4 py-2 font-semibold">Dalle</th>
                <th rowSpan={2} className="px-2 py-2 text-right font-semibold">Surface m²</th>
                <th rowSpan={2} className="px-2 py-2 text-right font-semibold">Ratio actif</th>
                <th rowSpan={2} className="px-2 py-2 text-right font-semibold">Ratio passif</th>
                {f.systemes.map((t) => (
                  <th key={t} colSpan={2} className="border-l border-bordure px-2 py-2 text-center font-semibold">
                    <span className="font-display text-texte">S{t}</span> · {t} toron{t > 1 ? "s" : ""}
                    {!lecture && (
                      <button onClick={() => retirerSysteme(t)} aria-label={`Retirer le système S${t}`} className="ml-1 cursor-pointer align-middle text-attenue hover:text-rouge-texte"><X size={13} /></button>
                    )}
                  </th>
                ))}
                <th rowSpan={2} className="border-l border-bordure px-3 py-2 text-right font-semibold">Prix au m²</th>
                <th rowSpan={2} className="px-4 py-2 text-right font-semibold">Montant HT</th>
                <th rowSpan={2} />
              </tr>
              <tr>
                {f.systemes.map((t) => (
                  <Fragment key={t}>
                    <th className="border-l border-bordure px-2 pb-2 text-right font-semibold">Ancr.</th>
                    <th className="px-2 pb-2 text-right font-semibold">Gaine m</th>
                  </Fragment>
                ))}
              </tr>
            </thead>
            <tbody>
              {f.lignes.map((l, i) => {
                const r = calcul.rows[i];
                const estOuvert = ouvert === l.id;
                return (
                  <Fragment key={l.id}>
                    <tr className="border-t border-bordure">
                      <td className="sticky left-0 z-10 whitespace-nowrap bg-white px-4 py-2">
                        <div className="font-display font-bold text-marine">{l.reference}</div>
                        <div className="text-[12px] text-secondaire">{l.emplacement}</div>
                      </td>
                      <td className="w-24 px-1 py-2"><input className={CELL} disabled aria-label={`Surface, dalle ${l.reference}`} value={l.surface} /></td>
                      <td className="w-20 px-1 py-2"><input className={CELL} disabled={lecture} inputMode="decimal" aria-label={`Ratio actif, dalle ${l.reference}`} value={l.ra} onChange={(e) => majLigne(l.id, { ra: e.target.value })} /></td>
                      <td className="w-20 px-1 py-2"><input className={CELL} disabled={lecture} inputMode="decimal" aria-label={`Ratio passif, dalle ${l.reference}`} value={l.rp} onChange={(e) => majLigne(l.id, { rp: e.target.value })} /></td>
                      {f.systemes.map((t) => (
                        <Fragment key={t}>
                          <td className="w-20 border-l border-bordure px-1 py-2"><input className={CELL} disabled={lecture} inputMode="decimal" aria-label={`Ancrages S${t}, dalle ${l.reference}`} value={l.sys[t]?.a ?? ""} onChange={(e) => majSys(l, t, "a", e.target.value)} /></td>
                          <td className="w-24 px-1 py-2"><input className={CELL} disabled={lecture} inputMode="decimal" aria-label={`Gaine S${t}, dalle ${l.reference}`} value={l.sys[t]?.g ?? ""} onChange={(e) => majSys(l, t, "g", e.target.value)} /></td>
                        </Fragment>
                      ))}
                      <td className="whitespace-nowrap border-l border-bordure px-3 py-2 text-right font-display font-bold">{ent(r.prix)} DH</td>
                      <td className="whitespace-nowrap px-4 py-2 text-right font-display font-bold">{formatDH(r.montant)}</td>
                      <td className="pr-3">
                        {voitFormules && <button onClick={() => setOuvert(estOuvert ? null : l.id)} aria-expanded={estOuvert} aria-label={`Détail du calcul, dalle ${l.reference}`}
                          className="cursor-pointer rounded-md p-1.5 text-secondaire hover:bg-neutre-fond">{estOuvert ? <ChevronUp size={16} /> : <ChevronDown size={16} />}</button>}
                      </td>
                    </tr>
                    {estOuvert && voitFormules && (
                      <tr className="bg-fond">
                        <td colSpan={nbCols} className="px-4 py-3">
                          <div className="max-w-xl">
                            <div className="mb-1 text-[12px] font-bold">Détail du calcul · {l.reference}</div>
                            {r.detail.map(([k, v, gras], j) => (
                              <div key={j} className={cn("flex justify-between gap-6 py-0.5", gras && "font-bold")}>
                                <span>{k}</span><span className="font-display">{v}</span>
                              </div>
                            ))}
                          </div>
                        </td>
                      </tr>
                    )}
                  </Fragment>
                );
              })}
            </tbody>
          </table>
        </div>
        {voitFormules && (
          <p className="border-t border-bordure px-6 py-3 text-[12px] text-attenue">
            Prix au m² = ENT( (Σ ancrages × 100 × torons + Σ gaines × 3 × torons) / surface + ratio actif × 12 × 1,05 + forfait ) + 1
          </p>
        )}
      </div>

      <div className="mt-5 grid gap-5 lg:grid-cols-[1fr_380px]">
        <div className="space-y-5">
          <div className="rounded-xl border border-bordure bg-white p-6">
            <h3 className="mb-4 text-[15px] font-bold">Remise finale au m²</h3>
            <div className="grid gap-4 sm:grid-cols-[160px_1fr]">
              <Field label="Remise (DH/m²)" error={erreurs.remiseM2?.[0]}>
                <Input inputMode="decimal" disabled={lecture} value={f.remise} onChange={(e) => maj({ remise: e.target.value })} />
              </Field>
              <Field label="Motif" required={lireNombre(f.remise) > 0} error={erreurs.remiseMotif?.[0]}>
                <Input disabled={lecture} value={f.motif} onChange={(e) => maj({ motif: e.target.value })} />
              </Field>
            </div>
            <p className="mt-2 text-[12px] text-attenue">
              Appliquée sur la surface totale : {ent(lireNombre(f.remise), 2)} DH × {ent(calcul.surfaceTotale, 2)} m² = {formatDH(calcul.remiseTotale)}.
            </p>
          </div>

          <div className="rounded-xl border border-bordure bg-white p-6">
            <div className="mb-4 flex items-center justify-between gap-3">
              <h3 className="text-[15px] font-bold">Modalités de paiement</h3>
              <Badge ton={pctOk ? "vert" : "rouge"}>{ent(calcul.totalPct, calcul.totalPct % 1 ? 2 : 0)} %</Badge>
            </div>
            <div className="space-y-2">
              {f.echeances.map((e, i) => (
                <div key={i} className="flex items-center gap-2">
                  <Input disabled={lecture} value={e.label} placeholder="Libellé de l'échéance" onChange={(ev) => majEch(i, { label: ev.target.value })} />
                  <Input className="w-20 text-right" inputMode="decimal" disabled={lecture} aria-label="Pourcentage" value={e.pct} onChange={(ev) => majEch(i, { pct: ev.target.value })} />
                  <span className="text-secondaire">%</span>
                  {e.retenue && <Badge ton="violet" className="shrink-0">Retenue</Badge>}
                  {!lecture && <Button variant="ghost" size="icon" aria-label="Retirer l'échéance" onClick={() => maj({ echeances: f.echeances.filter((_, j) => j !== i) })}><Trash2 size={15} /></Button>}
                </div>
              ))}
            </div>
            {!lecture && (
              <div className="mt-3 flex flex-wrap gap-2">
                <Button size="sm" variant="outline" onClick={() => maj({ echeances: [...f.echeances, { label: "", pct: "", retenue: false }] })}><Plus size={14} />Ajouter une échéance</Button>
                {!aRetenue && <Button size="sm" variant="outline" onClick={() => maj({ echeances: [...f.echeances, { label: "Retenue de garantie, payable 6 mois après la livraison", pct: "", retenue: true }] })}><Plus size={14} />Ajouter une retenue de garantie</Button>}
              </div>
            )}
            <p className={cn("mt-2 text-[12px]", pctOk ? "text-attenue" : "text-rouge-texte")}>
              {erreurs.echeances?.[0] ?? (pctOk ? "Le total fait 100 %. La retenue de garantie est comprise dans ce total." : "Le total des échéances doit faire 100 % avant l'envoi du devis.")}
            </p>
            <Field label="Modalités du contrat" className="mt-5">
              <textarea rows={4} disabled={lecture} value={f.contrat} onChange={(e) => maj({ contrat: e.target.value })}
                className="w-full rounded-lg border border-bordure-forte bg-white px-3 py-2 text-sm focus:border-marine focus:outline-none disabled:bg-fond" />
            </Field>
          </div>
        </div>

        <div className="h-fit rounded-xl border border-bordure bg-white p-6">
          <h3 className="mb-4 text-[15px] font-bold">Totaux</h3>
          <dl className="space-y-2">
            {([
              ["Surface totale", `${formatNombre(calcul.surfaceTotale)} m²`],
              ["Ratio actif moyen, pondéré", `${ent(calcul.ratioActif, 2)} kg/m²`],
              ["Ratio passif moyen, pondéré", `${ent(calcul.ratioPassif, 2)} kg/m²`],
              ["Total HT", formatDH(calcul.totalHT)],
              ...(calcul.remiseTotale > 0 ? [["Remise", `− ${formatDH(calcul.remiseTotale)}`], ["Net HT", formatDH(calcul.netHT)]] : []),
              [d.exonereTva ? "TVA 0 %, client exonéré" : "TVA 20 %", formatDH(calcul.tva)],
            ] as [string, string][]).map(([k, v]) => (
              <div key={k} className="flex justify-between gap-4">
                <dt className="text-secondaire">{k}</dt><dd className="font-display">{v}</dd>
              </div>
            ))}
            <div className="flex justify-between gap-4 border-t border-bordure pt-3 text-[16px] font-bold">
              <dt>Total TTC</dt><dd className="font-display">{formatDH(calcul.ttc)}</dd>
            </div>
          </dl>
          {d.exonereTva && <p className="mt-3 text-[12px] text-attenue">L&apos;exonération vient de la case « Client exonéré de TVA » de la fiche du client.</p>}
        </div>
      </div>

      <div className="mt-5 rounded-xl border border-bordure bg-white p-6">
        <h3 className="mb-3 text-[15px] font-bold">Historique</h3>
        <ul className="space-y-1.5 text-[13px]">
          {d.historique.map((h, i) => (
            <li key={i}><span className="font-display text-secondaire">{formatDateHeure(h.quand)}</span> · {h.quoi} <span className="text-secondaire">· {h.qui}</span></li>
          ))}
        </ul>
      </div>

      <Modal open={annulation !== null} onOpenChange={(o) => !o && setAnnulation(null)} title="Annuler le devis"
        description="Le devis passe à l'état Annulé et ne pourra plus être modifié."
        footer={
          <>
            <Button variant="outline" onClick={() => setAnnulation(null)}>Retour</Button>
            <Button variant="danger" disabled={envoi || !annulation?.trim()} onClick={() => transition("annuler", { motif: annulation })}>Annuler le devis</Button>
          </>
        }>
        <label className="block">
          <span className="mb-1.5 block text-[13px] font-semibold text-secondaire">Motif<span className="text-rouge-vif"> *</span></span>
          <textarea rows={3} autoFocus value={annulation ?? ""} onChange={(e) => setAnnulation(e.target.value)}
            className="w-full rounded-lg border border-bordure-forte px-3 py-2 text-sm focus:border-marine focus:outline-none" />
        </label>
      </Modal>

      {!lecture && (
        <div className="fixed bottom-0 left-[248px] right-0 z-20 flex items-center justify-end gap-4 border-t border-bordure bg-white px-8 py-3">
          {message && <span role={message.ok ? "status" : "alert"} className={cn("text-[13px]", message.ok ? "text-vert-texte" : "text-rouge-texte")}>{message.texte}</span>}
          {modifie && !message && <span className="text-[13px] text-secondaire">Modifications non enregistrées</span>}
          <span className="font-display font-bold">{formatDH(calcul.ttc)} TTC</span>
          <Button onClick={enregistrer} disabled={envoi || !modifie}>{envoi ? "Enregistrement..." : "Enregistrer le brouillon"}</Button>
        </div>
      )}
    </div>
  );
}

function Retour() {
  return (
    <Link href="/devis" className="mb-4 inline-flex items-center gap-1.5 text-[13px] font-semibold text-secondaire hover:text-marine">
      <ArrowLeft size={15} />Tous les devis
    </Link>
  );
}

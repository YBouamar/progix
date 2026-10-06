"use client";

import { useCallback, useEffect, useState } from "react";
import { ChevronDown, ChevronRight, Plus } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn, formatDate, formatDH, lireNombre } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Field, Input, Select } from "@/components/ui/input";
import { Sheet } from "@/components/ui/sheet";

type Depense = { id: string; date: string; categorie: string | null; libelle: string; montant: number; modeLabel: string; reference: string | null; saisiPar: string; annule: boolean; motifAnnulation: string | null; annulePar: string | null };
type Dalle = { id: string; reference: string; emplacement: string; total: number; depenses: Depense[] };
type Vue = {
  peutSaisir: boolean; peutAnnuler: boolean; total: number; categories: string[]; dalles: Dalle[];
  synthese: { facture: number; encaisse: number; depenses: number; ecart: number } | null;
};
const iso = (d: Date) => d.toLocaleDateString("fr-CA");

/** Dépenses du projet, dalle par dalle, comparées au montant facturé pour les rôles qui le voient. */
export function DepensesProjet({ projetId }: { projetId: string }) {
  const [vue, setVue] = useState<Vue | null>(null);
  const [ouvertes, setOuvertes] = useState<string[]>([]);
  const [saisie, setSaisie] = useState<string | null>(null);
  const [aAnnuler, setAAnnuler] = useState<string | null>(null);
  const [motif, setMotif] = useState("");
  const [erreur, setErreur] = useState("");

  const charger = useCallback(() => { api<Vue>(`/projets/${projetId}/depenses`).then(setVue).catch(() => setVue(null)); }, [projetId]);
  useEffect(() => { charger(); }, [charger]);
  if (!vue) return null;

  const annuler = async () => {
    setErreur("");
    try { await api(`/depenses/${aAnnuler}/annuler`, { method: "POST", body: JSON.stringify({ motif }) }); setAAnnuler(null); charger(); }
    catch (e) { setErreur(e instanceof ApiError ? e.message : "Annulation impossible."); }
  };
  const s = vue.synthese;

  return (
    <section className="mt-6 rounded-xl border border-bordure bg-white">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-bordure px-6 py-4">
        <div>
          <h2 className="font-display text-[15px] font-bold">Dépenses</h2>
          <div className="text-[13px] text-secondaire">Chaque dépense se rattache à une dalle précise · total {formatDH(vue.total)}</div>
        </div>
        {vue.peutSaisir && vue.dalles.length > 0 && <Button variant="outline" onClick={() => setSaisie("")}><Plus size={16} />Ajouter une dépense</Button>}
      </div>

      {s && (
        <div className="grid gap-3 border-b border-bordure px-6 py-4 sm:grid-cols-2 xl:grid-cols-4">
          {([["Facturé, TTC après avoirs", formatDH(s.facture), ""], ["Encaissé", formatDH(s.encaisse), "text-vert-texte"], ["Dépenses", formatDH(s.depenses), ""],
            ["Facturé moins dépenses", `${s.ecart < 0 ? "- " : ""}${formatDH(Math.abs(s.ecart))}`, s.ecart < 0 ? "text-rouge-texte" : "text-vert-texte"]] as [string, string, string][]).map(([l, v, c]) => (
            <div key={l} className="rounded-lg border border-bordure px-4 py-3"><div className="text-[13px] text-secondaire">{l}</div><div className={cn("font-display text-lg font-bold", c)}>{v}</div></div>
          ))}
        </div>
      )}

      {vue.dalles.length === 0 ? <div className="px-6 py-8 text-center text-secondaire">Ajoutez d&apos;abord une dalle au projet.</div> : (
        <ul className="divide-y divide-bordure">
          {vue.dalles.map((d) => {
            const ouvert = ouvertes.includes(d.id);
            return (
              <li key={d.id}>
                <button className="flex w-full cursor-pointer items-center gap-3 px-6 py-3 text-left hover:bg-fond" aria-expanded={ouvert}
                  onClick={() => setOuvertes(ouvert ? ouvertes.filter((x) => x !== d.id) : [...ouvertes, d.id])}>
                  {ouvert ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
                  <span className="font-display font-bold text-marine">{d.reference}</span>
                  <span className="text-[13px] text-secondaire">{d.emplacement}</span>
                  <span className="ml-auto text-[13px] text-secondaire">{d.depenses.filter((x) => !x.annule).length} dépense(s)</span>
                  <span className="w-36 text-right font-display font-bold">{formatDH(d.total)}</span>
                </button>
                {ouvert && (
                  <div className="bg-fond px-6 pb-4 pt-1">
                    {d.depenses.length === 0 ? <p className="py-2 text-[13px] text-secondaire">Aucune dépense sur cette dalle.</p> : (
                      <ul className="divide-y divide-bordure rounded-lg border border-bordure bg-white">
                        {d.depenses.map((x) => (
                          <li key={x.id} className="px-4 py-2.5 text-[13px]">
                            <div className="flex items-center justify-between gap-3">
                              <div className={cn(x.annule && "text-attenue line-through")}>
                                <span className="font-display">{formatDate(x.date)}</span> · <span className="font-semibold">{x.categorie}</span>{x.libelle ? ` · ${x.libelle}` : ""} · {x.modeLabel}{x.reference ? ` ${x.reference}` : ""}
                              </div>
                              <div className="flex items-center gap-3">
                                <span className={cn("whitespace-nowrap font-display font-bold", x.annule && "text-attenue line-through")}>{formatDH(x.montant)}</span>
                                {!x.annule && vue.peutAnnuler && <button className="cursor-pointer text-[12px] font-semibold text-rouge-texte hover:underline" onClick={() => { setAAnnuler(x.id); setMotif(""); setErreur(""); }}>Annuler</button>}
                              </div>
                            </div>
                            <div className="text-[12px] text-attenue">Saisie par {x.saisiPar}{x.annule ? ` · annulée par ${x.annulePar} : ${x.motifAnnulation}` : ""}</div>
                            {aAnnuler === x.id && (
                              <div className="mt-2">
                                <div className="flex items-end gap-2">
                                  <Field label="Motif de l'annulation" required className="flex-1"><Input autoFocus value={motif} onChange={(e) => setMotif(e.target.value)} /></Field>
                                  <Button variant="outline" onClick={() => setAAnnuler(null)}>Fermer</Button>
                                  <Button disabled={!motif.trim()} onClick={annuler}>Confirmer</Button>
                                </div>
                                {erreur && <div role="alert" className="mt-2 rounded-lg bg-rouge-fond px-3 py-2 text-[12px] text-rouge-texte">{erreur}</div>}
                              </div>
                            )}
                          </li>
                        ))}
                      </ul>
                    )}
                    {vue.peutSaisir && <Button className="mt-3" variant="outline" size="sm" onClick={() => setSaisie(d.id)}><Plus size={14} />Dépense sur cette dalle</Button>}
                  </div>
                )}
              </li>
            );
          })}
        </ul>
      )}
      {saisie !== null && <Saisie projetId={projetId} vue={vue} dalleInitiale={saisie} onClose={() => setSaisie(null)}
        onFaite={(dalleId) => { setSaisie(null); setOuvertes((o) => (o.includes(dalleId) ? o : [...o, dalleId])); charger(); }} />}
    </section>
  );
}

function Saisie({ projetId, vue, dalleInitiale, onClose, onFaite }: { projetId: string; vue: Vue; dalleInitiale: string; onClose: () => void; onFaite: (dalleId: string) => void }) {
  const [dalleId, setDalleId] = useState(dalleInitiale);
  const [categorie, setCategorie] = useState("");
  const [date, setDate] = useState(iso(new Date()));
  const [montant, setMontant] = useState("");
  const [mode, setMode] = useState("virement");
  const [reference, setReference] = useState("");
  const [libelle, setLibelle] = useState("");
  const [erreur, setErreur] = useState("");
  const [envoi, setEnvoi] = useState(false);
  const m = lireNombre(montant) ?? 0;
  const enregistrer = async () => {
    setEnvoi(true); setErreur("");
    try {
      await api(`/projets/${projetId}/depenses`, { method: "POST", body: JSON.stringify({ dalleId, categorie, dateDepense: date, montant: m, mode, reference, libelle }) });
      onFaite(dalleId);
    } catch (e) { setErreur(e instanceof ApiError ? e.message : "Enregistrement impossible."); setEnvoi(false); }
  };
  return (
    <Sheet open onOpenChange={(o) => !o && onClose()} title="Nouvelle dépense de dalle" description="Une dépense se rattache à une dalle précise et entre dans le journal de trésorerie.">
      <div className="grid grid-cols-2 gap-3">
        <Field label="Dalle" required className="col-span-2">
          <Select value={dalleId} onChange={(e) => setDalleId(e.target.value)}>
            <option value="">Choisir une dalle</option>{vue.dalles.map((d) => <option key={d.id} value={d.id}>{d.reference} · {d.emplacement}</option>)}
          </Select>
        </Field>
        <Field label="Catégorie" required className="col-span-2">
          <Select value={categorie} onChange={(e) => setCategorie(e.target.value)}>
            <option value="">Choisir une catégorie</option>{vue.categories.map((c) => <option key={c} value={c}>{c}</option>)}
          </Select>
        </Field>
        <Field label="Date" required><Input type="date" max={iso(new Date())} value={date} onChange={(e) => setDate(e.target.value)} /></Field>
        <Field label="Montant, en DH" required><Input inputMode="decimal" value={montant} onChange={(e) => setMontant(e.target.value)} /></Field>
        <Field label="Mode de paiement">
          <Select value={mode} onChange={(e) => setMode(e.target.value)}><option value="virement">Virement</option><option value="cheque">Chèque</option><option value="especes">Espèces</option><option value="effet">Effet</option></Select>
        </Field>
        <Field label="Référence"><Input value={reference} onChange={(e) => setReference(e.target.value)} /></Field>
        <Field label="Précision" className="col-span-2"><Input placeholder="Facultatif : fournisseur, détail" value={libelle} onChange={(e) => setLibelle(e.target.value)} /></Field>
      </div>
      {vue.categories.length === 0 && <p className="mt-3 text-[13px] text-rouge-texte">Aucune catégorie n&apos;est proposée : ajoutez-en dans le paramétrage.</p>}
      {erreur && <div role="alert" className="mt-3 rounded-lg bg-rouge-fond px-4 py-3 text-[13px] text-rouge-texte">{erreur}</div>}
      <div className="mt-4 flex justify-end gap-2">
        <Button variant="outline" onClick={onClose}>Fermer</Button>
        <Button disabled={envoi || !dalleId || !categorie || m <= 0} onClick={enregistrer}>Enregistrer la dépense</Button>
      </div>
    </Sheet>
  );
}

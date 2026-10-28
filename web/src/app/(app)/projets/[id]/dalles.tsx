"use client";

import { Fragment, useEffect, useState } from "react";
import { Layers, Plus } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { useAuth, voitMontants } from "@/lib/auth";
import { formatNombre, formatSurface } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field, Input, Select } from "@/components/ui/input";
import { Sheet } from "@/components/ui/sheet";
import { SuiviDalle } from "./suivi";
import { type Adapt, AdaptDalle } from "./adapt";
import { TauxPaiement, type VuePaiements } from "./paiements";

type Dalle = {
  id: string; reference: string; numero: number; version: string; produit: string;
  bloc: string | null; immeuble: string | null; niveau: number; niveauLabel: string; detail: string | null; surface: number;
  dessinateurId: string | null; dessinateur: string | null; etapesFaites: number; etape: string; adapt: Adapt | null;
};
type Personne = { id: string; nom: string };
type Niveau = { valeur: number; label: string; actif: boolean };
type Form = { id: string | null; reference: string; bloc: string; immeuble: string; niveau: string; detail: string; surface: string };
type FormNiveaux = { bloc: string; immeuble: string; niveaux: number[] };

const somme = (l: Dalle[]) => l.reduce((s, d) => s + d.surface, 0);
const pluriel = (n: number) => `${n} dalle${n > 1 ? "s" : ""}`;

export function DallesProjet({ projetId, ecriture, lectureSeule, onSuivi }: { projetId: string; ecriture: boolean; lectureSeule: boolean; onSuivi: () => void }) {
  const [suivi, setSuivi] = useState<string | null>(null);
  const [dessinateurs, setDessinateurs] = useState<Personne[]>([]);
  const [peutDessin, setPeutDessin] = useState(false);
  const [dalles, setDalles] = useState<Dalle[]>([]);
  const [niveaux, setNiveaux] = useState<Niveau[]>([]);
  const [form, setForm] = useState<Form | null>(null);
  const [erreurs, setErreurs] = useState<Record<string, string[]>>({});
  const [message, setMessage] = useState("");
  const [envoi, setEnvoi] = useState(false);
  const [peutAdapt, setPeutAdapt] = useState(false);
  const [adapt, setAdapt] = useState<string | null>(null);
  const [parNiveaux, setParNiveaux] = useState<FormNiveaux | null>(null);
  const base = `/projets/${projetId}/dalles`;
  // Taux de paiement par dalle : seulement pour les rôles qui voient les chiffres financiers.
  const { user } = useAuth();
  const montants = voitMontants(user);
  const [taux, setTaux] = useState<Record<string, number | null> | null>(null);
  useEffect(() => {
    if (!montants) return;
    api<VuePaiements>(`/projets/${projetId}/paiements`).then((v) => setTaux(Object.fromEntries(v.dalles.map((d) => [d.dalleId, d.taux])))).catch(() => setTaux(null));
  }, [projetId, montants]);
  const colonnes = 9 + (ecriture ? 1 : 0) + (montants ? 1 : 0);

  useEffect(() => {
    api<{ dalles: Dalle[]; niveaux: Niveau[]; peutImporterAdapt: boolean }>(base)
      .then((r) => { setDalles(r.dalles); setNiveaux(r.niveaux); setPeutAdapt(r.peutImporterAdapt); }).catch(() => undefined);
    api<{ candidats: { dessinateurs: Personne[] }; droits: { dessinateurs: boolean } }>(`/projets/${projetId}/equipe`)
      .then((r) => { setDessinateurs(r.candidats.dessinateurs); setPeutDessin(r.droits.dessinateurs && !lectureSeule); }).catch(() => undefined);
  }, [base, projetId, lectureSeule]);

  async function affecter(d: Dalle, userId: string) {
    try {
      const n = await api<Dalle>(`${base}/${d.id}/dessinateur`, { method: "PUT", body: JSON.stringify({ userId: userId || null }) });
      setDalles((l) => l.map((x) => (x.id === n.id ? n : x)));
    } catch { /* la liste garde l'ancienne valeur */ }
  }

  // Niveau proposé pour une nouvelle dalle : celui qui suit le niveau de la précédente, sinon le rez-de-chaussée, sinon le premier.
  const niveauSuivant = (precedent?: number) => {
    const proposes = niveaux.filter((n) => n.actif);
    if (precedent !== undefined) {
      const i = niveaux.findIndex((n) => n.valeur === precedent);
      const suivant = niveaux.slice(i + 1).find((n) => n.actif);
      if (i >= 0 && suivant) return suivant.valeur;
    }
    return (proposes.find((n) => n.valeur === 0) ?? proposes[0])?.valeur ?? 0;
  };

  const ouvrir = (d?: Dalle) => {
    const dernier = dalles[dalles.length - 1];
    setErreurs({}); setMessage("");
    setForm(d
      ? { id: d.id, reference: d.reference, bloc: d.bloc ?? "", immeuble: d.immeuble ?? "", niveau: String(d.niveau), detail: d.detail ?? "", surface: String(d.surface).replace(".", ",") }
      // Une nouvelle dalle reprend le bloc et l'immeuble de la précédente, au niveau suivant dans la liste paramétrée.
      : { id: null, reference: "", bloc: dernier?.bloc ?? "", immeuble: dernier?.immeuble ?? "", niveau: String(niveauSuivant(dernier?.niveau)), detail: "", surface: "" });
  };

  async function enregistrer() {
    if (!form) return;
    setEnvoi(true); setErreurs({}); setMessage("");
    try {
      const corps = JSON.stringify({
        bloc: form.bloc, immeuble: form.immeuble, niveau: Number(form.niveau), detail: form.detail,
        surface: Number(form.surface.replace(/\s/g, "").replace(",", ".")) || 0,
      });
      const d = await api<Dalle>(form.id ? `${base}/${form.id}` : base, { method: form.id ? "PUT" : "POST", body: corps });
      setDalles((l) => (form.id ? l.map((x) => (x.id === d.id ? d : x)) : [...l, d]));
      setForm(null);
    } catch (e) {
      if (e instanceof ApiError) { setErreurs(e.errors ?? {}); setMessage(e.errors ? "Corrigez les champs signalés." : e.message); }
    } finally {
      setEnvoi(false);
    }
  }

  // Création par niveaux : une dalle par niveau coché, la surface viendra des fichiers ADAPT.
  async function creerParNiveaux() {
    if (!parNiveaux) return;
    setEnvoi(true); setMessage("");
    try {
      const l = await api<Dalle[]>(`${base}/niveaux`, { method: "POST", body: JSON.stringify(parNiveaux) });
      setDalles((x) => [...x, ...l]);
      setParNiveaux(null);
    } catch (e) {
      if (e instanceof ApiError) setMessage(e.message);
    } finally {
      setEnvoi(false);
    }
  }
  const memeGroupe = (d: Dalle, f: FormNiveaux) => (d.bloc ?? "") === f.bloc.trim() && (d.immeuble ?? "") === f.immeuble.trim();
  const basculer = (v: number) => setParNiveaux((f) => f && { ...f, niveaux: f.niveaux.includes(v) ? f.niveaux.filter((x) => x !== v) : [...f.niveaux, v] });

  // Regroupement par bloc puis immeuble. Un projet simple n'affiche ni groupe ni sous-total.
  const groupes: { bloc: string; immeubles: { immeuble: string; dalles: Dalle[] }[] }[] = [];
  for (const d of dalles) {
    const bloc = d.bloc ?? "", immeuble = d.immeuble ?? "";
    let g = groupes.find((x) => x.bloc === bloc);
    if (!g) groupes.push((g = { bloc, immeubles: [] }));
    let i = g.immeubles.find((x) => x.immeuble === immeuble);
    if (!i) g.immeubles.push((i = { immeuble, dalles: [] }));
    i.dalles.push(d);
  }
  const groupe = dalles.some((d) => d.bloc || d.immeuble);
  const set = (k: keyof Form, v: string) => setForm((f) => (f ? { ...f, [k]: v } : f));

  const SousTotal = ({ label, liste, fond }: { label: string; liste: Dalle[]; fond: string }) => (
    <tr className={fond}>
      <td colSpan={4} className="px-5 py-2.5 text-[13px] font-semibold">{label} · {pluriel(liste.length)}</td>
      <td className="px-5 py-2.5 text-right font-display font-bold">{formatSurface(somme(liste))}</td>
      <td />
      <td />
      <td className="px-5 py-2.5 text-[13px] text-secondaire">{liste.filter((d) => d.etapesFaites >= 11).length} réalisée(s) sur {liste.length}</td>
      {montants && <td />}
      <td />
      {ecriture && <td />}
    </tr>
  );

  return (
    <div className="mt-5 rounded-xl border border-bordure bg-white">
      <div className="flex items-center justify-between gap-3 px-6 py-4">
        <h3 className="text-[15px] font-bold">Dalles <span className="font-display text-attenue">{dalles.length}</span></h3>
        {ecriture && (
          <div className="flex gap-2">
            <Button size="sm" variant="outline" onClick={() => { setMessage(""); setParNiveaux({ bloc: dalles[dalles.length - 1]?.bloc ?? "", immeuble: dalles[dalles.length - 1]?.immeuble ?? "", niveaux: [] }); }}>
              <Layers size={14} />Créer par niveaux
            </Button>
            <Button size="sm" onClick={() => ouvrir()}><Plus size={14} />Ajouter une dalle</Button>
          </div>
        )}
      </div>
      {dalles.length === 0 ? (
        <div className="border-t border-bordure px-6 py-10 text-center text-secondaire">Aucune dalle pour ce projet.</div>
      ) : (
        <div className="overflow-x-auto border-t border-bordure"><table className="w-full text-left">
          <thead className="border-b border-bordure bg-fond text-[12px] uppercase tracking-wide text-secondaire">
            <tr>
              <th className="px-5 py-3 font-semibold">Référence</th>
              <th className="px-5 py-3 font-semibold">Produit</th>
              <th className="px-5 py-3 font-semibold">Niveau</th>
              <th className="px-5 py-3 font-semibold">Détail</th>
              <th className="px-5 py-3 text-right font-semibold">Surface</th>
              <th className="px-5 py-3 font-semibold">ADAPT</th>
              <th className="px-5 py-3 font-semibold">Dessinateur</th>
              <th className="px-5 py-3 font-semibold">Progression</th>
              {montants && <th className="px-5 py-3 font-semibold">Paiement</th>}
              <th />
              {ecriture && <th />}
            </tr>
          </thead>
          <tbody>
            {groupes.map((g) => (
              <Fragment key={g.bloc}>
                {g.immeubles.map((i) => (
                  <Fragment key={i.immeuble}>
                    {groupe && (
                      <tr className="border-b border-bordure bg-neutre-fond/60">
                        <td colSpan={colonnes} className="px-5 py-2 text-[13px] font-bold">
                          {[g.bloc, i.immeuble].filter(Boolean).join(" · ") || "Sans regroupement"}
                        </td>
                      </tr>
                    )}
                    {i.dalles.map((d) => (
                      <tr key={d.id} className="border-b border-bordure">
                        <td className="whitespace-nowrap px-5 py-3 font-display font-bold text-marine">{d.reference}</td>
                        <td className="whitespace-nowrap px-5 py-3 text-secondaire">{d.produit}</td>
                        <td className="whitespace-nowrap px-5 py-3">{d.niveauLabel}</td>
                        <td className="px-5 py-3 text-secondaire">{d.detail}</td>
                        <td className="whitespace-nowrap px-5 py-3 text-right font-display">
                          {d.surface > 0 ? formatSurface(d.surface) : <span className="font-sans text-[13px] text-orange-texte">À renseigner</span>}
                        </td>
                        <td className="px-5 py-2">
                          <button type="button" className="text-left" onClick={() => setAdapt(d.id)}
                            title={d.adapt ? `Ratio actif ${formatNombre(d.adapt.ratioActif)} · ratio passif ${formatNombre(d.adapt.ratioPassif)} kg/m²` : undefined}>
                            {d.adapt
                              ? <Badge ton="vert">{d.adapt.systemes.map((x) => `S${x.torons}`).join(" · ")}</Badge>
                              : <Badge ton={peutAdapt ? "orange" : "neutre"}>{peutAdapt ? "À importer" : "Non importé"}</Badge>}
                          </button>
                        </td>
                        <td className="px-5 py-2">
                          {peutDessin ? (
                            <Select className="h-8 w-44 text-[13px]" aria-label={`Dessinateur de la dalle ${d.reference}`}
                              value={d.dessinateurId ?? ""} onChange={(e) => affecter(d, e.target.value)}>
                              <option value="">Non assigné</option>
                              {dessinateurs.map((u) => <option key={u.id} value={u.id}>{u.nom}</option>)}
                            </Select>
                          ) : <span className={d.dessinateur ? "" : "text-secondaire"}>{d.dessinateur ?? "Non assigné"}</span>}
                        </td>
                        <td className="px-5 py-3">
                          <div className="flex gap-0.5" aria-hidden>
                            {Array.from({ length: 11 }, (_, i) => (
                              <span key={i} className={`h-1.5 w-3 rounded-sm ${i < d.etapesFaites ? "bg-[#1e9e58]" : i === d.etapesFaites && d.etapesFaites > 0 ? "bg-marine" : "bg-bordure"}`} />
                            ))}
                          </div>
                          <div className={`mt-1 whitespace-nowrap text-[12px] ${d.etapesFaites >= 11 ? "font-semibold text-vert-texte" : "text-secondaire"}`}>{d.etape}</div>
                        </td>
                        {montants && <td className="whitespace-nowrap px-5 py-3">{taux ? <TauxPaiement taux={taux[d.id] ?? null} /> : null}</td>}
                        <td className="px-2 py-3"><Button variant="outline" size="sm" onClick={() => setSuivi(d.id)}>Suivi</Button></td>
                        {ecriture && (
                          <td className="px-3 py-3 text-right"><Button variant="ghost" size="sm" onClick={() => ouvrir(d)}>Modifier</Button></td>
                        )}
                      </tr>
                    ))}
                    {groupe && i.immeuble && g.immeubles.length > 1 && (
                      <SousTotal label={`Sous-total ${i.immeuble}`} liste={i.dalles} fond="border-b border-bordure bg-fond" />
                    )}
                  </Fragment>
                ))}
                {groupe && groupes.length > 1 && (
                  <SousTotal label={`Sous-total ${g.bloc || "sans bloc"}`} liste={g.immeubles.flatMap((x) => x.dalles)} fond="border-b border-bordure bg-neutre-fond" />
                )}
              </Fragment>
            ))}
            <SousTotal label="Total projet" liste={dalles} fond="bg-marine/10" />
          </tbody>
        </table></div>
      )}

      {suivi && (
        <SuiviDalle projetId={projetId} dalleId={suivi} onClose={() => setSuivi(null)}
          onChange={(n) => { setDalles((l) => l.map((x) => (x.id === n.id ? (n as Dalle) : x))); onSuivi(); }} />
      )}

      {adapt && dalles.find((d) => d.id === adapt) && (
        <AdaptDalle projetId={projetId} dalle={dalles.find((d) => d.id === adapt)!} peutImporter={peutAdapt}
          onClose={() => setAdapt(null)} onImporte={(n) => setDalles((l) => l.map((x) => (x.id === n.id ? n : x)))} />
      )}

      {parNiveaux && (
        <Sheet open onOpenChange={(o) => !o && setParNiveaux(null)} title="Créer les dalles par niveaux"
          description="Une dalle par niveau coché, dans l'ordre de la liste. La surface se renseigne ensuite, à la main ou par l'import des fichiers ADAPT."
          footer={
            <>
              <Button variant="outline" onClick={() => setParNiveaux(null)}>Annuler</Button>
              <Button onClick={creerParNiveaux} disabled={envoi || parNiveaux.niveaux.length === 0}>
                {envoi ? "Création..." : `Créer ${pluriel(parNiveaux.niveaux.length)}`}
              </Button>
            </>
          }>
          {message && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{message}</div>}
          <div className="grid grid-cols-2 gap-4">
            <Field label="Bloc"><Input value={parNiveaux.bloc} placeholder="Bloc A" onChange={(e) => setParNiveaux({ ...parNiveaux, bloc: e.target.value })} /></Field>
            <Field label="Immeuble"><Input value={parNiveaux.immeuble} placeholder="Immeuble 1" onChange={(e) => setParNiveaux({ ...parNiveaux, immeuble: e.target.value })} /></Field>
          </div>
          <div className="mt-5 flex items-center justify-between">
            <h4 className="text-[14px] font-bold">Niveaux présents dans le projet</h4>
            <span className="text-[13px] text-secondaire">{parNiveaux.niveaux.length} coché(s)</span>
          </div>
          <ul className="mt-2 max-h-[50vh] divide-y divide-bordure overflow-y-auto rounded-lg border border-bordure">
            {niveaux.filter((n) => n.actif).map((n) => {
              const existantes = dalles.filter((d) => d.niveau === n.valeur && memeGroupe(d, parNiveaux)).length;
              return (
                <li key={n.valeur}>
                  <label className="flex cursor-pointer items-center gap-3 px-4 py-2 hover:bg-fond">
                    <input type="checkbox" className="size-4 accent-marine" checked={parNiveaux.niveaux.includes(n.valeur)} onChange={() => basculer(n.valeur)} />
                    <span className="flex-1">{n.label}</span>
                    {existantes > 0 && <span className="text-[12px] text-secondaire">{pluriel(existantes)} déjà</span>}
                  </label>
                </li>
              );
            })}
          </ul>
        </Sheet>
      )}

      {form && (
        <Sheet open onOpenChange={(o) => !o && setForm(null)}
          title={form.id ? "Modifier la dalle" : "Ajouter une dalle"}
          description={form.id ? form.reference : "La référence est attribuée à la création : numéro suivant du projet, version A."}
          footer={
            <>
              <Button variant="outline" onClick={() => setForm(null)}>Annuler</Button>
              <Button onClick={enregistrer} disabled={envoi}>{envoi ? "Enregistrement..." : form.id ? "Enregistrer" : "Ajouter"}</Button>
            </>
          }>
          {message && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{message}</div>}
          <div className="grid grid-cols-2 gap-4">
            <Field label="Produit" className="col-span-2"><Input disabled value="Dalle en Post-Tension" /></Field>
            <Field label="Bloc"><Input value={form.bloc} placeholder="Bloc A" onChange={(e) => set("bloc", e.target.value)} /></Field>
            <Field label="Immeuble"><Input value={form.immeuble} placeholder="Immeuble 1" onChange={(e) => set("immeuble", e.target.value)} /></Field>
            <Field label="Niveau" required error={erreurs.niveau?.[0]}>
              <Select value={form.niveau} onChange={(e) => set("niveau", e.target.value)}>
                {niveaux.map((n) => <option key={n.valeur} value={n.valeur}>{n.label}</option>)}
              </Select>
            </Field>
            <Field label="Surface (m²)" error={erreurs.surface?.[0]}>
              <Input inputMode="decimal" value={form.surface} aria-invalid={!!erreurs.surface} onChange={(e) => set("surface", e.target.value)} />
            </Field>
            <Field label="Détail" className="col-span-2">
              <Input value={form.detail} placeholder="Parking, commerces, logements" onChange={(e) => set("detail", e.target.value)} />
            </Field>
          </div>
          <p className="mt-4 text-[12px] text-attenue">Bloc et immeuble sont facultatifs. S&apos;ils sont renseignés, les dalles sont regroupées avec des sous-totaux. Une surface laissée vide est reprise des fichiers ADAPT à leur import.</p>
        </Sheet>
      )}
    </div>
  );
}

"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { Columns3, List } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn, formatDate } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input, Select } from "@/components/ui/input";

type Personne = { id: string; nom: string };
type Dalle = { id: string; projetId: string; reference: string; projet: string; bloque: boolean; emplacement: string; dessinateurId: string | null; etat: string; etudePlanifiee: string | null; enRetard: boolean };
type Vue = { personnes: Personne[]; dalles: Dalle[] };
type Affichage = "tableau" | "liste";
const ETATS: Record<string, [string, "neutre" | "orange" | "vert"]> = { lancer: ["À lancer", "neutre"], cours: ["En cours", "orange"], terminee: ["Terminée", "vert"] };
const CLE_AFFICHAGE = "progix.repartition.dessinateurs.affichage";
type Filtres = { projet: string; dalle: string; etat: string; du: string; au: string; dessinateur: string };
const SANS_FILTRE: Filtres = { projet: "", dalle: "", etat: "", du: "", au: "", dessinateur: "tous" };

export default function RepartitionDessinateurs() {
  const [v, setV] = useState<Vue | null>(null);
  const [erreur, setErreur] = useState("");
  const [survol, setSurvol] = useState<string | null>(null);
  const [affichage, setAffichage] = useState<Affichage>("tableau");
  const [filtres, setFiltres] = useState<Filtres>(SANS_FILTRE);
  const glisse = useRef<string | null>(null);

  const charger = useCallback(() => api<Vue>("/repartition/dessinateurs").then(setV).catch((e) => setErreur(e instanceof ApiError ? e.message : "Chargement impossible.")), []);
  useEffect(() => { charger(); }, [charger]);

  // Affichage choisi, retenu sur ce poste.
  useEffect(() => {
    try { const a = localStorage.getItem(CLE_AFFICHAGE); if (a === "liste" || a === "tableau") setAffichage(a); } catch { /* stockage indisponible */ }
  }, []);
  const choisir = (a: Affichage) => {
    setAffichage(a);
    try { localStorage.setItem(CLE_AFFICHAGE, a); } catch { /* stockage indisponible */ }
  };

  const affecter = async (d: Dalle, colonne: string) => {
    const cible = colonne === "aucun" ? null : colonne;
    if (cible === d.dessinateurId) return;
    setErreur("");
    try { await api(`/projets/${d.projetId}/dalles/${d.id}/dessinateur`, { method: "PUT", body: JSON.stringify({ userId: cible }) }); }
    catch (e) { setErreur(e instanceof ApiError ? e.message : "Affectation impossible."); }
    await charger();
  };

  const planifier = async (d: Dalle, date: string) => {
    setErreur("");
    try { await api(`/projets/${d.projetId}/dalles/${d.id}/etude`, { method: "PUT", body: JSON.stringify({ date: date || null }) }); }
    catch (e) { setErreur(e instanceof ApiError ? e.message : "Planification impossible."); }
    await charger();
  };

  if (!v) return erreur ? <div role="alert" className="rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div> : null;
  const colonnes = [{ id: "aucun", nom: "Non assignées" }, ...v.personnes];
  const nomDe = (id: string | null) => v.personnes.find((p) => p.id === id)?.nom ?? "Non assignée";
  const filtre = filtres.dessinateur;
  const fixer = (k: keyof Filtres, valeur: string) => setFiltres((x) => ({ ...x, [k]: valeur }));
  const filtresActifs = (Object.keys(SANS_FILTRE) as (keyof Filtres)[]).filter((k) => filtres[k] !== SANS_FILTRE[k]).length;
  const projets = Array.from(new Map(v.dalles.map((d) => [d.projetId, d.projet])).entries()).sort((a, b) => a[1].localeCompare(b[1], "fr"));
  // Filtres communs au tableau et à la liste. Une période d'étude écarte les dalles sans date planifiée.
  const garde = (d: Dalle) =>
    (!filtres.projet || d.projetId === filtres.projet)
    && (!filtres.dalle.trim() || `${d.reference} ${d.emplacement}`.toLowerCase().includes(filtres.dalle.trim().toLowerCase()))
    && (!filtres.etat || d.etat === filtres.etat)
    && (!filtres.du || (!!d.etudePlanifiee && d.etudePlanifiee >= filtres.du))
    && (!filtres.au || (!!d.etudePlanifiee && d.etudePlanifiee <= filtres.au))
    && (filtre === "tous" || (d.dessinateurId ?? "aucun") === filtre);
  const retenues = v.dalles.filter(garde);

  const choixDessinateur = (d: Dalle, className?: string) => (
    <Select className={cn("h-9 text-[13px]", className)} aria-label={`Affecter la dalle ${d.reference}`} value={d.dessinateurId ?? "aucun"} onChange={(e) => affecter(d, e.target.value)}>
      <option value="aucun">Non assignée</option>{v.personnes.map((p) => <option key={p.id} value={p.id}>{p.nom}</option>)}
    </Select>
  );
  const dateEtude = (d: Dalle) => (
    <input type="date" className="h-8 flex-1 rounded-md border border-bordure-forte bg-white px-2 font-display text-[12px] text-texte" aria-label={`Date d'étude planifiée, ${d.reference}`}
      value={d.etudePlanifiee ?? ""} onChange={(e) => planifier(d, e.target.value)} />
  );

  // Liste : dalles non assignées d'abord, puis par dessinateur, chacune par date d'étude planifiée (comme les colonnes du tableau).
  const ordre = new Map(colonnes.map((c, i) => [c.id, i]));
  const lignes = v.dalles
    .filter(garde)
    .map((d, i) => ({ d, i }))
    .sort((a, b) => (ordre.get(a.d.dessinateurId ?? "aucun") ?? 0) - (ordre.get(b.d.dessinateurId ?? "aucun") ?? 0) || a.i - b.i)
    .map((x) => x.d);

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
        <p className="max-w-4xl text-[13px] text-secondaire">
          Une dalle par ligne ou par carte, tant que ses plans ne sont pas validés par le BCT, triée par date d&apos;étude planifiée.{" "}
          {affichage === "tableau" ? "Glissez une carte dans la colonne d'un dessinateur, ou choisissez-le dans la liste de la carte." : "Choisissez le dessinateur dans la liste de chaque ligne."}{" "}
          Une dalle a un seul dessinateur ; il fait ensuite avancer l&apos;étude depuis le suivi de la dalle.
        </p>
        <div role="group" aria-label="Affichage" className="flex shrink-0 gap-1 rounded-lg border border-bordure-forte bg-white p-0.5">
          {([["tableau", "Tableau", Columns3], ["liste", "Liste", List]] as const).map(([id, label, Icone]) => (
            <button key={id} type="button" aria-pressed={affichage === id} onClick={() => choisir(id)}
              className={cn("inline-flex h-8 cursor-pointer items-center gap-1.5 rounded-md px-3 text-[13px] font-semibold", affichage === id ? "bg-marine text-white" : "text-secondaire hover:text-marine")}>
              <Icone size={14} />{label}
            </button>
          ))}
        </div>
      </div>
      {erreur && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}

      <div className="mb-4 flex flex-wrap items-end gap-3 rounded-xl border border-bordure bg-white px-4 py-3">
        <label className="grid gap-1 text-[12px] font-semibold text-secondaire">Projet
          <Select className="h-9 w-56 text-[13px] font-normal text-texte" value={filtres.projet} onChange={(e) => fixer("projet", e.target.value)}>
            <option value="">Tous les projets</option>{projets.map(([id, nom]) => <option key={id} value={id}>{nom}</option>)}
          </Select>
        </label>
        <label className="grid gap-1 text-[12px] font-semibold text-secondaire">Dalle
          <Input className="h-9 w-48 text-[13px] font-normal" placeholder="Référence ou niveau" value={filtres.dalle} onChange={(e) => fixer("dalle", e.target.value)} />
        </label>
        <label className="grid gap-1 text-[12px] font-semibold text-secondaire">État
          <Select className="h-9 w-36 text-[13px] font-normal text-texte" value={filtres.etat} onChange={(e) => fixer("etat", e.target.value)}>
            <option value="">Tous</option>{Object.entries(ETATS).map(([id, [label]]) => <option key={id} value={id}>{label}</option>)}
          </Select>
        </label>
        <fieldset className="grid gap-1">
          <legend className="mb-1 text-[12px] font-semibold text-secondaire">Étude planifiée</legend>
          <div className="flex items-center gap-2 text-[12px] text-secondaire">
            du <Input type="date" className="h-9 w-40 text-[13px]" aria-label="Étude planifiée à partir du" value={filtres.du} max={filtres.au || undefined} onChange={(e) => fixer("du", e.target.value)} />
            au <Input type="date" className="h-9 w-40 text-[13px]" aria-label="Étude planifiée jusqu'au" value={filtres.au} min={filtres.du || undefined} onChange={(e) => fixer("au", e.target.value)} />
          </div>
        </fieldset>
        <label className="grid gap-1 text-[12px] font-semibold text-secondaire">Dessinateur
          <Select className="h-9 w-52 text-[13px] font-normal text-texte" value={filtres.dessinateur} onChange={(e) => fixer("dessinateur", e.target.value)}>
            <option value="tous">Tous les dessinateurs</option>
            {colonnes.map((c) => <option key={c.id} value={c.id}>{c.nom}</option>)}
          </Select>
        </label>
        <div className="ml-auto flex items-center gap-3 pb-1.5 text-[13px] text-secondaire">
          <span><b className="font-display text-texte">{retenues.length}</b> dalle(s) sur {v.dalles.length}</span>
          {filtresActifs > 0 && <Button variant="ghost" size="sm" onClick={() => setFiltres(SANS_FILTRE)}>Effacer les filtres</Button>}
        </div>
      </div>

      {affichage === "tableau" ? (
        <div className="flex gap-4 overflow-x-auto pb-4">
          {colonnes.filter((c) => filtre === "tous" || c.id === filtre).map((c) => {
            const cartes = retenues.filter((d) => (d.dessinateurId ?? "aucun") === c.id);
            return (
              <div key={c.id} onDragOver={(e) => { e.preventDefault(); setSurvol(c.id); }} onDragLeave={() => setSurvol((s) => (s === c.id ? null : s))}
                onDrop={(e) => { e.preventDefault(); setSurvol(null); const d = v.dalles.find((x) => x.id === glisse.current); glisse.current = null; if (d) affecter(d, c.id); }}
                className={cn("w-72 shrink-0 rounded-xl border p-3", survol === c.id ? "border-marine bg-white" : "border-bordure", c.id === "aucun" ? "bg-orange-fond/60" : "bg-fond")}>
                <div className="mb-3 flex items-center justify-between gap-2 px-1">
                  <span className={cn("font-display font-bold", c.id === "aucun" && "text-orange-texte")}>{c.nom}</span>
                  <span className="rounded-full bg-white px-2 py-0.5 font-display text-[12px] font-bold">{cartes.length}</span>
                </div>
                <div className="space-y-2.5">
                  {cartes.map((d) => (
                    <div key={d.id} draggable onDragStart={(e) => { glisse.current = d.id; e.dataTransfer.setData("text/plain", d.reference); }}
                      className="cursor-grab rounded-lg border border-bordure bg-white p-3 active:cursor-grabbing">
                      <div className="flex items-start justify-between gap-2">
                        <Link href={`/projets/${d.projetId}`} className="font-display text-[13px] font-bold text-marine hover:underline">{d.reference}</Link>
                        <Badge ton={ETATS[d.etat]?.[1] ?? "neutre"}>{ETATS[d.etat]?.[0] ?? d.etat}</Badge>
                      </div>
                      <div className="mt-1 text-[13px]">{d.projet}{d.emplacement ? ` · ${d.emplacement}` : ""}</div>
                      {d.bloque && <div className="text-[12px] text-rouge-texte">Projet bloqué</div>}
                      <label className={cn("mt-2 flex items-center gap-2 text-[12px]", d.enRetard ? "font-semibold text-rouge-texte" : "text-secondaire")}>
                        Étude{d.enRetard ? ", en retard" : ""}
                        {dateEtude(d)}
                      </label>
                      {choixDessinateur(d, "mt-2")}
                    </div>
                  ))}
                  {cartes.length === 0 && <div className="rounded-lg border border-dashed border-bordure-forte px-3 py-6 text-center text-[12px] text-attenue">{filtresActifs > 0 ? "Aucune dalle ne répond aux filtres" : c.id === "aucun" ? "Toutes les dalles ont un dessinateur" : "Déposez une dalle ici"}</div>}
                </div>
              </div>
            );
          })}
        </div>
      ) : (
        <div className="rounded-xl border border-bordure bg-white">
          <div className="overflow-x-auto"><table className="w-full text-left text-[13px]">
            <thead className="border-b border-bordure bg-fond text-[12px] uppercase tracking-wide text-secondaire">
              <tr>
                <th className="px-5 py-3 font-semibold">Dalle</th>
                <th className="px-5 py-3 font-semibold">Projet</th>
                <th className="px-5 py-3 font-semibold">État</th>
                <th className="px-5 py-3 font-semibold">Étude planifiée</th>
                <th className="px-5 py-3 font-semibold">Dessinateur</th>
              </tr>
            </thead>
            <tbody>
              {lignes.map((d) => (
                <tr key={d.id} className={cn("border-b border-bordure", !d.dessinateurId && "bg-orange-fond/40")}>
                  <td className="whitespace-nowrap px-5 py-2.5"><Link href={`/projets/${d.projetId}`} className="font-display font-bold text-marine hover:underline">{d.reference}</Link></td>
                  <td className="px-5 py-2.5">
                    {d.projet}{d.emplacement && <div className="text-[12px] text-secondaire">{d.emplacement}</div>}
                    {d.bloque && <div className="text-[12px] text-rouge-texte">Projet bloqué</div>}
                  </td>
                  <td className="px-5 py-2.5"><Badge ton={ETATS[d.etat]?.[1] ?? "neutre"}>{ETATS[d.etat]?.[0] ?? d.etat}</Badge></td>
                  <td className="px-5 py-2">
                    <div className={cn("flex items-center gap-2 text-[12px]", d.enRetard && "font-semibold text-rouge-texte")}>
                      {dateEtude(d)}{d.enRetard && <span title={d.etudePlanifiee ? `Prévue le ${formatDate(d.etudePlanifiee)}` : undefined}>En retard</span>}
                    </div>
                  </td>
                  <td className="px-5 py-2">{choixDessinateur(d, "w-48")}</td>
                </tr>
              ))}
              {lignes.length === 0 && <tr><td colSpan={5} className="px-5 py-8 text-center text-secondaire">{filtresActifs > 0 ? "Aucune dalle ne répond aux filtres." : "Aucune dalle."}</td></tr>}
            </tbody>
          </table></div>
          <div className="px-5 py-2.5 text-[12px] text-secondaire">{filtre !== "tous" && filtre !== "aucun" ? `${lignes.length} dalle(s) pour ${nomDe(filtre)}.` : `${lignes.length} dalle(s).`}</div>
        </div>
      )}
    </div>
  );
}

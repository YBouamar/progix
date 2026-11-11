"use client";

import { useEffect, useRef, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { cn, formatDateHeure, lireNombre } from "@/lib/utils";
import { AttenteFiche } from "@/components/ui/attente";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/input";
import { AccessoiresParam } from "./accessoires";
import { EntitesParam } from "./entites";
import { UtilisateursParam } from "./utilisateurs";
import { HabilitationsParam } from "./habilitations";
import { CoulisParam, DevisParam } from "./defauts";
import { CategoriesDepenseParam } from "./categories-depense";
import { ForfaitsParam } from "./forfaits";
import { TypesConstructionParam } from "./types-construction";

type Params = {
  forceKn: number; sectionMm2: number; moduleMpa: number; pertesPct: number; mmParMetre: number;
  modifiePar: string | null; modifieLe: string | null; modifiable: boolean;
};

const fr = (n: number) => String(n).replace(".", ",");

/** Constantes de l'allongement théorique d'un câble, pour préremplir la fiche de mise en tension. */
function TensionParam() {
  const [p, setP] = useState<Params | null>(null);
  const [f, setF] = useState({ forceKn: "", sectionMm2: "", moduleMpa: "", pertesPct: "" });
  const [message, setMessage] = useState<{ ok: boolean; texte: string } | null>(null);

  const charger = (r: Params) => { setP(r); setF({ forceKn: fr(r.forceKn), sectionMm2: fr(r.sectionMm2), moduleMpa: fr(r.moduleMpa), pertesPct: fr(r.pertesPct) }); };
  useEffect(() => { api<Params>("/parametres/tension").then(charger).catch(() => setMessage({ ok: false, texte: "Chargement impossible." })); }, []);

  if (!p) return message ? <div role="alert" className="rounded-xl border border-bordure bg-white p-6 text-secondaire">{message.texte}</div> : null;

  // Même calcul que le serveur : P × (1 − pertes) / (A × E), en mm par mètre.
  const A = lireNombre(f.sectionMm2), E = lireNombre(f.moduleMpa);
  const mm = A > 0 && E > 0 ? (lireNombre(f.forceKn) * 1000 * (1 - lireNombre(f.pertesPct) / 100)) / (A * E) * 1000 : 0;

  async function enregistrer() {
    setMessage(null);
    try {
      charger(await api<Params>("/parametres/tension", {
        method: "PUT",
        body: JSON.stringify({ forceKn: lireNombre(f.forceKn), sectionMm2: lireNombre(f.sectionMm2), moduleMpa: lireNombre(f.moduleMpa), pertesPct: lireNombre(f.pertesPct) }),
      }));
      setMessage({ ok: true, texte: "Paramètres enregistrés." });
    } catch (e) {
      setMessage({ ok: false, texte: e instanceof ApiError ? e.message : "Enregistrement impossible." });
    }
  }

  const champ = (k: keyof typeof f, label: string) => (
    <Field label={label}><Input inputMode="decimal" disabled={!p.modifiable} value={f[k]} onChange={(e) => { setF({ ...f, [k]: e.target.value }); setMessage(null); }} /></Field>
  );

  return (
    <div className="rounded-xl border border-bordure bg-white p-6">
      <h2 className="text-[15px] font-bold">Mise en tension : allongement théorique</h2>
      <p className="mt-1 text-[13px] text-secondaire">
        Allongement théorique d&apos;un câble = force × longueur / (section × module), diminué des pertes par frottement.
        Ces constantes servent à préremplir la fiche de mise en tension à partir de la longueur de chaque câble.
      </p>
      <div className="mt-5 grid gap-4 sm:grid-cols-2">
        {champ("forceKn", "Force de mise en tension par toron, kN")}
        {champ("sectionMm2", "Section du toron, mm²")}
        {champ("moduleMpa", "Module d'élasticité, MPa")}
        {champ("pertesPct", "Pertes par frottement, %")}
      </div>
      <div className="mt-5 rounded-lg bg-fond px-4 py-3">
        <span className="text-secondaire">Allongement théorique : </span>
        <span className="font-display text-lg font-bold">{mm.toLocaleString("fr-FR", { maximumFractionDigits: 3 })} mm par mètre de câble</span>
        <div className="text-[13px] text-secondaire">Exemple : un câble de 30 m s&apos;allonge de {(mm * 30).toLocaleString("fr-FR", { maximumFractionDigits: 1 })} mm.</div>
      </div>
      <p className="mt-3 text-[12px] text-attenue">
        Valeurs par défaut : toron Y1860S7 de 15,7 mm (150 mm², 195 000 MPa), force de 221 kN soit 0,9 × fp0,1k selon l&apos;EN 1992-1-1,
        pertes de 5 % à ajuster selon vos études. Les fiches déjà saisies ne sont pas recalculées.
      </p>
      {p.modifiePar && <p className="mt-1 text-[12px] text-attenue">Modifié par {p.modifiePar} le {formatDateHeure(p.modifieLe)}.</p>}
      {p.modifiable ? (
        <div className="mt-4 flex items-center gap-3">
          <Button onClick={enregistrer}>Enregistrer</Button>
          {message && <span role={message.ok ? "status" : "alert"} className={cn("text-[13px]", message.ok ? "text-vert-texte" : "text-rouge-texte")}>{message.texte}</span>}
        </div>
      ) : <p className="mt-4 text-[13px] text-secondaire">Consultation seule : ces paramètres sont modifiés par le directeur ou le chef de projet.</p>}
    </div>
  );
}

/** Onglets de la page, dans l'ordre d'affichage. Un onglet dont aucun bloc n'est ouvert à la personne ne s'affiche pas. */
const ONGLETS = [
  { code: "acces", label: "Utilisateurs et accès" },
  { code: "entites", label: "Entités" },
  { code: "projets", label: "Projets et devis" },
  { code: "chantier", label: "Chantier" },
] as const;
type Onglet = (typeof ONGLETS)[number]["code"];

export default function ParametragePage() {
  const [actif, setActif] = useState<Onglet | null>(null);
  const [ouverts, setOuverts] = useState<Onglet[]>([]);
  const [charge, setCharge] = useState(false);
  const zone = useRef<HTMLDivElement>(null);

  // Chaque bloc charge ses données et ne rend rien si la personne n'y a pas accès : les onglets se déduisent des blocs réellement affichés.
  useEffect(() => {
    const el = zone.current;
    if (!el) return;
    const relever = () => {
      const pleins = ONGLETS.map((o) => o.code).filter((code) => el.querySelector(`[data-onglet="${code}"] > div:not(:empty)`));
      setOuverts((avant) => (avant.join() === pleins.join() ? avant : pleins));
    };
    const observateur = new MutationObserver(relever);
    observateur.observe(el, { childList: true, subtree: true });
    relever();
    const t = setTimeout(() => setCharge(true), 1500);
    return () => { observateur.disconnect(); clearTimeout(t); };
  }, []);

  // L'onglet demandé par l'adresse (#chantier) s'ouvre dès que ses blocs sont chargés ; sinon le premier disponible.
  const demande = useRef<string | null>(null);
  useEffect(() => { demande.current = window.location.hash.slice(1) || null; }, []);
  useEffect(() => {
    if (ouverts.length === 0) return;
    const voulu = ouverts.find((o) => o === demande.current);
    if (voulu) demande.current = null;
    setActif((a) => voulu ?? (a && ouverts.includes(a) ? a : ouverts[0]));
  }, [ouverts]);

  const choisir = (code: Onglet) => { demande.current = null; setActif(code); window.history.replaceState(null, "", `#${code}`); };
  const panneau = (code: Onglet) => ({ "data-onglet": code, id: `panneau-${code}`, role: "tabpanel", "aria-labelledby": `onglet-${code}`, hidden: actif !== code });

  return (
    <div>
      {ouverts.length > 1 && (
        <div role="tablist" aria-label="Rubriques du paramétrage" className="mb-5 flex gap-6 overflow-x-auto border-b border-bordure">
          {ONGLETS.filter((o) => ouverts.includes(o.code)).map((o) => (
            <button key={o.code} id={`onglet-${o.code}`} role="tab" aria-selected={actif === o.code} aria-controls={`panneau-${o.code}`} onClick={() => choisir(o.code)}
              className={cn("-mb-px cursor-pointer whitespace-nowrap border-b-2 pb-2.5 text-[14px] font-semibold",
                actif === o.code ? "border-marine text-marine" : "border-transparent text-secondaire hover:text-texte")}>
              {o.label}
            </button>
          ))}
        </div>
      )}
      {ouverts.length === 0 && (charge
        ? <p className="rounded-xl border border-bordure bg-white px-6 py-10 text-center text-secondaire">Aucun réglage n&apos;est ouvert à votre fonction.</p>
        : <AttenteFiche />)}

      {/* Les blocs restent montés d'un onglet à l'autre : une saisie en cours n'est pas perdue. */}
      <div ref={zone}>
        <div {...panneau("acces")} className={cn("space-y-5", actif !== "acces" && "hidden")}>
          <div className="empty:hidden"><UtilisateursParam /></div>
          <div className="empty:hidden"><HabilitationsParam /></div>
        </div>
        <div {...panneau("entites")} className={cn(actif !== "entites" && "hidden")}>
          <div className="empty:hidden"><EntitesParam /></div>
        </div>
        <div {...panneau("projets")} className={cn("grid items-start gap-5 xl:grid-cols-2", actif !== "projets" && "hidden")}>
          <div className="empty:hidden"><TypesConstructionParam /></div>
          <div className="empty:hidden"><DevisParam /></div>
          <div className="empty:hidden"><ForfaitsParam /></div>
        </div>
        <div {...panneau("chantier")} className={cn("grid items-start gap-5 xl:grid-cols-2", actif !== "chantier" && "hidden")}>
          <div className="empty:hidden"><TensionParam /></div>
          <div className="empty:hidden"><CoulisParam /></div>
          <div className="empty:hidden"><AccessoiresParam /></div>
          <div className="empty:hidden"><CategoriesDepenseParam /></div>
        </div>
      </div>
    </div>
  );
}

"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { cn, formatDateHeure, lireNombre } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/input";
import { AccessoiresParam } from "./accessoires";
import { EntitesParam } from "./entites";
import { UtilisateursParam } from "./utilisateurs";
import { CoulisParam, DevisParam } from "./defauts";
import { CategoriesDepenseParam } from "./categories-depense";
import { ForfaitsParam } from "./forfaits";
import { TypesConstructionParam } from "./types-construction";

type Params = {
  forceKn: number; sectionMm2: number; moduleMpa: number; pertesPct: number; mmParMetre: number;
  modifiePar: string | null; modifieLe: string | null; modifiable: boolean;
};

const fr = (n: number) => String(n).replace(".", ",");

export default function ParametragePage() {
  const [p, setP] = useState<Params | null>(null);
  const [f, setF] = useState({ forceKn: "", sectionMm2: "", moduleMpa: "", pertesPct: "" });
  const [message, setMessage] = useState<{ ok: boolean; texte: string } | null>(null);

  const charger = (r: Params) => { setP(r); setF({ forceKn: fr(r.forceKn), sectionMm2: fr(r.sectionMm2), moduleMpa: fr(r.moduleMpa), pertesPct: fr(r.pertesPct) }); };
  useEffect(() => { api<Params>("/parametres/tension").then(charger).catch(() => setMessage({ ok: false, texte: "Chargement impossible." })); }, []);

  if (!p) return <div className="text-secondaire">{message?.texte ?? "Chargement..."}</div>;

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

  // Toute la largeur : les blocs larges (utilisateurs, entités) s'étendent sur deux colonnes, les autres se rangent deux par deux.
  // Un bloc que le rôle ne voit pas ne rend rien : son emplacement vide est masqué (empty:hidden).
  return (
    <div className="grid items-start gap-5 xl:grid-cols-2">
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
      <div className="empty:hidden"><TypesConstructionParam /></div>
      <div className="empty:hidden xl:col-span-2"><UtilisateursParam /></div>
      <div className="empty:hidden xl:col-span-2"><EntitesParam /></div>
      <div className="empty:hidden"><AccessoiresParam /></div>
      <div className="empty:hidden"><CategoriesDepenseParam /></div>
      <div className="empty:hidden"><ForfaitsParam /></div>
      <div className="empty:hidden"><DevisParam /></div>
      <div className="empty:hidden"><CoulisParam /></div>
    </div>
  );
}

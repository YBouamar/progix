"use client";

import { useEffect, useState } from "react";
import { Plus, Settings2 } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn, filtrerDecimal, formatDH, lireNombre } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";
import { ForfaitsParam } from "../parametrage/forfaits";
import { TypesConstructionParam } from "../parametrage/types-construction";
import type { ProjetDetail } from "./types";

/** Petit bouton « gérer la liste », placé à côté d'un champ à choix : visible seulement pour qui a l'écriture sur l'élément. */
export function BoutonGerer({ libelle, onClick }: { libelle: string; onClick: () => void }) {
  return (
    <button type="button" onClick={onClick} title={libelle} aria-label={libelle}
      className="inline-flex h-9 w-9 shrink-0 cursor-pointer items-center justify-center rounded-lg border border-bordure-forte bg-white text-secondaire hover:bg-fond hover:text-marine">
      <Settings2 size={16} aria-hidden />
    </button>
  );
}

/** Types de construction : ajouter, renommer, ordonner, désactiver ou retirer, depuis la fiche du projet. */
export function GererTypes({ onClose, onChange }: { onClose: () => void; onChange: () => void }) {
  return (
    <Modal large open onOpenChange={(o) => !o && onClose()} title="Types de construction"
      description="Les changements s'appliquent tout de suite à la liste du projet."
      footer={<Button variant="outline" onClick={onClose}>Fermer</Button>}>
      <TypesConstructionParam integre onChange={onChange} />
    </Modal>
  );
}

/** Grille des forfaits des devis : ajouter, modifier ou retirer des tranches de surface. */
export function GererGrille({ onClose, onChange }: { onClose: () => void; onChange: () => void }) {
  return (
    <Modal large open onOpenChange={(o) => !o && onClose()} title="Grille des forfaits"
      description="La grille s'applique aux projets sans forfait propre."
      footer={<Button variant="outline" onClick={onClose}>Fermer</Button>}>
      <ForfaitsParam integre onChange={onChange} />
    </Modal>
  );
}

type Commercial = { id: string; nom: string; actif: boolean };

/**
 * Commerciaux proposés sur le projet : les utilisateurs de fonction « agent commercial ».
 * Un commercial ne se supprime pas, il se désactive : les projets qu'il porte gardent son nom.
 */
export function GererCommerciaux({ onClose, onChange }: { onClose: () => void; onChange: () => void }) {
  const [liste, setListe] = useState<Commercial[]>([]);
  const [noms, setNoms] = useState<Record<string, string>>({});
  const [prenom, setPrenom] = useState("");
  const [nom, setNom] = useState("");
  const [email, setEmail] = useState("");
  const [message, setMessage] = useState<{ ok: boolean; texte: string } | null>(null);
  const [envoi, setEnvoi] = useState(false);

  const recevoir = (r: { commerciaux: Commercial[] }) => { setListe(r.commerciaux); setNoms(Object.fromEntries(r.commerciaux.map((c) => [c.id, c.nom]))); };
  useEffect(() => { api<{ commerciaux: Commercial[] }>("/commerciaux").then(recevoir).catch(() => setMessage({ ok: false, texte: "Chargement impossible." })); }, []);

  async function agir(f: () => Promise<{ commerciaux: Commercial[] }>, texte: string) {
    setEnvoi(true); setMessage(null);
    try { recevoir(await f()); setMessage({ ok: true, texte }); onChange(); return true; }
    catch (e) { setMessage({ ok: false, texte: e instanceof ApiError ? (e.errors ? Object.values(e.errors)[0]?.[0] ?? e.message : e.message) : "Enregistrement impossible." }); return false; }
    finally { setEnvoi(false); }
  }
  const maj = (c: Commercial, corps: { nom: string; actif: boolean }, texte: string) =>
    agir(() => api(`/commerciaux/${c.id}`, { method: "PUT", body: JSON.stringify(corps) }), texte);

  return (
    <Modal large open onOpenChange={(o) => !o && onClose()} title="Commerciaux"
      description="Les agents commerciaux proposés sur la fiche du projet. Un commercial retiré reste affiché sur les projets qu'il porte déjà."
      footer={<Button variant="outline" onClick={onClose}>Fermer</Button>}>
      <ul className="divide-y divide-bordure rounded-lg border border-bordure">
        {liste.map((c) => (
          <li key={c.id} className="flex flex-wrap items-center gap-3 px-3 py-2.5">
            <Input className="h-9 min-w-[200px] flex-1" aria-label={`Nom de ${c.nom}`} value={noms[c.id] ?? ""} onChange={(e) => setNoms({ ...noms, [c.id]: e.target.value })} />
            <span className={cn("text-[12px] font-semibold", c.actif ? "text-vert-texte" : "text-attenue")}>{c.actif ? "Proposé" : "Retiré"}</span>
            <Button size="sm" variant="outline" disabled={envoi || (noms[c.id] ?? "").trim() === c.nom} onClick={() => maj(c, { nom: noms[c.id], actif: c.actif }, "Nom enregistré.")}>Renommer</Button>
            <Button size="sm" variant="outline" disabled={envoi} onClick={() => maj(c, { nom: c.nom, actif: !c.actif }, c.actif ? "Commercial retiré de la liste." : "Commercial de nouveau proposé.")}>{c.actif ? "Retirer" : "Remettre"}</Button>
          </li>
        ))}
        {liste.length === 0 && <li className="px-3 py-4 text-center text-[13px] text-attenue">Aucun commercial pour l&apos;instant.</li>}
      </ul>

      <h3 className="mt-5 text-[14px] font-bold">Ajouter un commercial</h3>
      <div className="mt-2 grid gap-3 sm:grid-cols-3">
        <Field label="Prénom"><Input value={prenom} onChange={(e) => setPrenom(e.target.value)} /></Field>
        <Field label="Nom"><Input value={nom} onChange={(e) => setNom(e.target.value)} /></Field>
        <Field label="Adresse email"><Input type="email" value={email} onChange={(e) => setEmail(e.target.value)} /></Field>
      </div>
      <p className="mt-2 text-[12px] text-attenue">Le compte est créé sans mot de passe connu : le commercial ne peut pas se connecter tant qu&apos;un responsable des accès ne lui en donne pas un (Paramétrage, Accès).</p>
      <div className="mt-3 flex flex-wrap items-center gap-3">
        <Button disabled={envoi || !prenom.trim() || !nom.trim() || !email.trim()}
          onClick={async () => { if (await agir(() => api("/commerciaux", { method: "POST", body: JSON.stringify({ prenom, nom, email }) }), "Commercial ajouté.")) { setPrenom(""); setNom(""); setEmail(""); } }}>
          <Plus size={15} />Ajouter
        </Button>
        {message && <span role={message.ok ? "status" : "alert"} className={cn("text-[13px]", message.ok ? "text-vert-texte" : "text-rouge-texte")}>{message.texte}</span>}
      </div>
    </Modal>
  );
}

/**
 * Forfait du projet : prix propre au projet, ou grille des forfaits par surface. Remplir le champ remplace la grille pour les devis du projet ;
 * le vider revient à la grille. Un devis émis garde le forfait de son calcul.
 */
export function ForfaitProjet({ projet, peutModifier, peutGrille, onSaved }: { projet: ProjetDetail; peutModifier: boolean; peutGrille: boolean; onSaved: (p: ProjetDetail) => void }) {
  const [ouvert, setOuvert] = useState(false);
  const [grille, setGrille] = useState(false);
  const [valeur, setValeur] = useState("");
  const [erreur, setErreur] = useState("");
  const [envoi, setEnvoi] = useState(false);
  const propre = projet.forfaitPropre ?? null;

  async function enregistrer(forfait: number | null) {
    setEnvoi(true); setErreur("");
    try {
      onSaved(await api<ProjetDetail>(`/projets/${projet.id}/forfait`, { method: "PUT", body: JSON.stringify({ forfait }) }));
      setOuvert(false);
    } catch (e) { setErreur(e instanceof ApiError ? e.message : "Enregistrement impossible."); }
    finally { setEnvoi(false); }
  }

  return (
    <>
      <div className="flex flex-wrap items-center gap-3">
        <span>{propre ? `${formatDH(propre)} par m², propre au projet` : "Grille des forfaits, selon la surface du devis"}</span>
        {peutModifier && !projet.supprime && (
          <Button size="sm" variant="outline" onClick={() => { setValeur(propre ? String(propre).replace(".", ",") : ""); setErreur(""); setOuvert(true); }}>
            {propre ? "Modifier" : "Fixer un forfait"}
          </Button>
        )}
      </div>
      <Modal open={ouvert} onOpenChange={setOuvert} title="Forfait du projet"
        description="Un forfait propre au projet remplace la grille pour tous ses devis. Les brouillons le reprennent à leur prochain enregistrement ; les devis émis gardent leur forfait."
        footer={
          <>
            <Button variant="outline" onClick={() => setOuvert(false)}>Annuler</Button>
            {propre && <Button variant="outline" disabled={envoi} onClick={() => enregistrer(null)}>Revenir à la grille</Button>}
            <Button disabled={envoi || lireNombre(valeur) <= 0} onClick={() => enregistrer(lireNombre(valeur))}>Enregistrer</Button>
          </>
        }>
        <Field label="Forfait (DH/m²)" error={erreur}>
          <Input autoFocus inputMode="decimal" value={valeur} onChange={(e) => setValeur(filtrerDecimal(e.target.value))} placeholder="Par exemple : 95" />
        </Field>
        {peutGrille && <button type="button" onClick={() => { setOuvert(false); setGrille(true); }} className="mt-3 cursor-pointer text-[13px] font-semibold text-marine hover:underline">Gérer la grille des forfaits</button>}
      </Modal>
      {grille && <GererGrille onClose={() => setGrille(false)} onChange={() => undefined} />}
    </>
  );
}

"use client";

import { useEffect, useState } from "react";
import { api, type Paged } from "@/lib/api";
import { ENTITES, type LigneEdition, nouvelleLigne } from "@/lib/negoce";
import { Button } from "@/components/ui/button";
import { Field, Input, Select } from "@/components/ui/input";
import { Sheet } from "@/components/ui/sheet";
import { PartenaireCombo } from "@/components/partenaire-combo";
import { Alerte, EntiteSelect, LignesEditeur, Totaux, lignesEnvoi, messageErreur, totalHT, useArticles } from "@/components/negoce/commun";

type PrixClient = { groupe: { id: string; nom: string } | null; prix: { articleId: string; prixPublic: number; prixClient: number }[] };
type ProjetOption = { id: string; code: string; libelle: string; client: string };

export type DocumentExistant = { id: string; entite: string; clientId: string; client: string; projetId: string | null; date: string; echeance: string | null; notes: string | null; lignes: { articleId: string; quantite: number; prixUnitaire: number }[]; tauxTva: number };

/** Création d'un devis ou d'une commande, ou modification d'un devis ouvert. Le prix proposé tient compte du groupe du client. */
export function EditionDocument({ type, existant, onFermer, onFait }: { type: "devis" | "commandes"; existant?: DocumentExistant; onFermer: () => void; onFait: (id: string) => void }) {
  const articles = useArticles();
  const [entite, setEntite] = useState(existant?.entite ?? Object.keys(ENTITES)[0]);
  const [client, setClient] = useState<{ id: string | null; texte: string }>({ id: existant?.clientId ?? null, texte: existant?.client ?? "" });
  const [projetId, setProjetId] = useState(existant?.projetId ?? "");
  const [projets, setProjets] = useState<ProjetOption[]>([]);
  const [date, setDate] = useState(existant?.date ?? new Date().toISOString().slice(0, 10));
  const [echeance, setEcheance] = useState(existant?.echeance ?? "");
  const [notes, setNotes] = useState(existant?.notes ?? "");
  const [lignes, setLignes] = useState<LigneEdition[]>(existant ? existant.lignes.map((l) => ({ ...nouvelleLigne(), articleId: l.articleId, quantite: String(l.quantite).replace(".", ","), prix: String(l.prixUnitaire).replace(".", ",") })) : [nouvelleLigne()]);
  const [prix, setPrix] = useState<PrixClient | null>(null);
  const [erreur, setErreur] = useState("");
  const [enCours, setEnCours] = useState(false);

  // Les projets ne sont proposés qu'aux profils qui y ont accès ; une vente directe n'en exige pas.
  useEffect(() => { api<Paged<ProjetOption>>("/projets?pageSize=200&supprimes=false").then((r) => setProjets(r.items)).catch(() => setProjets([])); }, []);
  useEffect(() => {
    api<PrixClient>(`/negoce/articles/prix${client.id ? `?clientId=${client.id}` : ""}`).then(setPrix).catch(() => setPrix(null));
  }, [client.id]);

  const prixPar = (id: string) => { const p = prix?.prix.find((x) => x.articleId === id); return p ? p.prixClient : articles.find((a) => a.id === id)?.prixVente; };
  const ht = totalHT(lignes, prixPar);
  const taux = existant?.tauxTva ?? 20;
  const tva = Math.round(ht * taux) / 100;
  const libelle = type === "devis" ? "devis" : "commande";

  const enregistrer = async () => {
    setErreur(""); setEnCours(true);
    const corps = { entite, clientId: client.id, projetId: projetId || null, dateDocument: date, echeance: echeance || null, notes, lignes: lignesEnvoi(lignes) };
    try {
      const r = existant
        ? await api<{ id: string }>(`/negoce/devis/${existant.id}`, { method: "PUT", body: JSON.stringify(corps) })
        : await api<{ id: string }>(`/negoce/${type}`, { method: "POST", body: JSON.stringify(corps) });
      onFait(r.id);
    } catch (e) { setErreur(messageErreur(e)); } finally { setEnCours(false); }
  };

  return (
    <Sheet open onOpenChange={(o) => !o && onFermer()} large title={existant ? "Modifier le devis" : `Nouveau ${libelle}`} description="Le numéro est attribué à l'enregistrement, par entité."
      footer={<><Button variant="outline" onClick={onFermer}>Annuler</Button><Button onClick={enregistrer} disabled={enCours || !client.id}>Enregistrer</Button></>}>
      <Alerte message={erreur} />
      <div className="mb-6 grid gap-4 sm:grid-cols-4">
        <Field label="Client" required className="sm:col-span-2">
          <PartenaireCombo id={client.id} texte={client.texte} placeholder="Rechercher un client" disabled={!!existant} onChange={(id, texte) => setClient({ id, texte })} />
          {prix?.groupe && <p className="mt-1 text-[12px] text-attenue">Groupe de prix : {prix.groupe.nom}</p>}
        </Field>
        <Field label="Entité"><EntiteSelect valeur={entite} onChange={setEntite} disabled={!!existant} /></Field>
        <Field label="Date"><Input type="date" value={date} onChange={(e) => setDate(e.target.value)} /></Field>
        {projets.length > 0 && (
          <Field label="Projet (facultatif)" className="sm:col-span-2">
            <Select value={projetId} onChange={(e) => setProjetId(e.target.value)}><option value="">Aucun projet</option>{projets.map((p) => <option key={p.id} value={p.id}>{p.code} · {p.libelle} ({p.client})</option>)}</Select>
          </Field>
        )}
        <Field label="Échéance"><Input type="date" value={echeance} onChange={(e) => setEcheance(e.target.value)} /></Field>
        <Field label="Notes" className="sm:col-span-4"><Input value={notes} onChange={(e) => setNotes(e.target.value)} /></Field>
      </div>
      <LignesEditeur lignes={lignes} onChange={setLignes} articles={articles} prixPar={prixPar} />
      <p className="mt-2 text-[12px] text-attenue">Prix laissé vide : le prix du groupe du client, ou le prix public, s&apos;applique.</p>
      <div className="mt-6"><Totaux ht={ht} tva={tva} ttc={ht + tva} /></div>
    </Sheet>
  );
}

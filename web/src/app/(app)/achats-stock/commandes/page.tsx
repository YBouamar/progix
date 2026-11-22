"use client";

import { useCallback, useEffect, useState } from "react";
import { Plus, Search } from "lucide-react";
import { api } from "@/lib/api";
import { peutEcrire, useAuth } from "@/lib/auth";
import { ENTITES, type LigneEdition, nouvelleLigne, tonEtatAchat } from "@/lib/negoce";
import { formatDate, formatDH } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field, Input, Select } from "@/components/ui/input";
import { Sheet } from "@/components/ui/sheet";
import { Alerte, EntiteSelect, LIGNE_CLIQUABLE, LignesEditeur, TABLE_BOITE, TD, TH, THEAD, Totaux, Vide, lignesEnvoi, messageErreur, totalHT, useArticles } from "@/components/negoce/commun";
import { DetailCommande } from "./detail";

type Ligne = { id: string; numero: string; entite: string; fournisseurId: string; fournisseur: string; date: string; etat: string; etatLabel: string; totalTtc: number | null; reste: number | null };
type Fournisseur = { id: string; nom: string; actif: boolean };

const ETATS: Record<string, string> = { en_attente: "En attente", partielle: "Partiellement reçue", recue: "Reçue", annulee: "Annulée" };

export default function CommandesAchatPage() {
  const { user } = useAuth();
  const ecrit = peutEcrire(user, "negoce_achats");
  const [liste, setListe] = useState<Ligne[] | null>(null);
  const [fournisseurs, setFournisseurs] = useState<Fournisseur[]>([]);
  const [recherche, setRecherche] = useState("");
  const [etat, setEtat] = useState("");
  const [fournisseur, setFournisseur] = useState("");
  const [erreur, setErreur] = useState("");
  const [creation, setCreation] = useState(false);
  const [ouvert, setOuvert] = useState<string | null>(null);

  const charger = useCallback(() => {
    const p = new URLSearchParams();
    if (etat) p.set("etat", etat);
    if (fournisseur) p.set("fournisseurId", fournisseur);
    if (recherche.trim()) p.set("recherche", recherche.trim());
    api<Ligne[]>(`/negoce/achats?${p}`).then((r) => { setListe(r); setErreur(""); }).catch((e) => setErreur(messageErreur(e, "Chargement impossible.")));
  }, [etat, fournisseur, recherche]);
  useEffect(() => { const t = setTimeout(charger, 250); return () => clearTimeout(t); }, [charger]);
  useEffect(() => { api<Fournisseur[]>("/negoce/fournisseurs").then(setFournisseurs).catch(() => undefined); }, []);

  const voitMontants = liste?.some((l) => l.totalTtc !== null) ?? false;
  const voitReste = liste?.some((l) => l.reste !== null) ?? false;

  return (
    <div>
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <div className="relative w-full max-w-xs">
          <Search size={16} className="pointer-events-none absolute left-3 top-3 text-attenue" />
          <Input className="pl-9" placeholder="Numéro, fournisseur" value={recherche} onChange={(e) => setRecherche(e.target.value)} />
        </div>
        <Select className="w-auto" aria-label="État" value={etat} onChange={(e) => setEtat(e.target.value)}>
          <option value="">Tous les états</option>{Object.entries(ETATS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
        </Select>
        <Select className="w-auto" aria-label="Fournisseur" value={fournisseur} onChange={(e) => setFournisseur(e.target.value)}>
          <option value="">Tous les fournisseurs</option>{fournisseurs.map((f) => <option key={f.id} value={f.id}>{f.nom}</option>)}
        </Select>
        {ecrit && <Button className="ml-auto" onClick={() => setCreation(true)}><Plus size={16} />Nouvelle commande d&apos;achat</Button>}
      </div>
      <Alerte message={erreur} />
      {liste && liste.length === 0 ? <Vide texte="Aucune commande d'achat." /> : (
        <div className={TABLE_BOITE}>
          <table className="w-full text-left">
            <thead className={THEAD}><tr><th className={TH}>Commande</th><th className={TH}>Fournisseur</th><th className={TH}>État</th>{voitMontants && <th className={`${TH} text-right`}>Total TTC</th>}{voitReste && <th className={`${TH} text-right`}>Reste à payer</th>}</tr></thead>
            <tbody>
              {liste?.map((l) => (
                <tr key={l.id} className={LIGNE_CLIQUABLE} onClick={() => setOuvert(l.id)}>
                  <td className={TD}><div className="font-display font-bold text-marine">{l.numero}</div><div className="text-[12px] text-attenue">{formatDate(l.date)} · {l.entite}</div></td>
                  <td className={TD}>{l.fournisseur}</td>
                  <td className={TD}><Badge ton={tonEtatAchat(l.etat)}>{l.etatLabel}</Badge></td>
                  {voitMontants && <td className={`${TD} text-right font-display`}>{l.totalTtc !== null ? formatDH(l.totalTtc) : ""}</td>}
                  {voitReste && <td className={`${TD} text-right font-display`}>{l.reste ? formatDH(l.reste) : "-"}</td>}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {creation && <NouvelleCommande fournisseurs={fournisseurs.filter((f) => f.actif)} onFermer={() => setCreation(false)} onCree={(id) => { setCreation(false); charger(); setOuvert(id); }} />}
      {ouvert && <DetailCommande id={ouvert} onFermer={() => setOuvert(null)} onChange={charger} />}
    </div>
  );
}

function NouvelleCommande({ fournisseurs, onFermer, onCree }: { fournisseurs: Fournisseur[]; onFermer: () => void; onCree: (id: string) => void }) {
  const articles = useArticles();
  const [entite, setEntite] = useState(Object.keys(ENTITES)[0]);
  const [fournisseurId, setFournisseurId] = useState("");
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10));
  const [notes, setNotes] = useState("");
  const [lignes, setLignes] = useState<LigneEdition[]>([nouvelleLigne()]);
  const [prixFournisseur, setPrixFournisseur] = useState<Record<string, number>>({});
  const [erreur, setErreur] = useState("");
  const [enCours, setEnCours] = useState(false);

  // Prix d'achat du fournisseur choisi : proposés tant qu'aucun prix n'est saisi sur la ligne.
  useEffect(() => {
    if (!fournisseurId) { setPrixFournisseur({}); return; }
    api<{ articleId: string; prixAchat: number }[]>(`/negoce/prix-achat?fournisseurId=${fournisseurId}`)
      .then((r) => setPrixFournisseur(Object.fromEntries(r.map((p) => [p.articleId, p.prixAchat])))).catch(() => setPrixFournisseur({}));
  }, [fournisseurId]);

  const prixPar = (id: string) => prixFournisseur[id];
  const ht = totalHT(lignes, prixPar);
  const enregistrer = async () => {
    setErreur(""); setEnCours(true);
    try {
      const r = await api<{ id: string }>("/negoce/achats", { method: "POST", body: JSON.stringify({ entite, fournisseurId, dateCommande: date, notes, lignes: lignesEnvoi(lignes) }) });
      onCree(r.id);
    } catch (e) { setErreur(messageErreur(e)); } finally { setEnCours(false); }
  };
  return (
    <Sheet open onOpenChange={(o) => !o && onFermer()} large title="Nouvelle commande d'achat" description="Le numéro est attribué à l'enregistrement."
      footer={<><Button variant="outline" onClick={onFermer}>Annuler</Button><Button onClick={enregistrer} disabled={enCours || !fournisseurId}>Enregistrer la commande</Button></>}>
      <Alerte message={erreur} />
      <div className="mb-6 grid gap-4 sm:grid-cols-4">
        <Field label="Fournisseur" required className="sm:col-span-2">
          <Select value={fournisseurId} onChange={(e) => setFournisseurId(e.target.value)}><option value="">Choisir un fournisseur</option>{fournisseurs.map((f) => <option key={f.id} value={f.id}>{f.nom}</option>)}</Select>
        </Field>
        <Field label="Entité"><EntiteSelect valeur={entite} onChange={setEntite} /></Field>
        <Field label="Date de la commande"><Input type="date" value={date} onChange={(e) => setDate(e.target.value)} /></Field>
        <Field label="Notes" className="sm:col-span-4"><Input value={notes} onChange={(e) => setNotes(e.target.value)} /></Field>
      </div>
      <LignesEditeur lignes={lignes} onChange={setLignes} articles={articles} prixPar={prixPar} filtre={(a) => a.origine === "achete"} libellePrix="Prix d'achat HT" />
      <div className="mt-6"><Totaux ht={ht} tva={Math.round(ht * 20) / 100} ttc={ht + Math.round(ht * 20) / 100} /></div>
    </Sheet>
  );
}

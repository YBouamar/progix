"use client";

import { useCallback, useEffect, useState } from "react";
import { Plus, Search } from "lucide-react";
import { api } from "@/lib/api";
import { peutEcrire, useAuth } from "@/lib/auth";
import { tonEtatAchat } from "@/lib/negoce";
import { formatDate, formatDH } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/input";
import { Sheet } from "@/components/ui/sheet";
import { Alerte, LIGNE_CLIQUABLE, TABLE_BOITE, TD, TH, THEAD, Vide, messageErreur } from "@/components/negoce/commun";

type Ligne = { id: string; nom: string; contact: string | null; telephone: string | null; email: string | null; ville: string | null; actif: boolean; commandes: number; enCours: number; reste: number | null };
type Fiche = {
  id: string; nom: string; contact: string | null; telephone: string | null; email: string | null; adresse: string | null; ville: string | null; ice: string | null; notes: string | null; actif: boolean;
  commandes: { id: string; numero: string; date: string; etat: string; etatLabel: string; totalTtc: number | null; reste: number | null }[];
};
type Form = { nom: string; contact: string; telephone: string; email: string; adresse: string; ville: string; ice: string; notes: string; actif: boolean };
const VIDE: Form = { nom: "", contact: "", telephone: "", email: "", adresse: "", ville: "", ice: "", notes: "", actif: true };

export default function FournisseursPage() {
  const { user } = useAuth();
  const ecrit = peutEcrire(user, "negoce_fournisseurs");
  const [liste, setListe] = useState<Ligne[] | null>(null);
  const [recherche, setRecherche] = useState("");
  const [erreur, setErreur] = useState("");
  const [ouvert, setOuvert] = useState<Fiche | "nouveau" | null>(null);

  const charger = useCallback(() => {
    api<Ligne[]>("/negoce/fournisseurs").then((r) => { setListe(r); setErreur(""); }).catch((e) => setErreur(messageErreur(e, "Chargement impossible.")));
  }, []);
  useEffect(() => { charger(); }, [charger]);

  const voitReste = liste?.some((l) => l.reste !== null) ?? false;
  const affiche = (liste ?? []).filter((l) => !recherche.trim() || `${l.nom} ${l.contact ?? ""} ${l.ville ?? ""}`.toLowerCase().includes(recherche.trim().toLowerCase()));

  return (
    <div>
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <div className="relative w-full max-w-xs">
          <Search size={16} className="pointer-events-none absolute left-3 top-3 text-attenue" />
          <Input className="pl-9" placeholder="Nom, contact, ville" value={recherche} onChange={(e) => setRecherche(e.target.value)} />
        </div>
        {ecrit && <Button className="ml-auto" onClick={() => setOuvert("nouveau")}><Plus size={16} />Nouveau fournisseur</Button>}
      </div>
      <Alerte message={erreur} />
      {liste && affiche.length === 0 ? <Vide texte="Aucun fournisseur." /> : (
        <div className={TABLE_BOITE}>
          <table className="w-full text-left">
            <thead className={THEAD}><tr><th className={TH}>Fournisseur</th><th className={TH}>Contact</th><th className={TH}>Ville</th><th className={`${TH} text-right`}>Commandes en cours</th>{voitReste && <th className={`${TH} text-right`}>Reste à payer</th>}</tr></thead>
            <tbody>
              {affiche.map((l) => (
                <tr key={l.id} className={LIGNE_CLIQUABLE} onClick={() => api<Fiche>(`/negoce/fournisseurs/${l.id}`).then(setOuvert).catch((e) => setErreur(messageErreur(e)))}>
                  <td className={TD}><span className="font-semibold">{l.nom}</span>{!l.actif && <span className="ml-2 text-[12px] text-attenue">désactivé</span>}</td>
                  <td className={TD}>{l.contact}<div className="text-[12px] text-attenue">{l.telephone}</div></td>
                  <td className={`${TD} text-secondaire`}>{l.ville}</td>
                  <td className={`${TD} text-right font-display`}>{l.enCours}</td>
                  {voitReste && <td className={`${TD} text-right font-display`}>{l.reste ? formatDH(l.reste) : "-"}</td>}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {ouvert && <FicheFournisseur fiche={ouvert === "nouveau" ? null : ouvert} ecrit={ecrit} onFermer={() => setOuvert(null)} onChange={charger} />}
    </div>
  );
}

function FicheFournisseur({ fiche, ecrit, onFermer, onChange }: { fiche: Fiche | null; ecrit: boolean; onFermer: () => void; onChange: () => void }) {
  const [f, setF] = useState<Form>(fiche ? { nom: fiche.nom, contact: fiche.contact ?? "", telephone: fiche.telephone ?? "", email: fiche.email ?? "", adresse: fiche.adresse ?? "", ville: fiche.ville ?? "", ice: fiche.ice ?? "", notes: fiche.notes ?? "", actif: fiche.actif } : VIDE);
  const [erreur, setErreur] = useState("");
  const maj = (p: Partial<Form>) => setF((x) => ({ ...x, ...p }));
  const enregistrer = async () => {
    setErreur("");
    try {
      await api(fiche ? `/negoce/fournisseurs/${fiche.id}` : "/negoce/fournisseurs", { method: fiche ? "PUT" : "POST", body: JSON.stringify(f) });
      onChange(); onFermer();
    } catch (e) { setErreur(messageErreur(e)); }
  };
  return (
    <Sheet open onOpenChange={(o) => !o && onFermer()} title={fiche ? fiche.nom : "Nouveau fournisseur"} description="Coordonnées et commandes d'achat"
      footer={ecrit && <><Button variant="outline" onClick={onFermer}>Annuler</Button><Button onClick={enregistrer}>Enregistrer</Button></>}>
      <Alerte message={erreur} />
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Nom" required className="sm:col-span-2"><Input value={f.nom} disabled={!ecrit} onChange={(e) => maj({ nom: e.target.value })} /></Field>
        <Field label="Contact"><Input value={f.contact} disabled={!ecrit} onChange={(e) => maj({ contact: e.target.value })} /></Field>
        <Field label="Téléphone"><Input value={f.telephone} disabled={!ecrit} onChange={(e) => maj({ telephone: e.target.value })} /></Field>
        <Field label="Courriel"><Input type="email" value={f.email} disabled={!ecrit} onChange={(e) => maj({ email: e.target.value })} /></Field>
        <Field label="ICE"><Input value={f.ice} disabled={!ecrit} onChange={(e) => maj({ ice: e.target.value })} /></Field>
        <Field label="Adresse"><Input value={f.adresse} disabled={!ecrit} onChange={(e) => maj({ adresse: e.target.value })} /></Field>
        <Field label="Ville"><Input value={f.ville} disabled={!ecrit} onChange={(e) => maj({ ville: e.target.value })} /></Field>
        <Field label="Notes" className="sm:col-span-2"><Input value={f.notes} disabled={!ecrit} onChange={(e) => maj({ notes: e.target.value })} /></Field>
        {fiche && <label className="flex items-center gap-2 text-[14px] sm:col-span-2"><input type="checkbox" checked={f.actif} disabled={!ecrit} onChange={(e) => maj({ actif: e.target.checked })} />Fournisseur actif (proposé pour une nouvelle commande)</label>}
      </div>
      {fiche && (
        <section className="mt-8">
          <h3 className="mb-2 font-display text-[15px] font-bold">Commandes d&apos;achat</h3>
          {fiche.commandes.length === 0 ? <p className="text-secondaire">Aucune commande.</p> : (
            <ul className="divide-y divide-bordure rounded-lg border border-bordure">
              {fiche.commandes.map((c) => (
                <li key={c.id} className="flex items-center justify-between gap-3 px-4 py-2.5">
                  <span><span className="font-display font-bold text-marine">{c.numero}</span> <span className="text-[13px] text-secondaire">{formatDate(c.date)}</span></span>
                  <span className="flex items-center gap-3">{c.totalTtc !== null && <span className="font-display">{formatDH(c.totalTtc)}</span>}<Badge ton={tonEtatAchat(c.etat)}>{c.etatLabel}</Badge></span>
                </li>
              ))}
            </ul>
          )}
        </section>
      )}
    </Sheet>
  );
}

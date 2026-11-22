"use client";

import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api";
import { peutEcrire, useAuth } from "@/lib/auth";
import { formatQuantite, tonEtatVente } from "@/lib/negoce";
import { formatDate, formatDH } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";
import { Sheet } from "@/components/ui/sheet";
import { PartenaireCombo } from "@/components/partenaire-combo";
import { Alerte, EntiteSelect, Totaux, messageErreur } from "@/components/negoce/commun";
import { Annulation } from "@/components/negoce/annulation";
import { SignatureDoc } from "@/components/signature-doc";
import { Reprise } from "@/components/negoce/reprise";
import { EditionDocument, type DocumentExistant } from "./edition";

type LigneBase = { id: string; articleId: string; code: string; designation: string; unite: string; quantite: number; prixUnitaire: number; montantHt: number };
type Enfant = { id: string; numero: string; date: string; etatLabel: string; etat: string; totalTtc: number };
type Entete = {
  id: string; numero: string; entite: string; clientId: string; client: string; projetId: string | null; projetCode: string | null; groupe: string | null; parentId: string | null; parentNumero: string | null; signePar: string | null; signeLe: string | null;
  date: string; echeance: string | null; etat: string; etatLabel: string; tauxTva: number; notes: string | null; creePar: string; motifAnnulation: string | null; totalHt: number; totalTva: number; totalTtc: number;
};
type DetailD = Entete & { lignes: LigneBase[]; commande: { id: string; numero: string } | null };
type LigneC = LigneBase & { livree: number; facturee: number; resteALivrer: number; resteAFacturer: number };
type DetailC = Entete & { lignes: LigneC[]; livraisons: Enfant[]; factures: Enfant[] };

function Corps({ d }: { d: Entete & { lignes: LigneBase[] } }) {
  return (
    <>
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <Badge ton={tonEtatVente(d.etat)}>{d.etatLabel}</Badge>
        {d.groupe && <Badge ton="violet">Groupe {d.groupe}</Badge>}
        {d.projetCode && <span className="text-[13px] text-secondaire">Projet {d.projetCode}</span>}
        {d.parentNumero && <span className="text-[13px] text-secondaire">Issu de {d.parentNumero}</span>}
        {d.motifAnnulation && <span className="text-[13px] text-rouge-texte">Motif : {d.motifAnnulation}</span>}
        {d.notes && <span className="text-[13px] text-secondaire">{d.notes}</span>}
      </div>
    </>
  );
}

/** Détail d'un devis : modification tant qu'il est ouvert, duplication, refus, annulation, passage en commande. */
export function DetailDevis({ id, onFermer, onChange, onOuvrirCommande }: { id: string; onFermer: () => void; onChange: () => void; onOuvrirCommande: (id: string) => void }) {
  const { user } = useAuth();
  const ecrit = peutEcrire(user, "negoce_devis");
  const [d, setD] = useState<DetailD | null>(null);
  const [erreur, setErreur] = useState("");
  const [mode, setMode] = useState<"edition" | "duplication" | "annulation" | null>(null);

  const charger = useCallback(() => { api<DetailD>(`/negoce/devis/${id}`).then((r) => { setD(r); setErreur(""); }).catch((e) => setErreur(messageErreur(e, "Chargement impossible."))); }, [id]);
  useEffect(() => { charger(); }, [charger]);
  const apres = () => { charger(); onChange(); };
  const agir = async (chemin: string, suite?: (r: { id: string }) => void) => {
    setErreur("");
    try { const r = await api<{ id: string }>(chemin, { method: "POST", body: "{}" }); apres(); suite?.(r); } catch (e) { setErreur(messageErreur(e)); }
  };

  if (!d) return <Sheet open onOpenChange={(o) => !o && onFermer()} large title="Devis" description=""><Alerte message={erreur} /></Sheet>;
  const ouvert = d.etat === "ouvert";
  const existant: DocumentExistant = { id: d.id, entite: d.entite, clientId: d.clientId, client: d.client, projetId: d.projetId, date: d.date, echeance: d.echeance, notes: d.notes, lignes: d.lignes, tauxTva: d.tauxTva };

  return (
    <Sheet open onOpenChange={(o) => !o && onFermer()} large title={d.numero} description={`${d.client} · ${formatDate(d.date)} · ${d.entite}`}
      footer={<>
        {ecrit && d.etat !== "annule" && d.etat !== "accepte" && <Button variant="outline" className="mr-auto text-rouge-texte" onClick={() => setMode("annulation")}>Annuler le devis</Button>}
        {ecrit && <Button variant="outline" onClick={() => setMode("duplication")}>Dupliquer</Button>}
        {ecrit && ouvert && <Button variant="outline" onClick={() => agir(`/negoce/devis/${d.id}/refuser`)}>Marquer refusé</Button>}
        {ecrit && ouvert && !d.signePar && <Button variant="outline" onClick={() => setMode("edition")}>Modifier</Button>}
        {ecrit && ouvert && <Button onClick={() => agir(`/negoce/devis/${d.id}/commande`, (r) => onOuvrirCommande(r.id))}>Accepté : créer la commande</Button>}
      </>}>
      <Alerte message={erreur} />
      <Corps d={d} />
      {d.commande && <p className="mb-4 text-[14px]">Commande créée : <button className="font-display font-bold text-marine underline" onClick={() => onOuvrirCommande(d.commande!.id)}>{d.commande.numero}</button></p>}
      <TableLignes lignes={d.lignes} />
      <div className="mt-4"><Totaux ht={d.totalHt} tva={d.totalTva} ttc={d.totalTtc} /></div>
      {d.etat !== "annule" && <SignatureDoc signePar={d.signePar} signeLe={d.signeLe} quoi="ce devis" url={`/negoce/devis/${d.id}`} peutSigner={ecrit} onSigne={apres} />}
      {mode === "edition" && <EditionDocument type="devis" existant={existant} onFermer={() => setMode(null)} onFait={() => { setMode(null); apres(); }} />}
      {mode === "duplication" && <Duplication d={d} onFermer={() => setMode(null)} onFait={() => { setMode(null); onChange(); onFermer(); }} />}
      {mode === "annulation" && <Annulation chemin={`/negoce/devis/${d.id}/annuler`} titre="Annuler le devis" onFermer={() => setMode(null)} onFait={() => { setMode(null); apres(); }} />}
    </Sheet>
  );
}

function TableLignes({ lignes }: { lignes: LigneBase[] }) {
  return (
    <div className="overflow-x-auto rounded-lg border border-bordure">
      <table className="w-full text-left text-[14px]">
        <thead className="border-b border-bordure bg-fond text-[12px] text-secondaire"><tr><th className="px-4 py-2 font-semibold">Article</th><th className="px-4 py-2 text-right font-semibold">Quantité</th><th className="px-4 py-2 text-right font-semibold">Prix HT</th><th className="px-4 py-2 text-right font-semibold">Montant HT</th></tr></thead>
        <tbody>
          {lignes.map((l) => (
            <tr key={l.id} className="border-b border-bordure last:border-0">
              <td className="px-4 py-2"><div className="font-semibold">{l.designation}</div><div className="font-display text-[12px] text-attenue">{l.code}</div></td>
              <td className="px-4 py-2 text-right font-display">{formatQuantite(l.quantite)} {l.unite}</td>
              <td className="px-4 py-2 text-right font-display">{formatDH(l.prixUnitaire)}</td>
              <td className="px-4 py-2 text-right font-display">{formatDH(l.montantHt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** Copie d'un devis : autre client, entité ou projet possibles, prix conservés ou recalculés selon le groupe du nouveau client. */
function Duplication({ d, onFermer, onFait }: { d: DetailD; onFermer: () => void; onFait: () => void }) {
  const [client, setClient] = useState<{ id: string | null; texte: string }>({ id: d.clientId, texte: d.client });
  const [entite, setEntite] = useState(d.entite);
  const [recalculer, setRecalculer] = useState(false);
  const [erreur, setErreur] = useState("");
  const valider = async () => {
    setErreur("");
    try {
      await api(`/negoce/devis/${d.id}/dupliquer`, { method: "POST", body: JSON.stringify({ clientId: client.id, entite, projetId: null, recalculerPrix: recalculer }) });
      onFait();
    } catch (e) { setErreur(messageErreur(e)); }
  };
  return (
    <Modal open onOpenChange={(o) => !o && onFermer()} title={`Dupliquer ${d.numero}`} description="La copie est un nouveau devis ouvert, sous un nouveau numéro."
      footer={<><Button variant="outline" onClick={onFermer}>Annuler</Button><Button onClick={valider} disabled={!client.id}>Dupliquer</Button></>}>
      <Alerte message={erreur} />
      <div className="grid gap-4">
        <Field label="Client"><PartenaireCombo id={client.id} texte={client.texte} onChange={(id, texte) => setClient({ id, texte })} /></Field>
        <Field label="Entité"><EntiteSelect valeur={entite} onChange={setEntite} /></Field>
        <label className="flex items-start gap-2 text-[14px]"><input className="mt-1" type="checkbox" checked={recalculer} onChange={(e) => setRecalculer(e.target.checked)} />Recalculer les prix d&apos;après le groupe du client (sinon les prix du devis sont conservés)</label>
      </div>
    </Modal>
  );
}

/** Détail d'une commande de vente : avancement par ligne, bons de livraison et factures. */
export function DetailCommandeVente({ id, onFermer, onChange }: { id: string; onFermer: () => void; onChange: () => void }) {
  const { user } = useAuth();
  const livre = peutEcrire(user, "negoce_livraisons");
  const facture = peutEcrire(user, "negoce_factures");
  const ecrit = peutEcrire(user, "negoce_devis");
  const [d, setD] = useState<DetailC | null>(null);
  const [erreur, setErreur] = useState("");
  const [mode, setMode] = useState<"livraison" | "facture" | "annulation" | null>(null);

  const charger = useCallback(() => { api<DetailC>(`/negoce/commandes/${id}`).then((r) => { setD(r); setErreur(""); }).catch((e) => setErreur(messageErreur(e, "Chargement impossible."))); }, [id]);
  useEffect(() => { charger(); }, [charger]);
  const apres = () => { charger(); onChange(); };

  if (!d) return <Sheet open onOpenChange={(o) => !o && onFermer()} large title="Commande" description=""><Alerte message={erreur} /></Sheet>;
  const actif = d.etat !== "annule";
  const aLivrer = d.lignes.filter((l) => l.resteALivrer > 0).map((l) => ({ id: l.id, code: l.code, designation: l.designation, unite: l.unite, reste: l.resteALivrer }));
  const aFacturer = d.lignes.filter((l) => l.resteAFacturer > 0).map((l) => ({ id: l.id, code: l.code, designation: l.designation, unite: l.unite, reste: l.resteAFacturer }));
  const sansEnfant = d.livraisons.length === 0 && d.factures.length === 0;

  return (
    <Sheet open onOpenChange={(o) => !o && onFermer()} large title={d.numero} description={`${d.client} · ${formatDate(d.date)} · ${d.entite}`}
      footer={<>
        {ecrit && actif && sansEnfant && <Button variant="outline" className="mr-auto text-rouge-texte" onClick={() => setMode("annulation")}>Annuler la commande</Button>}
        {facture && actif && <Button variant="outline" onClick={() => setMode("facture")}>Facturer ce qui est livré</Button>}
        {livre && actif && <Button onClick={() => setMode("livraison")}>Créer un bon de livraison</Button>}
      </>}>
      <Alerte message={erreur} />
      <Corps d={d} />
      <div className="overflow-x-auto rounded-lg border border-bordure">
        <table className="w-full text-left text-[14px]">
          <thead className="border-b border-bordure bg-fond text-[12px] text-secondaire"><tr><th className="px-4 py-2 font-semibold">Article</th><th className="px-4 py-2 text-right font-semibold">Commandé</th><th className="px-4 py-2 text-right font-semibold">Livré</th><th className="px-4 py-2 text-right font-semibold">Facturé</th><th className="px-4 py-2 text-right font-semibold">Prix HT</th></tr></thead>
          <tbody>
            {d.lignes.map((l) => (
              <tr key={l.id} className="border-b border-bordure last:border-0">
                <td className="px-4 py-2"><div className="font-semibold">{l.designation}</div><div className="font-display text-[12px] text-attenue">{l.code}</div></td>
                <td className="px-4 py-2 text-right font-display">{formatQuantite(l.quantite)} {l.unite}</td>
                <td className="px-4 py-2 text-right font-display">{formatQuantite(l.livree)}</td>
                <td className="px-4 py-2 text-right font-display">{formatQuantite(l.facturee)}</td>
                <td className="px-4 py-2 text-right font-display">{formatDH(l.prixUnitaire)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="mt-4 mb-8"><Totaux ht={d.totalHt} tva={d.totalTva} ttc={d.totalTtc} /></div>
      <ListeEnfants titre="Bons de livraison" vide="Aucune livraison." liste={d.livraisons} />
      <ListeEnfants titre="Factures" vide="Aucune facture." liste={d.factures} />

      {mode === "livraison" && <Reprise titre="Nouveau bon de livraison" description="Indiquez ce qui part. La sortie de stock est enregistrée avec le bon." chemin="/negoce/livraisons" corpsDe={{ commandeId: d.id }} libelleReste="Reste à livrer" lignes={aLivrer} onFermer={() => setMode(null)} onFait={() => { setMode(null); apres(); }} />}
      {mode === "facture" && <Reprise titre="Nouvelle facture" description="On ne facture que ce qui est livré et pas encore facturé." chemin="/negoce/factures" corpsDe={{ commandeId: d.id }} avecEcheance libelleReste="Reste à facturer" lignes={aFacturer} onFermer={() => setMode(null)} onFait={() => { setMode(null); apres(); }} />}
      {mode === "annulation" && <Annulation chemin={`/negoce/commandes/${d.id}/annuler`} titre="Annuler la commande" onFermer={() => setMode(null)} onFait={() => { setMode(null); apres(); }} />}
    </Sheet>
  );
}

function ListeEnfants({ titre, vide, liste }: { titre: string; vide: string; liste: Enfant[] }) {
  return (
    <section className="mb-6">
      <h3 className="mb-2 font-display text-[15px] font-bold">{titre}</h3>
      {liste.length === 0 ? <p className="text-secondaire">{vide}</p> : (
        <ul className="divide-y divide-bordure rounded-lg border border-bordure">
          {liste.map((e) => <li key={e.id} className="flex items-center justify-between px-4 py-2 text-[14px]"><span><span className="font-display font-bold">{e.numero}</span> <span className="text-[13px] text-secondaire">{formatDate(e.date)}</span></span><span className="flex items-center gap-3"><Badge ton={tonEtatVente(e.etat)}>{e.etatLabel}</Badge><span className="font-display">{formatDH(e.totalTtc)}</span></span></li>)}
        </ul>
      )}
    </section>
  );
}

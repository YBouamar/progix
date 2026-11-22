"use client";

import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api";
import { peutEcrire, useAuth } from "@/lib/auth";
import { MODES_PAIEMENT, avecImage, filtrerDecimal, formatQuantite, lireNombre, tonEtatAchat } from "@/lib/negoce";
import { formatDate, formatDH } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field, Input, Select } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";
import { Sheet } from "@/components/ui/sheet";
import { Documents } from "@/components/documents";
import { Alerte, Totaux, messageErreur } from "@/components/negoce/commun";
import { Annulation } from "@/components/negoce/annulation";

type Ligne = { id: string; code: string; designation: string; unite: string; quantite: number; quantiteRecue: number; reste: number; prixUnitaire: number | null };
type Detail = {
  id: string; numero: string; entite: string; fournisseur: string; date: string; notes: string | null; etat: string; etatLabel: string; annule: boolean; motifAnnulation: string | null;
  totalHt: number | null; totalTva: number | null; totalTtc: number | null; paye: number | null; reste: number | null; lignes: Ligne[];
  receptions: { id: string; date: string; par: string; notes: string | null; lignes: { designation: string; unite: string; quantite: number }[] }[];
  factures: { id: string; numero: string; date: string; montantTtc: number; notes: string | null; fichiers: number }[] | null;
  paiements: { id: string; date: string; montant: number; mode: string; reference: string | null; par: string; annule: boolean; motifAnnulation: string | null; avecImage: boolean; fichiers: number }[] | null;
};

const aujourdhui = () => new Date().toISOString().slice(0, 10);

/** Détail d'une commande d'achat : lignes, réceptions, factures du fournisseur, paiements. */
export function DetailCommande({ id, onFermer, onChange }: { id: string; onFermer: () => void; onChange: () => void }) {
  const { user } = useAuth();
  const ecrit = peutEcrire(user, "negoce_achats");
  const recoit = peutEcrire(user, "negoce_receptions");
  const paie = peutEcrire(user, "negoce_paiements_fournisseurs");
  const [d, setD] = useState<Detail | null>(null);
  const [erreur, setErreur] = useState("");
  const [reception, setReception] = useState(false);
  const [annulation, setAnnulation] = useState(false);

  const charger = useCallback(() => {
    api<Detail>(`/negoce/achats/${id}`).then((r) => { setD(r); setErreur(""); }).catch((e) => setErreur(messageErreur(e, "Chargement impossible.")));
  }, [id]);
  useEffect(() => { charger(); }, [charger]);
  const apres = () => { charger(); onChange(); };

  if (!d) return <Sheet open onOpenChange={(o) => !o && onFermer()} large title="Commande d'achat" description=""><Alerte message={erreur} /></Sheet>;
  const modifiable = !d.annule && d.receptions.length === 0 && !(d.paiements ?? []).some((p) => !p.annule);

  return (
    <Sheet open onOpenChange={(o) => !o && onFermer()} large title={d.numero} description={`${d.fournisseur} · ${formatDate(d.date)} · ${d.entite}`}
      footer={<>
        {ecrit && modifiable && <Button variant="outline" className="mr-auto text-rouge-texte" onClick={() => setAnnulation(true)}>Annuler la commande</Button>}
        {recoit && !d.annule && d.etat !== "recue" && <Button onClick={() => setReception(true)}>Enregistrer une réception</Button>}
        <Button variant="outline" onClick={onFermer}>Fermer</Button>
      </>}>
      <Alerte message={erreur} />
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <Badge ton={tonEtatAchat(d.etat)}>{d.etatLabel}</Badge>
        {d.annule && <span className="text-[13px] text-rouge-texte">Motif : {d.motifAnnulation}</span>}
        {d.notes && <span className="text-[13px] text-secondaire">{d.notes}</span>}
      </div>

      <section className="mb-8">
        <h3 className="mb-2 font-display text-[15px] font-bold">Articles commandés</h3>
        <div className="overflow-x-auto rounded-lg border border-bordure">
          <table className="w-full text-left text-[14px]">
            <thead className="border-b border-bordure bg-fond text-[12px] text-secondaire"><tr><th className="px-4 py-2 font-semibold">Article</th><th className="px-4 py-2 text-right font-semibold">Commandé</th><th className="px-4 py-2 text-right font-semibold">Reçu</th><th className="px-4 py-2 text-right font-semibold">Reste</th>{d.totalHt !== null && <th className="px-4 py-2 text-right font-semibold">Prix HT</th>}</tr></thead>
            <tbody>
              {d.lignes.map((l) => (
                <tr key={l.id} className="border-b border-bordure last:border-0">
                  <td className="px-4 py-2"><div className="font-semibold">{l.designation}</div><div className="font-display text-[12px] text-attenue">{l.code}</div></td>
                  <td className="px-4 py-2 text-right font-display">{formatQuantite(l.quantite)} {l.unite}</td>
                  <td className="px-4 py-2 text-right font-display">{formatQuantite(l.quantiteRecue)}</td>
                  <td className="px-4 py-2 text-right font-display">{formatQuantite(l.reste)}</td>
                  {d.totalHt !== null && <td className="px-4 py-2 text-right font-display">{l.prixUnitaire !== null ? formatDH(l.prixUnitaire) : ""}</td>}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {d.totalHt !== null && <div className="mt-4"><Totaux ht={d.totalHt} tva={d.totalTva ?? 0} ttc={d.totalTtc ?? 0} /></div>}
      </section>

      <section className="mb-8">
        <h3 className="mb-2 font-display text-[15px] font-bold">Réceptions</h3>
        {d.receptions.length === 0 ? <p className="text-secondaire">Rien n&apos;a encore été reçu. Chaque réception crée une entrée en stock.</p> : (
          <ul className="divide-y divide-bordure rounded-lg border border-bordure">
            {d.receptions.map((r) => (
              <li key={r.id} className="px-4 py-2.5 text-[14px]">
                <div className="flex justify-between"><span className="font-semibold">{formatDate(r.date)}</span><span className="text-[13px] text-secondaire">{r.par}</span></div>
                <div className="text-secondaire">{r.lignes.map((l) => `${formatQuantite(l.quantite)} ${l.unite} ${l.designation}`).join(" · ")}</div>
                {r.notes && <div className="text-[13px] text-attenue">{r.notes}</div>}
              </li>
            ))}
          </ul>
        )}
      </section>

      {d.factures !== null && <FacturesFournisseur d={d} peutEcrire={paie && !d.annule} onChange={apres} />}
      {d.paiements !== null && <Paiements d={d} peutEcrire={paie && !d.annule} onChange={apres} />}

      {reception && <Reception d={d} onFermer={() => setReception(false)} onFait={() => { setReception(false); apres(); }} />}
      {annulation && <Annulation chemin={`/negoce/achats/${d.id}/annuler`} titre="Annuler la commande" onFermer={() => setAnnulation(false)} onFait={() => { setAnnulation(false); apres(); }} />}
    </Sheet>
  );
}

function Reception({ d, onFermer, onFait }: { d: Detail; onFermer: () => void; onFait: () => void }) {
  const [date, setDate] = useState(aujourdhui());
  const [notes, setNotes] = useState("");
  const [quantites, setQuantites] = useState<Record<string, string>>({});
  const [erreur, setErreur] = useState("");
  const valider = async () => {
    setErreur("");
    try {
      const lignes = d.lignes.filter((l) => lireNombre(quantites[l.id] ?? "") > 0).map((l) => ({ ligneId: l.id, quantite: lireNombre(quantites[l.id]) }));
      await api(`/negoce/achats/${d.id}/receptions`, { method: "POST", body: JSON.stringify({ date, notes, lignes }) });
      onFait();
    } catch (e) { setErreur(messageErreur(e)); }
  };
  return (
    <Modal open onOpenChange={(o) => !o && onFermer()} large title="Enregistrer une réception" description="Indiquez ce qui est arrivé. Le reste pourra être reçu plus tard."
      footer={<><Button variant="outline" onClick={onFermer}>Annuler</Button><Button onClick={valider}>Enregistrer la réception</Button></>}>
      <Alerte message={erreur} />
      <div className="mb-4 grid gap-4 sm:grid-cols-2">
        <Field label="Date de réception"><Input type="date" value={date} onChange={(e) => setDate(e.target.value)} /></Field>
        <Field label="Notes"><Input value={notes} onChange={(e) => setNotes(e.target.value)} /></Field>
      </div>
      <ul className="space-y-2">
        {d.lignes.filter((l) => l.reste > 0).map((l) => (
          <li key={l.id} className="grid items-center gap-2 sm:grid-cols-[1fr_130px]">
            <div><div className="font-semibold">{l.designation}</div><div className="text-[13px] text-secondaire">Reste à recevoir : {formatQuantite(l.reste)} {l.unite}</div></div>
            <div className="flex gap-1.5">
              <Input aria-label={`Quantité reçue de ${l.designation}`} inputMode="decimal" value={quantites[l.id] ?? ""} onChange={(e) => setQuantites({ ...quantites, [l.id]: filtrerDecimal(e.target.value) })} />
              <Button type="button" variant="outline" size="sm" onClick={() => setQuantites({ ...quantites, [l.id]: String(l.reste).replace(".", ",") })}>Tout</Button>
            </div>
          </li>
        ))}
      </ul>
    </Modal>
  );
}

function FacturesFournisseur({ d, peutEcrire: ecrit, onChange }: { d: Detail; peutEcrire: boolean; onChange: () => void }) {
  const [f, setF] = useState({ numero: "", date: aujourdhui(), montant: "" });
  const [erreur, setErreur] = useState("");
  const ajouter = async () => {
    setErreur("");
    try {
      await api(`/negoce/achats/${d.id}/factures`, { method: "POST", body: JSON.stringify({ numero: f.numero, date: f.date, montantTtc: lireNombre(f.montant) }) });
      setF({ numero: "", date: aujourdhui(), montant: "" }); onChange();
    } catch (e) { setErreur(messageErreur(e)); }
  };
  const retirer = async (fid: string) => { try { await api(`/negoce/achats/${d.id}/factures/${fid}`, { method: "DELETE" }); onChange(); } catch (e) { setErreur(messageErreur(e)); } };
  return (
    <section className="mb-8">
      <h3 className="mb-2 font-display text-[15px] font-bold">Factures du fournisseur</h3>
      <Alerte message={erreur} />
      {d.factures!.length === 0 ? <p className="mb-3 text-secondaire">Aucune facture reçue.</p> : (
        <ul className="mb-3 space-y-3">
          {d.factures!.map((x) => (
            <li key={x.id} className="rounded-lg border border-bordure p-4">
              <div className="mb-2 flex items-center justify-between">
                <span><span className="font-display font-bold">{x.numero}</span> <span className="text-[13px] text-secondaire">{formatDate(x.date)}</span></span>
                <span className="flex items-center gap-3"><span className="font-display font-semibold">{formatDH(x.montantTtc)}</span>{ecrit && <Button variant="ghost" size="sm" onClick={() => retirer(x.id)}>Retirer</Button>}</span>
              </div>
              <Documents contexte="negoce_facture_fournisseur" cibleId={x.id} titre="Facture jointe" compact lectureSeule={!ecrit} aide="PDF ou image de la facture reçue." />
            </li>
          ))}
        </ul>
      )}
      {ecrit && (
        <div className="grid gap-2 sm:grid-cols-[1fr_150px_140px_auto]">
          <Input aria-label="Numéro de la facture" placeholder="Numéro de facture" value={f.numero} onChange={(e) => setF({ ...f, numero: e.target.value })} />
          <Input aria-label="Date" type="date" value={f.date} onChange={(e) => setF({ ...f, date: e.target.value })} />
          <Input aria-label="Montant TTC" inputMode="decimal" placeholder="Montant TTC" value={f.montant} onChange={(e) => setF({ ...f, montant: filtrerDecimal(e.target.value) })} />
          <Button variant="outline" disabled={!f.numero.trim() || !f.montant} onClick={ajouter}>Ajouter</Button>
        </div>
      )}
    </section>
  );
}

function Paiements({ d, peutEcrire: ecrit, onChange }: { d: Detail; peutEcrire: boolean; onChange: () => void }) {
  const [p, setP] = useState({ date: aujourdhui(), montant: "", mode: "virement", reference: "" });
  const [erreur, setErreur] = useState("");
  const [annule, setAnnule] = useState<string | null>(null);
  const ajouter = async () => {
    setErreur("");
    try {
      await api(`/negoce/achats/${d.id}/paiements`, { method: "POST", body: JSON.stringify({ date: p.date, montant: lireNombre(p.montant), mode: p.mode, reference: p.reference }) });
      setP({ date: aujourdhui(), montant: "", mode: "virement", reference: "" }); onChange();
    } catch (e) { setErreur(messageErreur(e)); }
  };
  return (
    <section className="mb-4">
      <div className="mb-2 flex items-baseline justify-between">
        <h3 className="font-display text-[15px] font-bold">Paiements</h3>
        {d.reste !== null && <span className="text-[13px] text-secondaire">Payé {formatDH(d.paye ?? 0)} · reste {formatDH(d.reste)}</span>}
      </div>
      <Alerte message={erreur} />
      {d.paiements!.length === 0 ? <p className="mb-3 text-secondaire">Aucun paiement.</p> : (
        <ul className="mb-3 space-y-3">
          {d.paiements!.map((x) => (
            <li key={x.id} className="rounded-lg border border-bordure p-4">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <span className={x.annule ? "text-attenue line-through" : ""}>
                  <span className="font-display font-bold">{formatDH(x.montant)}</span> <span className="text-[13px] text-secondaire">{MODES_PAIEMENT[x.mode]} · {formatDate(x.date)}{x.reference ? ` · ${x.reference}` : ""}</span>
                </span>
                {x.annule ? <Badge ton="rouge">Annulé : {x.motifAnnulation}</Badge> : ecrit && <Button variant="ghost" size="sm" onClick={() => setAnnule(x.id)}>Annuler</Button>}
              </div>
              {!x.annule && x.avecImage && <div className="mt-3"><Documents contexte="negoce_paiement_fournisseur" cibleId={x.id} titre={`Image du ${x.mode === "cheque" ? "chèque" : "l'effet"}`} compact lectureSeule={!ecrit} aide="Photo du chèque ou de l'effet remis au fournisseur." /></div>}
            </li>
          ))}
        </ul>
      )}
      {ecrit && d.reste !== null && d.reste > 0 && (
        <div className="grid gap-2 sm:grid-cols-[140px_130px_150px_1fr_auto]">
          <Input aria-label="Date" type="date" value={p.date} onChange={(e) => setP({ ...p, date: e.target.value })} />
          <Input aria-label="Montant" inputMode="decimal" placeholder="Montant TTC" value={p.montant} onChange={(e) => setP({ ...p, montant: filtrerDecimal(e.target.value) })} />
          <Select aria-label="Mode de paiement" value={p.mode} onChange={(e) => setP({ ...p, mode: e.target.value })}>{Object.entries(MODES_PAIEMENT).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</Select>
          <Input aria-label="Référence" placeholder={avecImage(p.mode) ? "Numéro du chèque ou de l'effet" : "Référence"} value={p.reference} onChange={(e) => setP({ ...p, reference: e.target.value })} />
          <Button variant="outline" disabled={!p.montant} onClick={ajouter}>Enregistrer</Button>
        </div>
      )}
      {annule && <Annulation chemin={`/negoce/achats/${d.id}/paiements/${annule}/annuler`} titre="Annuler le paiement" onFermer={() => setAnnule(null)} onFait={() => { setAnnule(null); onChange(); }} />}
    </section>
  );
}

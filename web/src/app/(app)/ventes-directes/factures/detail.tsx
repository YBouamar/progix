"use client";

import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api";
import { peutEcrire, useAuth } from "@/lib/auth";
import { LABEL_STATUT_FACTURE, MODES_PAIEMENT, avecImage, formatQuantite, tonEtatVente, tonStatutFacture } from "@/lib/negoce";
import { filtrerDecimal, formatDate, formatDH, lireNombre } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input, Select } from "@/components/ui/input";
import { Sheet } from "@/components/ui/sheet";
import { Documents } from "@/components/documents";
import { Alerte, Totaux, messageErreur } from "@/components/negoce/commun";
import { Annulation } from "@/components/negoce/annulation";
import { SignatureDoc } from "@/components/signature-doc";
import { Reprise } from "@/components/negoce/reprise";

export type Avoir = { signePar: string | null; id: string; numero: string; entite: string; client: string; date: string; etat: string; etatLabel: string; totalTtc: number; parentNumero: string | null };
type DetailF = {
  signePar: string | null; signeLe: string | null; id: string; numero: string; entite: string; client: string; date: string; echeance: string | null; etat: string; etatLabel: string; notes: string | null; motifAnnulation: string | null; parentNumero: string | null;
  totalHt: number; totalTva: number; totalTtc: number; encaisse: number; reste: number; statut: string;
  lignes: { id: string; code: string; designation: string; unite: string; quantite: number; prixUnitaire: number; montantHt: number; resteACrediter: number }[];
  paiements: { id: string; date: string; montant: number; mode: string; reference: string | null; par: string; annule: boolean; motifAnnulation: string | null; avecImage: boolean }[];
  avoirs: Avoir[];
};

const aujourdhui = () => new Date().toISOString().slice(0, 10);

/** Détail d'une facture (lignes, paiements, avoirs) : ouvert depuis la liste des factures ou depuis la fiche du projet. */
export function DetailFacture({ id, onFermer, onChange }: { id: string; onFermer: () => void; onChange: () => void }) {
  const { user } = useAuth();
  const ecrit = peutEcrire(user, "negoce_factures");
  const [d, setD] = useState<DetailF | null>(null);
  const [erreur, setErreur] = useState("");
  const [mode, setMode] = useState<"avoir" | "annulation" | null>(null);
  const [annulePaiement, setAnnulePaiement] = useState<string | null>(null);
  const [annuleAvoir, setAnnuleAvoir] = useState<string | null>(null);
  const [p, setP] = useState({ date: aujourdhui(), montant: "", mode: "virement", reference: "" });

  const charger = useCallback(() => { api<DetailF>(`/negoce/factures/${id}`).then((r) => { setD(r); setErreur(""); }).catch((e) => setErreur(messageErreur(e, "Chargement impossible."))); }, [id]);
  useEffect(() => { charger(); }, [charger]);
  const apres = () => { charger(); onChange(); };

  const signerAvoir = async (aid: string) => {
    try { await api(`/negoce/avoirs/${aid}/signer`, { method: "POST", body: "{}" }); apres(); } catch (e) { setErreur(messageErreur(e)); }
  };
  // Plafond légal des espèces : on montre ce qu'il reste possible pour le client ce mois-ci.
  const [especes, setEspeces] = useState<{ plafond: number; deja: number; disponible: number } | null>(null);
  useEffect(() => {
    if (p.mode !== "especes") { setEspeces(null); return; }
    api<{ plafond: number; deja: number; disponible: number }>(`/negoce/encaissements/especes?factureId=${id}&date=${p.date}`).then(setEspeces).catch(() => setEspeces(null));
  }, [p.mode, p.date, id, d?.encaisse]);

  const encaisser = async () => {
    setErreur("");
    try {
      await api("/negoce/encaissements", { method: "POST", body: JSON.stringify({ factureId: id, date: p.date, montant: lireNombre(p.montant), mode: p.mode, reference: p.reference }) });
      setP({ date: aujourdhui(), montant: "", mode: "virement", reference: "" }); apres();
    } catch (e) { setErreur(messageErreur(e)); }
  };

  if (!d) return <Sheet open onOpenChange={(o) => !o && onFermer()} large title="Facture" description=""><Alerte message={erreur} /></Sheet>;
  const annulee = d.etat === "annule";
  const acrediter = d.lignes.filter((l) => l.resteACrediter > 0).map((l) => ({ id: l.id, code: l.code, designation: l.designation, unite: l.unite, reste: l.resteACrediter }));
  const sansEffet = d.paiements.every((x) => x.annule) && d.avoirs.every((a) => a.etat === "annule");

  return (
    <Sheet open onOpenChange={(o) => !o && onFermer()} large title={d.numero} description={`${d.client} · ${formatDate(d.date)} · ${d.entite}`}
      footer={<>
        {ecrit && !annulee && sansEffet && <Button variant="outline" className="mr-auto text-rouge-texte" onClick={() => setMode("annulation")}>Annuler la facture</Button>}
        {ecrit && !annulee && acrediter.length > 0 && <Button variant="outline" onClick={() => setMode("avoir")}>Créer un avoir</Button>}
        <Button variant="outline" onClick={onFermer}>Fermer</Button>
      </>}>
      <Alerte message={erreur} />
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <Badge ton={tonStatutFacture(d.statut)}>{LABEL_STATUT_FACTURE[d.statut] ?? d.statut}</Badge>
        {d.parentNumero && <span className="text-[13px] text-secondaire">Commande {d.parentNumero}</span>}
        {d.echeance && <span className="text-[13px] text-secondaire">Échéance {formatDate(d.echeance)}</span>}
        {d.motifAnnulation && <span className="text-[13px] text-rouge-texte">Motif : {d.motifAnnulation}</span>}
      </div>
      <div className="overflow-x-auto rounded-lg border border-bordure">
        <table className="w-full text-left text-[14px]">
          <thead className="border-b border-bordure bg-fond text-[12px] text-secondaire"><tr><th className="px-4 py-2 font-semibold">Article</th><th className="px-4 py-2 text-right font-semibold">Quantité</th><th className="px-4 py-2 text-right font-semibold">Prix HT</th><th className="px-4 py-2 text-right font-semibold">Montant HT</th></tr></thead>
          <tbody>
            {d.lignes.map((l) => (
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
      <div className="mt-4"><Totaux ht={d.totalHt} tva={d.totalTva} ttc={d.totalTtc} /></div>
      <div className="mb-8">{!annulee && <SignatureDoc signePar={d.signePar} signeLe={d.signeLe} quoi="cette facture" url={`/negoce/factures/${d.id}`} peutSigner={ecrit} onSigne={apres} />}</div>

      {d.avoirs.length > 0 && (
        <section className="mb-8">
          <h3 className="mb-2 font-display text-[15px] font-bold">Avoirs</h3>
          <ul className="divide-y divide-bordure rounded-lg border border-bordure">
            {d.avoirs.map((a) => (
              <li key={a.id} className="flex items-center justify-between px-4 py-2 text-[14px]">
                <span><span className="font-display font-bold">{a.numero}</span> <span className="text-[13px] text-secondaire">{formatDate(a.date)}</span></span>
                <span className="flex items-center gap-3"><Badge ton={tonEtatVente(a.etat)}>{a.etatLabel}</Badge><span className="font-display">{formatDH(a.totalTtc)}</span>{a.signePar ? <a href={`/api/negoce/avoirs/${a.id}/pdf`} target="_blank" rel="noreferrer" className="text-[12px] font-semibold text-marine hover:underline">PDF</a> : ecrit && a.etat !== "annule" && <Button variant="ghost" size="sm" onClick={() => signerAvoir(a.id)}>Signer</Button>}{ecrit && a.etat !== "annule" && <Button variant="ghost" size="sm" onClick={() => setAnnuleAvoir(a.id)}>Annuler</Button>}</span>
              </li>
            ))}
          </ul>
        </section>
      )}

      <section className="mb-4">
        <div className="mb-2 flex items-baseline justify-between"><h3 className="font-display text-[15px] font-bold">Encaissements</h3><span className="text-[13px] text-secondaire">Encaissé {formatDH(d.encaisse)} · reste {formatDH(d.reste)}</span></div>
        {d.paiements.length === 0 ? <p className="mb-3 text-secondaire">Aucun encaissement.</p> : (
          <ul className="mb-3 space-y-3">
            {d.paiements.map((x) => (
              <li key={x.id} className="rounded-lg border border-bordure p-4">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className={x.annule ? "text-attenue line-through" : ""}><span className="font-display font-bold">{formatDH(x.montant)}</span> <span className="text-[13px] text-secondaire">{MODES_PAIEMENT[x.mode]} · {formatDate(x.date)}{x.reference ? ` · ${x.reference}` : ""}</span></span>
                  {x.annule ? <Badge ton="rouge">Annulé : {x.motifAnnulation}</Badge> : ecrit && !annulee && <Button variant="ghost" size="sm" onClick={() => setAnnulePaiement(x.id)}>Annuler</Button>}
                </div>
                {!x.annule && x.avecImage && <div className="mt-3"><Documents contexte="negoce_paiement_client" cibleId={x.id} titre={`Image du ${x.mode === "cheque" ? "chèque" : "l'effet"}`} compact lectureSeule={!ecrit} aide="Photo du chèque ou de l'effet reçu du client." /></div>}
              </li>
            ))}
          </ul>
        )}
        {ecrit && !annulee && d.reste > 0 && (
          <div className="grid gap-2 sm:grid-cols-[140px_130px_150px_1fr_auto]">
            <Input aria-label="Date" type="date" value={p.date} onChange={(e) => setP({ ...p, date: e.target.value })} />
            <Input aria-label="Montant" inputMode="decimal" placeholder="Montant TTC" value={p.montant} onChange={(e) => setP({ ...p, montant: filtrerDecimal(e.target.value) })} />
            <Select aria-label="Mode de paiement" value={p.mode} onChange={(e) => setP({ ...p, mode: e.target.value })}>{Object.entries(MODES_PAIEMENT).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</Select>
            <Input aria-label="Référence" placeholder={avecImage(p.mode) ? "Numéro du chèque ou de l'effet" : "Référence"} value={p.reference} onChange={(e) => setP({ ...p, reference: e.target.value })} />
            <Button variant="outline" disabled={!p.montant} onClick={encaisser}>Enregistrer</Button>
          </div>
        )}
        {ecrit && !annulee && especes && <p className="mt-2 text-[12px] text-secondaire">Espèces : {formatDH(especes.plafond)} par client et par mois, dont {formatDH(especes.deja)} déjà encaissés ce mois (ventes directes et post-tension). Il reste {formatDH(especes.disponible)} possibles.</p>}
      </section>

      {mode === "avoir" && <Reprise titre="Nouvel avoir" description="Reprenez les lignes à créditer, dans la limite de ce qui a été facturé." chemin="/negoce/avoirs" corpsDe={{ factureId: d.id }} motif libelleReste="Reste à créditer" lignes={acrediter} onFermer={() => setMode(null)} onFait={() => { setMode(null); apres(); }} />}
      {mode === "annulation" && <Annulation chemin={`/negoce/factures/${d.id}/annuler`} titre="Annuler la facture" onFermer={() => setMode(null)} onFait={() => { setMode(null); apres(); }} />}
      {annulePaiement && <Annulation chemin={`/negoce/encaissements/${annulePaiement}/annuler`} titre="Annuler l'encaissement" onFermer={() => setAnnulePaiement(null)} onFait={() => { setAnnulePaiement(null); apres(); }} />}
      {annuleAvoir && <Annulation chemin={`/negoce/avoirs/${annuleAvoir}/annuler`} titre="Annuler l'avoir" onFermer={() => setAnnuleAvoir(null)} onFait={() => { setAnnuleAvoir(null); apres(); }} />}
    </Sheet>
  );
}

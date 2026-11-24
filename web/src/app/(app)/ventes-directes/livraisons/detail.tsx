"use client";

import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api";
import { peutEcrire, useAuth } from "@/lib/auth";
import { formatQuantite, tonEtatVente } from "@/lib/negoce";
import { formatDate } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Sheet } from "@/components/ui/sheet";
import { Alerte, messageErreur } from "@/components/negoce/commun";
import { Annulation } from "@/components/negoce/annulation";
import { SignatureDoc } from "@/components/signature-doc";

type Detail = { id: string; numero: string; entite: string; client: string; date: string; etat: string; etatLabel: string; parentNumero: string | null; signePar: string | null; signeLe: string | null; notes: string | null; motifAnnulation: string | null; creePar: string; lignes: { id: string; code: string; designation: string; unite: string; quantite: number }[] };

/** Détail d'un bon de livraison : ouvert depuis la liste des livraisons ou depuis la fiche du projet. */
export function DetailLivraison({ id, onFermer, onChange }: { id: string; onFermer: () => void; onChange: () => void }) {
  const { user } = useAuth();
  const ecrit = peutEcrire(user, "negoce_livraisons");
  const [d, setD] = useState<Detail | null>(null);
  const [erreur, setErreur] = useState("");
  const [annulation, setAnnulation] = useState(false);
  const charger = useCallback(() => { api<Detail>(`/negoce/livraisons/${id}`).then((r) => { setD(r); setErreur(""); }).catch((e) => setErreur(messageErreur(e, "Chargement impossible."))); }, [id]);
  useEffect(() => { charger(); }, [charger]);

  if (!d) return <Sheet open onOpenChange={(o) => !o && onFermer()} title="Bon de livraison" description=""><Alerte message={erreur} /></Sheet>;
  return (
    <Sheet open onOpenChange={(o) => !o && onFermer()} title={d.numero} description={`${d.client} · ${formatDate(d.date)} · ${d.entite}`}
      footer={<>{ecrit && d.etat !== "annule" && <Button variant="outline" className="mr-auto text-rouge-texte" onClick={() => setAnnulation(true)}>Annuler le bon</Button>}<Button variant="outline" onClick={onFermer}>Fermer</Button></>}>
      <Alerte message={erreur} />
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <Badge ton={tonEtatVente(d.etat)}>{d.etatLabel}</Badge>
        {d.parentNumero && <span className="text-[13px] text-secondaire">Commande {d.parentNumero}</span>}
        {d.motifAnnulation && <span className="text-[13px] text-rouge-texte">Motif : {d.motifAnnulation}</span>}
        {d.notes && <span className="text-[13px] text-secondaire">{d.notes}</span>}
      </div>
      <ul className="divide-y divide-bordure rounded-lg border border-bordure">
        {d.lignes.map((l) => (
          <li key={l.id} className="flex items-center justify-between px-4 py-2.5 text-[14px]"><span><span className="font-semibold">{l.designation}</span> <span className="font-display text-[12px] text-attenue">{l.code}</span></span><span className="font-display font-bold">{formatQuantite(l.quantite)} {l.unite}</span></li>
        ))}
      </ul>
      {d.etat !== "annule" && <SignatureDoc signePar={d.signePar} signeLe={d.signeLe} quoi="ce bon de livraison" url={`/negoce/livraisons/${d.id}`} peutSigner={ecrit} onSigne={() => { charger(); onChange(); }} />}
      {annulation && <Annulation chemin={`/negoce/livraisons/${d.id}/annuler`} titre="Annuler le bon de livraison" onFermer={() => setAnnulation(false)} onFait={() => { setAnnulation(false); charger(); onChange(); }} />}
    </Sheet>
  );
}

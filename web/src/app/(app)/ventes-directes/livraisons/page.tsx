"use client";

import { useCallback, useEffect, useState } from "react";
import { Search } from "lucide-react";
import { api } from "@/lib/api";
import { peutEcrire, useAuth } from "@/lib/auth";
import { formatQuantite, tonEtatVente } from "@/lib/negoce";
import { formatDate } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input, Select } from "@/components/ui/input";
import { Sheet } from "@/components/ui/sheet";
import { Alerte, LIGNE_CLIQUABLE, TABLE_BOITE, TD, TH, THEAD, Vide, messageErreur } from "@/components/negoce/commun";
import { Annulation } from "@/components/negoce/annulation";
import { SignatureDoc } from "@/components/signature-doc";

type Ligne = { id: string; numero: string; entite: string; client: string; date: string; etat: string; etatLabel: string; parentNumero: string | null };
type Detail = Ligne & { signePar: string | null; signeLe: string | null; notes: string | null; motifAnnulation: string | null; creePar: string; lignes: { id: string; code: string; designation: string; unite: string; quantite: number }[] };

/** Bons de livraison : créés depuis une commande de vente, chacun sort du stock. L'annulation remet la quantité en stock. */
export default function LivraisonsPage() {
  const [liste, setListe] = useState<Ligne[] | null>(null);
  const [recherche, setRecherche] = useState("");
  const [etat, setEtat] = useState("");
  const [erreur, setErreur] = useState("");
  const [ouvert, setOuvert] = useState<string | null>(null);

  const charger = useCallback(() => {
    const p = new URLSearchParams();
    if (etat) p.set("etat", etat);
    if (recherche.trim()) p.set("recherche", recherche.trim());
    api<Ligne[]>(`/negoce/livraisons?${p}`).then((r) => { setListe(r); setErreur(""); }).catch((e) => setErreur(messageErreur(e, "Chargement impossible.")));
  }, [etat, recherche]);
  useEffect(() => { const t = setTimeout(charger, 250); return () => clearTimeout(t); }, [charger]);

  return (
    <div>
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <div className="relative w-full max-w-xs">
          <Search size={16} className="pointer-events-none absolute left-3 top-3 text-attenue" />
          <Input className="pl-9" placeholder="Numéro, client ou commande" value={recherche} onChange={(e) => setRecherche(e.target.value)} />
        </div>
        <Select className="w-auto" aria-label="État" value={etat} onChange={(e) => setEtat(e.target.value)}>
          <option value="">Tous les états</option><option value="emis">Émis</option><option value="annule">Annulé</option>
        </Select>
        <p className="ml-auto text-[13px] text-secondaire">Un bon se crée depuis la commande de vente.</p>
      </div>
      <Alerte message={erreur} />
      {liste && liste.length === 0 ? <Vide texte="Aucun bon de livraison." /> : (
        <div className={TABLE_BOITE}>
          <table className="w-full text-left">
            <thead className={THEAD}><tr><th className={TH}>Bon</th><th className={TH}>Client</th><th className={TH}>Commande</th><th className={TH}>État</th></tr></thead>
            <tbody>
              {liste?.map((l) => (
                <tr key={l.id} className={LIGNE_CLIQUABLE} onClick={() => setOuvert(l.id)}>
                  <td className={TD}><div className="font-display font-bold text-marine">{l.numero}</div><div className="text-[12px] text-attenue">{formatDate(l.date)} · {l.entite}</div></td>
                  <td className={TD}>{l.client}</td>
                  <td className={TD}>{l.parentNumero ?? "-"}</td>
                  <td className={TD}><Badge ton={tonEtatVente(l.etat)}>{l.etatLabel}</Badge></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {ouvert && <DetailLivraison id={ouvert} onFermer={() => setOuvert(null)} onChange={charger} />}
    </div>
  );
}

function DetailLivraison({ id, onFermer, onChange }: { id: string; onFermer: () => void; onChange: () => void }) {
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

"use client";

import { useCallback, useEffect, useState } from "react";
import { Search } from "lucide-react";
import { api } from "@/lib/api";
import { tonEtatVente } from "@/lib/negoce";
import { formatDate } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Input, Select } from "@/components/ui/input";
import { Alerte, LIGNE_CLIQUABLE, TABLE_BOITE, TD, TH, THEAD, Vide, messageErreur } from "@/components/negoce/commun";
import { DetailLivraison } from "./detail";

type Ligne = { id: string; numero: string; entite: string; client: string; date: string; etat: string; etatLabel: string; parentNumero: string | null };

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

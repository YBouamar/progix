"use client";

import { useCallback, useEffect, useState } from "react";
import { Plus, Search } from "lucide-react";
import { api } from "@/lib/api";
import { peutEcrire, useAuth } from "@/lib/auth";
import { tonEtatVente } from "@/lib/negoce";
import { formatDate, formatDH } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input, Select } from "@/components/ui/input";
import { Alerte, LIGNE_CLIQUABLE, TABLE_BOITE, TD, TH, THEAD, Vide, messageErreur } from "@/components/negoce/commun";
import { EditionDocument } from "./edition";
import { DetailDevis, DetailCommandeVente } from "./detail";

export type Resume = { id: string; numero: string; entite: string; client: string; projetCode: string | null; date: string; etat: string; etatLabel: string; totalHt: number; totalTtc: number; parentNumero: string | null };

const ETATS_DEVIS: Record<string, string> = { ouvert: "Ouvert", accepte: "Accepté", refuse: "Refusé", annule: "Annulé" };
const ETATS_COMMANDE: Record<string, string> = { ouvert: "En cours", annule: "Annulée" };

export default function DevisPage() {
  const { user } = useAuth();
  const ecrit = peutEcrire(user, "negoce_devis");
  const [onglet, setOnglet] = useState<"devis" | "commandes">("devis");
  const [liste, setListe] = useState<Resume[] | null>(null);
  const [recherche, setRecherche] = useState("");
  const [etat, setEtat] = useState("");
  const [erreur, setErreur] = useState("");
  const [creation, setCreation] = useState(false);
  const [ouvert, setOuvert] = useState<string | null>(null);

  const charger = useCallback(() => {
    const p = new URLSearchParams();
    if (etat) p.set("etat", etat);
    if (recherche.trim()) p.set("recherche", recherche.trim());
    api<Resume[]>(`/negoce/${onglet}?${p}`).then((r) => { setListe(r); setErreur(""); }).catch((e) => setErreur(messageErreur(e, "Chargement impossible.")));
  }, [onglet, etat, recherche]);
  useEffect(() => { const t = setTimeout(charger, 250); return () => clearTimeout(t); }, [charger]);

  const etats = onglet === "devis" ? ETATS_DEVIS : ETATS_COMMANDE;
  const changerOnglet = (o: "devis" | "commandes") => { setOnglet(o); setEtat(""); setListe(null); };

  return (
    <div>
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <div className="flex gap-1.5" role="tablist" aria-label="Documents">
          {(["devis", "commandes"] as const).map((o) => (
            <Button key={o} role="tab" aria-selected={onglet === o} size="sm" variant={onglet === o ? "default" : "outline"} onClick={() => changerOnglet(o)}>{o === "devis" ? "Devis" : "Commandes"}</Button>
          ))}
        </div>
        <div className="relative w-full max-w-xs">
          <Search size={16} className="pointer-events-none absolute left-3 top-3 text-attenue" />
          <Input className="pl-9" placeholder="Numéro ou client" value={recherche} onChange={(e) => setRecherche(e.target.value)} />
        </div>
        <Select className="w-auto" aria-label="État" value={etat} onChange={(e) => setEtat(e.target.value)}>
          <option value="">Tous les états</option>{Object.entries(etats).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
        </Select>
        {ecrit && <Button className="ml-auto" onClick={() => setCreation(true)}><Plus size={16} />{onglet === "devis" ? "Nouveau devis" : "Nouvelle commande"}</Button>}
      </div>
      <Alerte message={erreur} />
      {liste && liste.length === 0 ? <Vide texte={onglet === "devis" ? "Aucun devis." : "Aucune commande de vente."} /> : (
        <div className={TABLE_BOITE}>
          <table className="w-full text-left">
            <thead className={THEAD}><tr><th className={TH}>Numéro</th><th className={TH}>Client</th><th className={TH}>État</th><th className={`${TH} text-right`}>Total HT</th><th className={`${TH} text-right`}>Total TTC</th></tr></thead>
            <tbody>
              {liste?.map((l) => (
                <tr key={l.id} className={LIGNE_CLIQUABLE} onClick={() => setOuvert(l.id)}>
                  <td className={TD}><div className="font-display font-bold text-marine">{l.numero}</div><div className="text-[12px] text-attenue">{formatDate(l.date)} · {l.entite}{l.parentNumero ? ` · depuis ${l.parentNumero}` : ""}</div></td>
                  <td className={TD}>{l.client}{l.projetCode && <div className="text-[12px] text-attenue">Projet {l.projetCode}</div>}</td>
                  <td className={TD}><Badge ton={tonEtatVente(l.etat)}>{l.etatLabel}</Badge></td>
                  <td className={`${TD} text-right font-display`}>{formatDH(l.totalHt)}</td>
                  <td className={`${TD} text-right font-display font-semibold`}>{formatDH(l.totalTtc)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {creation && <EditionDocument type={onglet} onFermer={() => setCreation(false)} onFait={(id) => { setCreation(false); charger(); setOuvert(id); }} />}
      {ouvert && (onglet === "devis"
        ? <DetailDevis id={ouvert} onFermer={() => setOuvert(null)} onChange={charger} onOuvrirCommande={(id) => { changerOnglet("commandes"); setOuvert(id); }} />
        : <DetailCommandeVente id={ouvert} onFermer={() => setOuvert(null)} onChange={charger} />)}
    </div>
  );
}

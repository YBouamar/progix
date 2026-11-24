"use client";

import { useCallback, useEffect, useState } from "react";
import { Search } from "lucide-react";
import { api } from "@/lib/api";
import { LABEL_STATUT_FACTURE, tonEtatVente, tonStatutFacture } from "@/lib/negoce";
import { formatDate, formatDH } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input, Select } from "@/components/ui/input";
import { Alerte, LIGNE_CLIQUABLE, TABLE_BOITE, TD, TH, THEAD, Vide, messageErreur } from "@/components/negoce/commun";
import { DetailFacture, type Avoir } from "./detail";

type Facture = { id: string; numero: string; entite: string; client: string; projetCode: string | null; date: string; echeance: string | null; etat: string; totalHt: number; totalTtc: number; encaisse: number; reste: number; statut: string; parentNumero: string | null };


export default function FacturesPage() {
  const [onglet, setOnglet] = useState<"factures" | "avoirs">("factures");
  const [factures, setFactures] = useState<Facture[] | null>(null);
  const [avoirs, setAvoirs] = useState<Avoir[] | null>(null);
  const [recherche, setRecherche] = useState("");
  const [statut, setStatut] = useState("");
  const [erreur, setErreur] = useState("");
  const [ouvert, setOuvert] = useState<string | null>(null);

  const charger = useCallback(() => {
    const p = new URLSearchParams();
    if (recherche.trim()) p.set("recherche", recherche.trim());
    if (onglet === "factures") {
      if (statut) p.set("statut", statut);
      api<Facture[]>(`/negoce/factures?${p}`).then((r) => { setFactures(r); setErreur(""); }).catch((e) => setErreur(messageErreur(e, "Chargement impossible.")));
    } else api<Avoir[]>(`/negoce/avoirs?${p}`).then((r) => { setAvoirs(r); setErreur(""); }).catch((e) => setErreur(messageErreur(e, "Chargement impossible.")));
  }, [onglet, statut, recherche]);
  useEffect(() => { const t = setTimeout(charger, 250); return () => clearTimeout(t); }, [charger]);

  return (
    <div>
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <div className="flex gap-1.5" role="tablist" aria-label="Documents">
          {(["factures", "avoirs"] as const).map((o) => <Button key={o} role="tab" aria-selected={onglet === o} size="sm" variant={onglet === o ? "default" : "outline"} onClick={() => setOnglet(o)}>{o === "factures" ? "Factures" : "Avoirs"}</Button>)}
        </div>
        <div className="relative w-full max-w-xs">
          <Search size={16} className="pointer-events-none absolute left-3 top-3 text-attenue" />
          <Input className="pl-9" placeholder="Numéro, client ou commande" value={recherche} onChange={(e) => setRecherche(e.target.value)} />
        </div>
        {onglet === "factures" && (
          <Select className="w-auto" aria-label="Statut" value={statut} onChange={(e) => setStatut(e.target.value)}>
            <option value="">Tous les statuts</option>{Object.entries(LABEL_STATUT_FACTURE).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </Select>
        )}
        <p className="ml-auto text-[13px] text-secondaire">Une facture se crée depuis la commande de vente, un avoir depuis la facture.</p>
      </div>
      <Alerte message={erreur} />
      {onglet === "factures" ? (
        factures && factures.length === 0 ? <Vide texte="Aucune facture." /> : (
          <div className={TABLE_BOITE}>
            <table className="w-full text-left">
              <thead className={THEAD}><tr><th className={TH}>Facture</th><th className={TH}>Client</th><th className={TH}>Statut</th><th className={`${TH} text-right`}>Total TTC</th><th className={`${TH} text-right`}>Reste à payer</th></tr></thead>
              <tbody>
                {factures?.map((f) => (
                  <tr key={f.id} className={LIGNE_CLIQUABLE} onClick={() => setOuvert(f.id)}>
                    <td className={TD}><div className="font-display font-bold text-marine">{f.numero}</div><div className="text-[12px] text-attenue">{formatDate(f.date)} · {f.entite}{f.parentNumero ? ` · ${f.parentNumero}` : ""}</div></td>
                    <td className={TD}>{f.client}</td>
                    <td className={TD}><Badge ton={tonStatutFacture(f.statut)}>{LABEL_STATUT_FACTURE[f.statut] ?? f.statut}</Badge></td>
                    <td className={`${TD} text-right font-display`}>{formatDH(f.totalTtc)}</td>
                    <td className={`${TD} text-right font-display font-semibold`}>{f.reste > 0 ? formatDH(f.reste) : "-"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )
      ) : avoirs && avoirs.length === 0 ? <Vide texte="Aucun avoir." /> : (
        <div className={TABLE_BOITE}>
          <table className="w-full text-left">
            <thead className={THEAD}><tr><th className={TH}>Avoir</th><th className={TH}>Client</th><th className={TH}>Facture</th><th className={TH}>État</th><th className={`${TH} text-right`}>Total TTC</th></tr></thead>
            <tbody>
              {avoirs?.map((a) => (
                <tr key={a.id} className="border-b border-bordure last:border-0">
                  <td className={TD}><div className="font-display font-bold text-marine">{a.numero}</div><div className="text-[12px] text-attenue">{formatDate(a.date)} · {a.entite}</div></td>
                  <td className={TD}>{a.client}</td><td className={TD}>{a.parentNumero ?? "-"}</td>
                  <td className={TD}><Badge ton={tonEtatVente(a.etat)}>{a.etatLabel}</Badge></td>
                  <td className={`${TD} text-right font-display`}>{formatDH(a.totalTtc)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {ouvert && <DetailFacture id={ouvert} onFermer={() => setOuvert(null)} onChange={charger} />}
    </div>
  );
}

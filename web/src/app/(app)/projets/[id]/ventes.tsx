"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { LABEL_STATUT_FACTURE, MODES_PAIEMENT, tonEtatVente, tonStatutFacture } from "@/lib/negoce";
import { formatDate, formatDH } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";

type Doc = { id: string; numero: string; date: string; etat: string; etatLabel: string; totalTtc: number; parentNumero: string | null };
type Facture = { resume: Doc; encaisse: number; reste: number; statut: string };
type Paiement = { id: string; facture: string; date: string; montant: number; mode: string; reference: string | null; annule: boolean };
type Ventes = { devis: Doc[] | null; commandes: Doc[] | null; livraisons: Doc[] | null; factures: Facture[] | null; avoirs: Doc[] | null; paiements: Paiement[] | null };

/** Onglet « Ventes directes » de la fiche projet : devis, commandes, bons de livraison, factures et paiements rattachés au projet (lecture seule). */
export function VentesDirectesProjet({ projetId }: { projetId: string }) {
  const [v, setV] = useState<Ventes | null>(null);
  const [erreur, setErreur] = useState("");
  useEffect(() => { api<Ventes>(`/projets/${projetId}/ventes-directes`).then(setV).catch(() => setErreur("Chargement impossible.")); }, [projetId]);

  if (erreur) return <p className="text-rouge-texte">{erreur}</p>;
  if (!v) return <p className="text-secondaire">Chargement…</p>;
  const vide = [v.devis, v.commandes, v.livraisons, v.factures, v.avoirs].every((l) => !l || l.length === 0);
  if (vide) return <p className="text-secondaire">Aucune vente directe n&apos;est rattachée à ce projet. Choisissez ce projet en créant un devis dans « Ventes directes ».</p>;

  return (
    <div className="grid gap-6">
      <Bloc titre="Devis" docs={v.devis} />
      <Bloc titre="Commandes" docs={v.commandes} />
      <Bloc titre="Bons de livraison" docs={v.livraisons} />
      {v.factures && v.factures.length > 0 && (
        <Section titre="Factures">
          {v.factures.map((f) => (
            <Ligne key={f.resume.id} numero={f.resume.numero} date={f.resume.date} note={f.resume.parentNumero ? `Issue de ${f.resume.parentNumero}` : null}>
              {f.resume.etat === "annule" ? <Badge ton="rouge">Annulée</Badge> : <Badge ton={tonStatutFacture(f.statut)}>{LABEL_STATUT_FACTURE[f.statut] ?? f.statut}</Badge>}
              <span className="font-display">{formatDH(f.resume.totalTtc)}</span>
              {f.reste > 0 && <span className="text-[13px] text-secondaire">reste {formatDH(f.reste)}</span>}
            </Ligne>
          ))}
        </Section>
      )}
      <Bloc titre="Avoirs" docs={v.avoirs} />
      {v.paiements && v.paiements.length > 0 && (
        <Section titre="Paiements">
          {v.paiements.map((p) => (
            <Ligne key={p.id} numero={p.facture} date={p.date} note={`${MODES_PAIEMENT[p.mode] ?? p.mode}${p.reference ? ` · ${p.reference}` : ""}`}>
              {p.annule && <Badge ton="rouge">Annulé</Badge>}
              <span className={`font-display ${p.annule ? "line-through text-attenue" : ""}`}>{formatDH(p.montant)}</span>
            </Ligne>
          ))}
        </Section>
      )}
    </div>
  );
}

function Bloc({ titre, docs }: { titre: string; docs: Doc[] | null }) {
  if (!docs || docs.length === 0) return null;
  return (
    <Section titre={titre}>
      {docs.map((d) => (
        <Ligne key={d.id} numero={d.numero} date={d.date} note={d.parentNumero ? `Issu de ${d.parentNumero}` : null}>
          <Badge ton={tonEtatVente(d.etat)}>{d.etatLabel}</Badge>
          <span className="font-display">{formatDH(d.totalTtc)}</span>
        </Ligne>
      ))}
    </Section>
  );
}

function Section({ titre, children }: { titre: string; children: React.ReactNode }) {
  return (
    <section>
      <h3 className="mb-2 font-display text-[15px] font-bold">{titre}</h3>
      <ul className="divide-y divide-bordure rounded-lg border border-bordure bg-white">{children}</ul>
    </section>
  );
}

function Ligne({ numero, date, note, children }: { numero: string; date: string; note: string | null; children: React.ReactNode }) {
  return (
    <li className="flex flex-wrap items-center justify-between gap-2 px-4 py-2 text-[14px]">
      <span><span className="font-display font-bold">{numero}</span> <span className="text-[13px] text-secondaire">{formatDate(date)}{note ? ` · ${note}` : ""}</span></span>
      <span className="flex items-center gap-3">{children}</span>
    </li>
  );
}

"use client";

import { use, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { ArrowLeft } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn, formatDate, formatDateHeure, formatSurface, formatNombre } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";

type Tendon = { numero: string; torons: number; sens: string; longueur: number; ancrages: number };
type Accessoire = { libelle: string; unite: string | null; quantite: number };
type Detail = {
  dalleId: string; projetId: string; reference: string; projet: string; client: string;
  entite: string; entiteLabel: string; adresse: string | null; ville: string | null;
  bloc: string | null; immeuble: string | null; niveau: string; surface: number; detailDalle: string | null;
  demandee: string; demandeePar: string | null;
  validationTechniquePar: string | null; validationTechniqueLe: string | null;
  validationAdministrativePar: string | null; validationAdministrativeLe: string | null;
  fabricationLe: string | null; livreeLe: string | null; recueLe: string | null; suiviPar: string | null;
  etat: string; etatLabel: string;
  tendons: Tendon[]; accessoires: Accessoire[];
};

/** Fiche isolée d'une commande usine, ouverte depuis la liste de la logistique, sans passer par la fiche projet. */
export default function FicheCommande({ params }: { params: Promise<{ dalleId: string }> }) {
  const { dalleId } = use(params);
  const [d, setD] = useState<Detail | null>(null);
  const [erreur, setErreur] = useState("");

  useEffect(() => {
    api<Detail>(`/logistique/commandes/${dalleId}`).then(setD)
      .catch((e) => setErreur(e instanceof ApiError && e.status === 404 ? "Commande introuvable." : "Chargement impossible."));
  }, [dalleId]);

  const calcul = useMemo(() => {
    if (!d) return null;
    const parSysteme: Record<number, { tendons: number; gaine: number; ancrages: number }> = {};
    let gaine = 0, torons = 0, x = 0, y = 0;
    for (const t of d.tendons) {
      gaine += t.longueur; torons += t.longueur * t.torons;
      if (t.sens === "Y") y++; else x++;
      parSysteme[t.torons] ??= { tendons: 0, gaine: 0, ancrages: 0 };
      parSysteme[t.torons].tendons++; parSysteme[t.torons].gaine += t.longueur; parSysteme[t.torons].ancrages += t.ancrages;
    }
    return { parSysteme, gaine, torons, x, y };
  }, [d]);

  if (!d) {
    return (
      <div>
        <Retour />
        <div className="rounded-xl border border-bordure bg-white p-12 text-center text-secondaire">{erreur || "Chargement..."}</div>
      </div>
    );
  }

  const validations: [string, string][] = [
    ["Validation technique", d.validationTechniqueLe ? `${formatDateHeure(d.validationTechniqueLe)}, par ${d.validationTechniquePar}` : "En attente"],
    ["Validation administrative", d.validationAdministrativeLe ? `${formatDateHeure(d.validationAdministrativeLe)}, par ${d.validationAdministrativePar}` : "En attente"],
  ];
  const suivi: [string, string | null][] = [
    ["En fabrication depuis", d.fabricationLe ? formatDateHeure(d.fabricationLe) : null],
    ["Livrée le", d.livreeLe ? formatDateHeure(d.livreeLe) : null],
    ["Reçue le", d.recueLe ? formatDateHeure(d.recueLe) : null],
  ];

  return (
    <div>
      <Retour />
      <div className="rounded-xl border border-bordure bg-white p-6">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-display text-[15px] font-bold text-marine">{d.reference}</span>
              <Badge ton={d.etat === "recue" ? "vert" : d.etat === "attente" ? "neutre" : "orange"}>{d.etatLabel}</Badge>
              <Badge>{d.entite} · {d.entiteLabel}</Badge>
            </div>
            <h2 className="mt-2 text-2xl font-bold">Commande usine</h2>
            <div className="mt-1 text-secondaire">
              <Link href={`/projets/${d.projetId}`} className="font-semibold text-marine hover:underline">{d.projet}</Link>
              {" · "}{d.client}
            </div>
          </div>
        </div>
      </div>

      <div className="mt-5 grid gap-5 lg:grid-cols-2">
        <div className="rounded-xl border border-bordure bg-white p-6">
          <h3 className="mb-4 text-[15px] font-bold">Dalle</h3>
          <dl className="grid gap-x-6 gap-y-4 sm:grid-cols-2">
            <Info label="Niveau" value={d.niveau} />
            <Info label="Bloc / immeuble" value={[d.bloc, d.immeuble].filter(Boolean).join(" · ") || "Non renseigné"} />
            <Info label="Surface" value={formatSurface(d.surface)} />
            <Info label="Adresse du projet" value={[d.adresse, d.ville].filter(Boolean).join(", ") || "Non renseignée"} />
            {d.detailDalle && <Info label="Détail" value={d.detailDalle} className="sm:col-span-2" />}
          </dl>
        </div>

        <div className="rounded-xl border border-bordure bg-white p-6">
          <h3 className="mb-4 text-[15px] font-bold">Validations et suivi</h3>
          <dl className="grid gap-x-6 gap-y-4 sm:grid-cols-2">
            <Info label="Demandée" value={`${formatDate(d.demandee)}, par ${d.demandeePar}`} className="sm:col-span-2" />
            {validations.map(([k, v]) => <Info key={k} label={k} value={v} />)}
            {suivi.filter(([, v]) => v).map(([k, v]) => <Info key={k} label={k} value={v!} />)}
            {d.suiviPar && <Info label="Suivi logistique par" value={d.suiviPar} />}
          </dl>
        </div>
      </div>

      <div className="mt-5 rounded-xl border border-bordure bg-white p-6">
        <div className="mb-4 flex flex-wrap items-baseline justify-between gap-2">
          <h3 className="text-[15px] font-bold">Tendons</h3>
          {calcul && d.tendons.length > 0 && (
            <div className="text-[13px] text-secondaire">
              {d.tendons.length} tendons · {calcul.x} en X, {calcul.y} en Y · {formatNombre(calcul.gaine)} m de gaine · {formatNombre(calcul.torons)} m de torons
            </div>
          )}
        </div>
        <div className="overflow-x-auto rounded-md border border-bordure">
          <table className="w-full text-left text-[13px]">
            <thead className="bg-fond text-[11px] uppercase tracking-wide text-secondaire">
              <tr>
                <th className="px-3 py-2 font-semibold">N° tendon</th>
                <th className="px-3 py-2 font-semibold">Système</th>
                <th className="px-3 py-2 font-semibold">Orientation</th>
                <th className="px-3 py-2 text-right font-semibold">Longueur m</th>
                <th className="px-3 py-2 text-right font-semibold">Ancrages</th>
                <th className="px-3 py-2 text-right font-semibold">Torons m</th>
              </tr>
            </thead>
            <tbody>
              {d.tendons.length === 0 && <tr><td colSpan={6} className="px-3 py-6 text-center text-secondaire">Aucun tendon saisi.</td></tr>}
              {d.tendons.map((t, i) => (
                <tr key={i} className="border-t border-bordure">
                  <td className="px-3 py-1.5 font-display font-bold text-marine">{t.numero}</td>
                  <td className="px-3 py-1.5 font-display">S{t.torons}</td>
                  <td className="px-3 py-1.5">{t.sens}</td>
                  <td className="px-3 py-1.5 text-right font-display">{formatNombre(t.longueur)}</td>
                  <td className="px-3 py-1.5 text-right font-display">{t.ancrages}</td>
                  <td className="px-3 py-1.5 text-right font-display text-secondaire">{formatNombre(t.longueur * t.torons)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {calcul && Object.keys(calcul.parSysteme).length > 0 && (
          <p className="mt-2 text-[12px] text-attenue">
            Par système : {Object.entries(calcul.parSysteme).map(([t, v]) => `S${t} : ${v.tendons} tendons, ${v.ancrages} ancrages`).join(" · ")}
          </p>
        )}
      </div>

      <div className="mt-5 rounded-xl border border-bordure bg-white p-6">
        <h3 className="mb-3 text-[15px] font-bold">Accessoires</h3>
        {d.accessoires.length === 0 ? (
          <div className="text-[13px] text-secondaire">Aucun accessoire dans cette commande.</div>
        ) : (
          <ul className="divide-y divide-bordure text-[13px]">
            {d.accessoires.map((a, i) => (
              <li key={i} className="flex items-center gap-2 py-2">
                <span className="w-16 shrink-0 text-right font-display font-bold">{a.quantite}</span>
                <span className="w-16 shrink-0 text-secondaire">{a.unite}</span>
                <span className="flex-1 font-semibold">{a.libelle}</span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

function Info({ label, value, className }: { label: string; value: string; className?: string }) {
  return (
    <div className={className}>
      <dt className="text-[12px] font-semibold uppercase tracking-wide text-attenue">{label}</dt>
      <dd className={cn("mt-0.5")}>{value}</dd>
    </div>
  );
}

function Retour() {
  return (
    <Link href="/logistique" className="mb-4 inline-flex items-center gap-1.5 text-[13px] font-semibold text-secondaire hover:text-marine">
      <ArrowLeft size={15} />Toutes les commandes
    </Link>
  );
}

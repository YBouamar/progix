"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { api } from "@/lib/api";
import { cn } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { TON_STATUT } from "../projets/types";

type Projet = { id: string; code: string; libelle: string; client: string; statut: string; statutLabel: string; bloque: boolean; dalles: number; realisees: number };
type Dalle = { id: string; projetId: string; reference: string; projet: string; emplacement: string; etape: number; etapeLabel: string; faites: number; verrou: string | null; aFaire: boolean };
type Vue = { perimetre: string; nbProjets: number; nbDalles: number; dallesRealisees: number; dallesEnCours: number; enRealisation: number; actions: number; projets: Projet[]; dalles: Dalle[] };

/** Onze segments : étapes faites en vert, étape courante en marine (rouge si elle est verrouillée). */
function Progression({ faites, verrou }: { faites: number; verrou?: boolean }) {
  return (
    <span className="flex gap-0.5" role="img" aria-label={`${faites} étapes faites sur 11`}>
      {Array.from({ length: 11 }, (_, i) => <span key={i} className={cn("h-2 w-4 rounded-sm", i < faites ? "bg-vert-texte" : i === faites ? (verrou ? "bg-rouge-texte" : "bg-marine") : "bg-bordure")} />)}
    </span>
  );
}

function Tuiles({ items }: { items: [string, string | number][] }) {
  return (
    <div className="mb-5 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      {items.map(([l, v]) => <div key={l} className="rounded-xl border border-bordure bg-white p-5"><div className="text-[13px] text-secondaire">{l}</div><div className="mt-1 font-display text-2xl font-bold">{v}</div></div>)}
    </div>
  );
}

function MesProjets({ v, client }: { v: Vue; client?: boolean }) {
  return (
    <div className="overflow-x-auto rounded-xl border border-bordure bg-white">
      <div className="flex items-center justify-between gap-3 border-b border-bordure px-5 py-3">
        <div className="font-display font-bold">Mes projets</div><div className="text-[13px] text-secondaire">{v.perimetre} · aucun montant sur ce tableau de bord</div>
      </div>
      {v.projets.length === 0 ? <div className="px-5 py-8 text-center text-[13px] text-secondaire">Aucun projet pour le moment.</div> : (
        <table className="w-full text-left text-[13px]">
          <thead className="border-b border-bordure bg-fond text-[12px] uppercase tracking-wide text-secondaire">
            <tr><th className="px-5 py-2.5 font-semibold">Code</th><th className="px-5 py-2.5 font-semibold">Libellé</th>{client && <th className="px-5 py-2.5 font-semibold">Client</th>}<th className="px-5 py-2.5 font-semibold">Statut</th><th className="px-5 py-2.5 font-semibold">Dalles réalisées</th></tr>
          </thead>
          <tbody>
            {v.projets.map((p) => (
              <tr key={p.id} className="border-b border-bordure last:border-0">
                <td className="whitespace-nowrap px-5 py-2.5"><Link href={`/projets/${p.id}`} className="font-display font-bold text-marine hover:underline">{p.code}</Link></td>
                <td className="px-5 py-2.5 font-semibold">{p.libelle}</td>
                {client && <td className="px-5 py-2.5 text-secondaire">{p.client}</td>}
                <td className="px-5 py-2.5"><Badge ton={TON_STATUT[p.statut] ?? "neutre"}>{p.statutLabel}</Badge>{p.bloque && <Badge ton="rouge" className="ml-1.5">Bloqué</Badge>}</td>
                <td className="px-5 py-2.5">
                  <div className="flex items-center gap-3">
                    <span className="h-2 w-32 overflow-hidden rounded-full bg-fond"><span className="block h-full rounded-full bg-vert-texte" style={{ width: `${p.dalles ? p.realisees / p.dalles * 100 : 0}%` }} /></span>
                    <span className="font-display">{p.realisees} sur {p.dalles}</span>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

export function TableauPersonnel({ variante }: { variante: "etudes" | "chantier" | "agent" }) {
  const [v, setV] = useState<Vue | null>(null);
  const [erreur, setErreur] = useState(false);
  useEffect(() => { api<Vue>("/tableau-de-bord/mes-projets").then(setV).catch(() => setErreur(true)); }, []);
  if (erreur) return <div role="alert" className="rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">Chargement du tableau de bord impossible.</div>;
  if (!v) return null;
  const aFaire = v.dalles.filter((d) => d.aFaire);

  if (variante === "agent") return (
    <div>
      <p className="mb-5 rounded-lg bg-fond px-4 py-3 text-[13px] text-secondaire">Consultation seule. Vous voyez l&apos;avancement des projets que vous avez apportés, sans les montants.</p>
      <div className="mb-5 grid gap-4 sm:grid-cols-3">
        {([["Projets apportés", v.nbProjets], ["En réalisation", v.enRealisation], ["Dalles réalisées", `${v.dallesRealisees} sur ${v.nbDalles}`]] as [string, string | number][]).map(([l, x]) => (
          <div key={l} className="rounded-xl border border-bordure bg-white p-5"><div className="text-[13px] text-secondaire">{l}</div><div className="mt-1 font-display text-2xl font-bold">{x}</div></div>
        ))}
      </div>
      <MesProjets v={v} client />
    </div>
  );

  if (variante === "chantier") {
    const prochaines = v.dalles.filter((d) => d.aFaire || (d.verrou && [3, 5, 6, 7, 8, 9, 10, 11].includes(d.etape)));
    return (
      <div>
        <Tuiles items={[["Mes projets", v.nbProjets], ["Dalles en cours", v.dallesEnCours], ["Dalles réalisées", v.dallesRealisees], ["Étapes à réaliser", v.actions]]} />
        <div className="mb-5 rounded-xl border border-bordure bg-white">
          <div className="border-b border-bordure px-5 py-3 font-display font-bold">Prochaines étapes à réaliser</div>
          {prochaines.length === 0 ? <div className="px-5 py-8 text-center text-[13px] text-secondaire">Aucune étape de chantier en attente.</div> : (
            <ul className="divide-y divide-bordure">
              {prochaines.map((d) => (
                <li key={d.id} className="flex flex-wrap items-center gap-4 px-5 py-3.5">
                  <div className="min-w-0 flex-1">
                    <div className="font-display font-bold text-marine">{d.reference}</div>
                    <div className="text-[13px] text-secondaire">{d.projet}{d.emplacement ? ` · ${d.emplacement}` : ""}</div>
                    <div className="mt-2 flex items-center gap-3"><Progression faites={d.faites} verrou={!!d.verrou} /><span className="text-[12px] text-secondaire">Étape {d.etape} sur 11</span></div>
                  </div>
                  <div className="w-56">
                    <div className="font-semibold">{d.etapeLabel}</div>
                    {d.verrou && <div className="text-[12px] text-rouge-texte">{d.verrou}</div>}
                  </div>
                  <Link href={`/projets/${d.projetId}`} className={cn("rounded-lg border px-4 py-2.5 text-[13px] font-semibold", d.verrou ? "border-bordure-forte bg-white hover:bg-fond" : "border-marine bg-marine text-white")}>{d.verrou ? "Voir" : "Commencer"}</Link>
                </li>
              ))}
            </ul>
          )}
        </div>
        <MesProjets v={v} />
      </div>
    );
  }

  return (
    <div>
      <Tuiles items={[["Mes projets", v.nbProjets], ["Mes dalles", v.nbDalles], ["Dalles réalisées", v.dallesRealisees], ["Actions en attente", v.actions]]} />
      <div className="mb-5 rounded-xl border border-bordure bg-white">
        <div className="border-b border-bordure px-5 py-3 font-display font-bold">En attente de votre action</div>
        {aFaire.length === 0 ? <div className="px-5 py-8 text-center text-[13px] text-secondaire">Aucune action en attente.</div> : (
          <ul className="divide-y divide-bordure">
            {aFaire.map((d) => (
              <li key={d.id} className="flex flex-wrap items-center gap-3 px-5 py-3 text-[13px]">
                <span className="font-semibold">{d.etape} · {d.etapeLabel}</span>
                <span className="font-display font-bold text-marine">{d.reference}</span>
                <span className="flex-1 text-secondaire">· {d.projet}{d.emplacement ? ` · ${d.emplacement}` : ""}</span>
                <Link href={`/projets/${d.projetId}`} className="font-semibold text-marine hover:underline">Ouvrir la dalle</Link>
              </li>
            ))}
          </ul>
        )}
      </div>
      <div className="mb-5"><MesProjets v={v} /></div>
      <div className="overflow-x-auto rounded-xl border border-bordure bg-white">
        <div className="border-b border-bordure px-5 py-3 font-display font-bold">Mes dalles en cours</div>
        {v.dalles.length === 0 ? <div className="px-5 py-8 text-center text-[13px] text-secondaire">Aucune dalle en cours.</div> : (
          <table className="w-full text-left text-[13px]">
            <thead className="border-b border-bordure bg-fond text-[12px] uppercase tracking-wide text-secondaire">
              <tr><th className="px-5 py-2.5 font-semibold">Référence</th><th className="px-5 py-2.5 font-semibold">Étape courante</th><th className="px-5 py-2.5 font-semibold">Progression sur 11 étapes</th></tr>
            </thead>
            <tbody>
              {v.dalles.map((d) => (
                <tr key={d.id} className="border-b border-bordure last:border-0">
                  <td className="whitespace-nowrap px-5 py-2.5"><Link href={`/projets/${d.projetId}`} className="font-display font-bold text-marine hover:underline">{d.reference}</Link></td>
                  <td className="px-5 py-2.5">{d.etape} · {d.etapeLabel}{d.verrou && <span className="text-[12px] text-rouge-texte"> · {d.verrou}</span>}</td>
                  <td className="px-5 py-2.5"><Progression faites={d.faites} verrou={!!d.verrou} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}

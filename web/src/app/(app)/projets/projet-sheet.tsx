"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { api, ApiError } from "@/lib/api";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/input";
import { Sheet } from "@/components/ui/sheet";
import { PartenaireCombo } from "@/components/partenaire-combo";
import { PartenaireSheet } from "../clients/partenaire-sheet";
import type { Compteur } from "../clients/types";
import type { ProjetDetail, Referentiel } from "./types";

type Doublon = { doublon: boolean; id?: string; code?: string; client?: string; statut?: string };

export function ProjetSheet({ projet, referentiel, onClose, onSaved }: {
  projet: ProjetDetail | null; referentiel: Referentiel; onClose: () => void; onSaved: (p: ProjetDetail) => void;
}) {
  const [libelle, setLibelle] = useState(projet?.libelle ?? "");
  const [client, setClient] = useState<{ id: string | null; texte: string }>({ id: projet?.clientId ?? null, texte: projet?.client ?? "" });
  const [proprio, setProprio] = useState<{ id: string | null; texte: string }>({ id: projet?.proprietaireId ?? null, texte: projet?.proprietaire ?? "" });
  const [entite, setEntite] = useState(projet?.entite ?? "APT");
  const [type, setType] = useState(projet?.typeConstruction ?? "immeuble");
  const [description, setDescription] = useState(projet?.description ?? "");
  const [adresse, setAdresse] = useState(projet?.adresse ?? "");
  const [ville, setVille] = useState(projet?.ville ?? "");
  const [dateCreation, setDateCreation] = useState(projet?.dateCreation ?? new Date().toLocaleDateString("fr-CA"));
  const [erreurs, setErreurs] = useState<Record<string, string[]>>({});
  const [message, setMessage] = useState("");
  const [doublon, setDoublon] = useState<Doublon | null>(null);
  const [envoi, setEnvoi] = useState(false);
  const [nouveauClient, setNouveauClient] = useState<Compteur[] | null>(null);

  useEffect(() => {
    const l = libelle.trim();
    if (l.length < 3) { setDoublon(null); return; }
    const t = setTimeout(() => {
      const p = new URLSearchParams({ libelle: l });
      if (projet) p.set("sauf", projet.id);
      api<Doublon>(`/projets/doublon?${p}`).then(setDoublon).catch(() => setDoublon(null));
    }, 350);
    return () => clearTimeout(t);
  }, [libelle, projet]);

  const err = (k: string) => erreurs[k]?.[0];
  const forfait = referentiel.types.find((t) => t.code === type)?.forfait;

  async function enregistrer() {
    setEnvoi(true); setErreurs({}); setMessage("");
    try {
      const corps = JSON.stringify({
        libelle, clientId: client.id ?? "00000000-0000-0000-0000-000000000000",
        proprietaireId: proprio.id, proprietaireLibre: proprio.id ? null : proprio.texte,
        entite, typeConstruction: type, description, adresse, ville, dateCreation: dateCreation || null,
      });
      onSaved(await api<ProjetDetail>(projet ? `/projets/${projet.id}` : "/projets", { method: projet ? "PUT" : "POST", body: corps }));
    } catch (e) {
      if (e instanceof ApiError) { setErreurs(e.errors ?? {}); setMessage(e.errors ? "Corrigez les champs signalés." : e.message); }
    } finally {
      setEnvoi(false);
    }
  }

  async function ouvrirNouveauClient() {
    setNouveauClient(await api<Compteur[]>("/partenaires/compteurs").catch(() => []));
  }

  return (
    <>
      <Sheet open onOpenChange={(o) => !o && onClose()}
        title={projet ? "Modifier le projet" : "Nouveau projet"}
        description={projet ? projet.code : "Le code sera attribué à la création, avec l'année de la date de création."}
        footer={
          <>
            <Button variant="outline" onClick={onClose}>Annuler</Button>
            <Button onClick={enregistrer} disabled={envoi}>{envoi ? "Enregistrement..." : projet ? "Enregistrer" : "Créer le projet"}</Button>
          </>
        }>
        {message && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{message}</div>}
        <div className="space-y-5">
          <Field label="Libellé du projet" required error={err("libelle")}>
            <Input value={libelle} aria-invalid={!!err("libelle")} onChange={(e) => setLibelle(e.target.value)} />
          </Field>
          {doublon?.doublon && (
            <div role="status" className="-mt-2 rounded-lg bg-orange-fond px-4 py-3 text-orange-texte">
              <div className="font-semibold">Un projet porte déjà ce nom.</div>
              <div className="text-[13px]">
                <span className="font-display">{doublon.code}</span>, {doublon.client}, {doublon.statut?.toLowerCase()}. Vous pouvez quand même {projet ? "enregistrer" : "créer ce projet"}.
              </div>
              <Link href={`/projets/${doublon.id}`} className="mt-1 inline-block text-[13px] font-semibold underline">Ouvrir le projet existant</Link>
            </div>
          )}

          <div>
            <div className="mb-1.5 flex items-center justify-between">
              <span className="text-[13px] font-semibold text-secondaire">Client<span className="text-rouge-vif"> *</span></span>
              <button type="button" onClick={ouvrirNouveauClient} className="cursor-pointer text-[13px] font-semibold text-marine hover:underline">Créer un client</button>
            </div>
            <PartenaireCombo id={client.id} texte={client.texte} invalid={!!err("clientId")} placeholder="Rechercher un client"
              onChange={(id, texte) => setClient({ id, texte })} />
            {err("clientId") && <span className="mt-1 block text-[12px] text-rouge-texte">{err("clientId")}</span>}
          </div>

          <div className="grid grid-cols-2 gap-4">
            <Field label="Adresse du projet" className="col-span-2">
              <Input value={adresse} onChange={(e) => setAdresse(e.target.value)} />
            </Field>
            <Field label="Ville">
              <Input value={ville} onChange={(e) => setVille(e.target.value)} />
            </Field>
            <Field label="Date de création" required error={err("dateCreation")}>
              <Input type="date" disabled={!!projet} value={dateCreation} aria-invalid={!!err("dateCreation")}
                onChange={(e) => setDateCreation(e.target.value)} />
            </Field>
          </div>

          <Field label="Propriétaire">
            <PartenaireCombo libre id={proprio.id} texte={proprio.texte} placeholder="Un client de la liste ou un nom saisi librement"
              onChange={(id, texte) => setProprio({ id, texte })} />
          </Field>

          <div>
            <span className="mb-1.5 block text-[13px] font-semibold text-secondaire">Entité émettrice<span className="text-rouge-vif"> *</span></span>
            <div className="grid grid-cols-2 gap-3">
              {referentiel.entites.map((e) => (
                <button key={e.code} type="button" disabled={!!projet} onClick={() => setEntite(e.code)}
                  className={cn("cursor-pointer rounded-lg border p-3 text-left disabled:cursor-not-allowed",
                    entite === e.code ? "border-marine bg-marine/5 ring-1 ring-marine" : "border-bordure-forte", projet && entite !== e.code && "opacity-50")}>
                  <div className="font-semibold">{e.label}</div>
                  <div className="font-display text-[12px] text-secondaire">{e.code} · TVA 20 %</div>
                </button>
              ))}
            </div>
            <p className="mt-1.5 text-[12px] text-attenue">
              {projet ? "L'entité émettrice ne peut plus être modifiée : elle figure dans le code du projet."
                : "Le logo, la raison sociale et les mentions légales de l'entité figureront sur tous les documents du projet."}
            </p>
          </div>

          <div>
            <span className="mb-1.5 block text-[13px] font-semibold text-secondaire">Type de construction<span className="text-rouge-vif"> *</span></span>
            <div className="grid grid-cols-4 gap-1 rounded-lg border border-bordure-forte p-0.5">
              {referentiel.types.map((t) => (
                <button key={t.code} type="button" onClick={() => setType(t.code)}
                  className={cn("h-9 cursor-pointer rounded-md text-[13px] font-semibold", type === t.code ? "bg-marine text-white" : "text-secondaire")}>
                  {t.label}
                </button>
              ))}
            </div>
            {forfait && <p className="mt-1.5 text-[12px] text-attenue">Forfait appliqué au prix : {forfait}.</p>}
          </div>

          <Field label="Description">
            <textarea rows={4} value={description} onChange={(e) => setDescription(e.target.value)}
              className="w-full rounded-lg border border-bordure-forte bg-white px-3 py-2 text-sm focus:border-marine focus:outline-none focus:ring-2 focus:ring-marine/15" />
          </Field>
        </div>
      </Sheet>
      {nouveauClient && (
        <PartenaireSheet id={null} ecriture types={nouveauClient} onClose={() => setNouveauClient(null)}
          onSaved={(p) => { setNouveauClient(null); if (p) setClient({ id: p.id, texte: p.nom }); }} />
      )}
    </>
  );
}

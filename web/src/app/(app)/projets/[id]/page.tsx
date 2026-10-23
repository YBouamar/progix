"use client";

import { use, useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { ArrowLeft, ArrowLeftRight, Lock, Pencil, RotateCcw, Trash2, Unlock } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { peutDiriger, peutGererCommercial, useAuth, voitMontants } from "@/lib/auth";
import { cn, formatDate, formatDateHeure } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/modal";
import { ProjetSheet } from "../projet-sheet";
import { DallesProjet } from "./dalles";
import { EquipeProjet } from "./equipe";
import { Documents } from "@/components/documents";
import { DevisProjet } from "./devis";
import { DepensesProjet } from "./depenses";
import { ContratsProjet } from "./contrats";
import { CourriersProjet } from "./courriers";
import { PartenairesProjet } from "./partenaires";
import { RemarquesProjet } from "./remarques";
import { SituationProjet } from "./situation";
import { TON_STATUT, type ProjetDetail, type Referentiel } from "../types";

type Action = "bloquer" | "supprimer";

export default function ProjetFiche({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { user } = useAuth();
  const ecriture = peutGererCommercial(user);
  const direction = peutDiriger(user);
  // Onglets de la fiche : seuls ceux que le rôle peut consulter sont proposés. L'onglet ouvert est gardé dans l'adresse.
  const role = user?.role ?? "";
  const onglets = ([
    ["informations", "Informations", true],
    ["dalles", "Dalles et suivi", true],
    ["commercial", voitMontants(user) && peutGererCommercial(user) ? "Devis et contrats" : "Devis", voitMontants(user) || peutGererCommercial(user)],
    ["courriers", "Courriers", ["directeur", "chef_projet", "responsable_administratif", "administrateur", "ingenieur_chef", "ingenieur"].includes(role)],
    ["depenses", "Dépenses", ["directeur", "chef_projet", "responsable_administratif", "administrateur", "chef_chantier", "responsable_chantier"].includes(role)],
    ["intervenants", "Équipe et partenaires", true],
    ["documents", "Documents et remarques", true],
  ] as [string, string, boolean][]).filter((o) => o[2]);
  const [choix, setChoix] = useState("");
  useEffect(() => { setChoix(window.location.hash.slice(1)); }, []);
  const onglet = onglets.some((o) => o[0] === choix) ? choix : "informations";
  const ouvrir = (code: string) => { setChoix(code); window.history.replaceState(null, "", `#${code}`); };
  const [p, setP] = useState<ProjetDetail | null>(null);
  const [ref, setRef] = useState<Referentiel | null>(null);
  const [erreur, setErreur] = useState("");
  const [edition, setEdition] = useState(false);
  const [action, setAction] = useState<Action | null>(null);
  const [motif, setMotif] = useState("");
  const [envoi, setEnvoi] = useState(false);
  const [reaffectation, setReaffectation] = useState(false);
  const [version, setVersion] = useState(0);

  const charger = useCallback(() => {
    api<ProjetDetail>(`/projets/${id}`).then(setP)
      .catch((e) => setErreur(e instanceof ApiError && e.status === 404 ? "Projet introuvable." : "Chargement impossible."));
  }, [id]);

  useEffect(() => { charger(); api<Referentiel>("/projets/referentiel").then(setRef).catch(() => undefined); }, [charger]);

  async function executer(chemin: string, corps?: object) {
    setEnvoi(true); setErreur("");
    try {
      setP(await api<ProjetDetail>(`/projets/${id}/${chemin}`, { method: "POST", body: JSON.stringify(corps ?? {}) }));
      setAction(null); setMotif("");
    } catch (e) {
      setErreur(e instanceof ApiError ? (e.errors?.motif?.[0] ?? e.message) : "Action impossible.");
    } finally {
      setEnvoi(false);
    }
  }

  if (!p) {
    return (
      <div>
        <Retour />
        <div className="rounded-xl border border-bordure bg-white p-12 text-center text-secondaire">{erreur || "Chargement..."}</div>
      </div>
    );
  }

  const infos: [string, string][] = [
    ["Client", p.client],
    ["Propriétaire", p.proprietaire || "Non renseigné"],
    ["Entité émettrice", `${p.entiteLabel} (${p.entite})`],
    ["Type de construction", p.typeLabel],
    ["Forfait appliqué au prix", p.forfait],
    ["Adresse du projet", [p.adresse, p.ville].filter(Boolean).join(", ") || "Non renseignée"],
    ["Date de création", `${formatDate(p.dateCreation)} · saisi par ${p.creePar}`],
    ["Description", p.description || "Aucune description"],
  ];

  return (
    <div>
      <Retour />
      <div className="rounded-xl border border-bordure bg-white p-6">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-display text-[15px] font-bold text-marine">{p.code}</span>
              <Badge ton={TON_STATUT[p.statut] ?? "neutre"}>{p.statutLabel}</Badge>
              <Badge>{p.entite} · {p.entiteLabel}</Badge>
              {p.bloque && <Badge ton="rouge">Projet bloqué</Badge>}
              {p.supprime && <Badge ton="rouge">Supprimé</Badge>}
            </div>
            <h2 className="mt-2 text-2xl font-bold">{p.libelle}</h2>
            <div className="mt-1 text-secondaire">{[p.client, p.typeLabel, p.ville].filter(Boolean).join(" · ")}</div>
          </div>
          {!p.supprime && (
            <div className="flex flex-wrap gap-2">
              {ecriture && <Button variant="outline" onClick={() => setEdition(true)}><Pencil size={15} />Modifier</Button>}
              {user?.role === "directeur" && !p.bloque && <Button variant="outline" onClick={() => setAction("bloquer")}><Lock size={15} />Bloquer</Button>}
              {user && ["directeur", "chef_projet", "responsable_administratif"].includes(user.role) && <Button variant="outline" onClick={() => setReaffectation(true)}><ArrowLeftRight size={15} />Changer d&apos;entité</Button>}
              {direction && <Button variant="outline" onClick={() => setAction("supprimer")}><Trash2 size={15} />Supprimer</Button>}
            </div>
          )}
        </div>

        {erreur && !action && <div role="alert" className="mt-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{erreur}</div>}

        {p.bloque && !p.supprime && (
          <div className="mt-5 flex flex-wrap items-center justify-between gap-3 rounded-lg border border-rouge-vif/30 bg-rouge-fond px-4 py-3 text-rouge-texte">
            <div>
              <div className="font-semibold">Projet bloqué.</div>
              <div className="text-[13px]">Motif : {p.blocageMotif?.replace(/[.\s]+$/, "")}. Bloqué par {p.blocagePar} le {formatDateHeure(p.blocageLe)}.</div>
            </div>
            {user?.role === "directeur" && <Button variant="outline" size="sm" disabled={envoi} onClick={() => executer("debloquer")}><Unlock size={14} />Débloquer</Button>}
          </div>
        )}

        {p.supprime && (
          <div className="mt-5 flex flex-wrap items-center justify-between gap-3 rounded-lg border border-bordure-forte bg-fond px-4 py-3">
            <div>
              <div className="font-semibold">Projet supprimé, en lecture seule.</div>
              <div className="text-[13px] text-secondaire">Supprimé le {formatDate(p.suppressionLe)} par {p.suppressionPar}. Motif : {p.suppressionMotif}</div>
            </div>
            {direction && <Button variant="outline" size="sm" disabled={envoi} onClick={() => executer("restaurer")}><RotateCcw size={14} />Restaurer</Button>}
          </div>
        )}
      </div>

      <div role="tablist" className="mt-5 flex gap-1 overflow-x-auto border-b border-bordure">
        {onglets.map(([code, label]) => (
          <button key={code} role="tab" type="button" aria-selected={onglet === code} onClick={() => ouvrir(code)}
            className={cn("-mb-px whitespace-nowrap border-b-2 px-4 py-2.5 text-[14px] font-semibold transition-colors",
              onglet === code ? "border-marine text-marine" : "border-transparent text-secondaire hover:text-marine")}>
            {label}
          </button>
        ))}
      </div>

      {onglet === "informations" && (
        <>
        <div className="mt-5 rounded-xl border border-bordure bg-white p-6">
          <h3 className="mb-4 text-[15px] font-bold">Informations</h3>
          <dl className="grid gap-x-8 gap-y-4 sm:grid-cols-2">
            {infos.map(([k, v]) => (
              <div key={k} className={k === "Description" ? "sm:col-span-2" : undefined}>
                <dt className="text-[12px] font-semibold uppercase tracking-wide text-attenue">{k}</dt>
                <dd className="mt-0.5 whitespace-pre-line">{v}</dd>
              </div>
            ))}
          </dl>
        </div>
        </>
      )}
      {onglet === "dalles" && <DallesProjet key={`dalles-${version}`} projetId={p.id} ecriture={ecriture && !p.supprime} lectureSeule={p.supprime} onSuivi={charger} />}
      {onglet === "commercial" && (
        <>
          {voitMontants(user) && <DevisProjet key={`devis-${version}`} projetId={p.id} lectureSeule={p.supprime} />}
          {peutGererCommercial(user) && voitMontants(user) && <SituationProjet key={`situation-${version}`} projetId={p.id} />}
          {peutGererCommercial(user) && <ContratsProjet key={`contrats-${version}`} projetId={p.id} />}
        </>
      )}
      {onglet === "courriers" && <CourriersProjet key={`courriers-${version}`} projetId={p.id} />}
      {onglet === "depenses" && <DepensesProjet key={`depenses-${version}`} projetId={p.id} />}
      {onglet === "intervenants" && (
        <>
          <EquipeProjet projetId={p.id} lectureSeule={p.supprime} />
          <PartenairesProjet projetId={p.id} ecriture={ecriture && !p.supprime} />
        </>
      )}
      {onglet === "documents" && (
        <>
          <Documents contexte="projet" cibleId={p.id} lectureSeule={p.supprime} aide="Plans d'architecte, courriers, photos. Plusieurs fichiers à la fois." />
          <RemarquesProjet key={`remarques-${version}`} projetId={p.id} lectureSeule={p.supprime} />
        </>
      )}

      {edition && ref && (
        <ProjetSheet projet={p} referentiel={ref} onClose={() => setEdition(false)} onSaved={(n) => { setP(n); setEdition(false); }} />
      )}

      <Modal open={reaffectation} onOpenChange={(o) => { if (!o) { setReaffectation(false); setErreur(""); } }}
        title="Changer l'entité du projet"
        description={`Le projet passe de ${p.entite} à ${p.entite === "APT" ? "BBR" : "APT"}. Il reçoit un nouveau code, et tous ses documents (devis, bons de livraison) reçoivent de nouveaux numéros dans la numérotation de la nouvelle entité.`}
        footer={
          <>
            <Button variant="outline" onClick={() => { setReaffectation(false); setErreur(""); }}>Annuler</Button>
            <Button disabled={envoi} onClick={async () => {
              setEnvoi(true); setErreur("");
              try {
                setP(await api<ProjetDetail>(`/projets/${id}/reaffecter`, { method: "POST", body: JSON.stringify({ entite: p.entite === "APT" ? "BBR" : "APT" }) }));
                setReaffectation(false); setVersion((v) => v + 1);
              } catch (e) { setErreur(e instanceof ApiError ? e.message : "Action impossible."); }
              finally { setEnvoi(false); }
            }}>Réaffecter à {p.entite === "APT" ? "BBR" : "APT"}</Button>
          </>
        }>
        <p className="text-[13px] text-secondaire">Les anciens numéros restent tracés dans l&apos;historique des devis, sur les bons de livraison et dans les remarques du projet.</p>
        {erreur && <div role="alert" className="mt-2 text-[13px] text-rouge-texte">{erreur}</div>}
      </Modal>

      <Modal open={!!action} onOpenChange={(o) => { if (!o) { setAction(null); setMotif(""); setErreur(""); } }}
        title={action === "bloquer" ? "Bloquer le projet" : "Supprimer le projet"}
        description={action === "bloquer"
          ? "Le blocage s'ajoute au statut du projet. Le motif, votre nom et la date seront affichés sur la fiche."
          : "Le projet passe en lecture seule. Il reste restaurable par le directeur ou le chef de projet."}
        footer={
          <>
            <Button variant="outline" onClick={() => { setAction(null); setMotif(""); setErreur(""); }}>Annuler</Button>
            <Button variant={action === "supprimer" ? "danger" : "default"} disabled={envoi || !motif.trim()}
              onClick={() => executer(action!, { motif })}>{action === "bloquer" ? "Bloquer" : "Supprimer"}</Button>
          </>
        }>
        <label className="block">
          <span className="mb-1.5 block text-[13px] font-semibold text-secondaire">Motif<span className="text-rouge-vif"> *</span></span>
          <textarea rows={3} autoFocus value={motif} onChange={(e) => setMotif(e.target.value)}
            className="w-full rounded-lg border border-bordure-forte px-3 py-2 text-sm focus:border-marine focus:outline-none focus:ring-2 focus:ring-marine/15" />
        </label>
        {erreur && <div role="alert" className="mt-2 text-[13px] text-rouge-texte">{erreur}</div>}
      </Modal>
    </div>
  );
}

function Retour() {
  return (
    <Link href="/projets" className="mb-4 inline-flex items-center gap-1.5 text-[13px] font-semibold text-secondaire hover:text-marine">
      <ArrowLeft size={15} />Tous les projets
    </Link>
  );
}

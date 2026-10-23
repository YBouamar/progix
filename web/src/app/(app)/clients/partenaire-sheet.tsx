"use client";

import { useEffect, useState } from "react";
import { Plus, Trash2 } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Field, Input, Select } from "@/components/ui/input";
import { Sheet } from "@/components/ui/sheet";
import { cn } from "@/lib/utils";
import { FORM_VIDE, type Compteur, type PartenaireForm } from "./types";

type Doublon = { doublon: boolean; id: string | null; nom: string | null };
type Probable = { id: string; nom: string; ville: string | null; motif: string };

export function PartenaireSheet({ id, ecriture, types, typeInitial, onClose, onSaved }: {
  id: string | null; ecriture: boolean; types: Compteur[]; typeInitial?: string; onClose: () => void; onSaved: (cree?: { id: string; nom: string }) => void;
}) {
  const [f, setF] = useState<PartenaireForm>(typeInitial ? { ...FORM_VIDE, type: typeInitial } : FORM_VIDE);
  const [pret, setPret] = useState(id === null);
  const [erreurs, setErreurs] = useState<Record<string, string[]>>({});
  const [message, setMessage] = useState("");
  const [doublon, setDoublon] = useState<Doublon | null>(null);
  const [envoi, setEnvoi] = useState(false);

  useEffect(() => {
    if (!id) return;
    api<Record<string, unknown>>(`/partenaires/${id}`).then((d) => {
      const s = (k: string) => (d[k] as string | null) ?? "";
      setF({
        type: s("type"), nature: d.nature as PartenaireForm["nature"],
        raisonSociale: s("raisonSociale"), ice: s("ice"), rc: s("rc"), identifiantFiscal: s("identifiantFiscal"), patente: s("patente"),
        civilite: s("civilite") || "M.", nomFamille: s("nomFamille"), prenom: s("prenom"), cin: s("cin"),
        nationalite: s("nationalite"), dateNaissance: s("dateNaissance"), lieuNaissance: s("lieuNaissance"),
        adresse: s("adresse"), ville: s("ville"), telephone: s("telephone"), email: s("email"),
        exonereTva: !!d.exonereTva, attestationNumero: s("attestationNumero"), attestationExpiration: s("attestationExpiration"), contacts: (d.contacts as PartenaireForm["contacts"]) ?? [],
      });
      setPret(true);
    }).catch((e) => setMessage(e instanceof ApiError ? e.message : "Chargement impossible."));
  }, [id]);

  // Contrôle de doublon pendant la saisie de l'ICE ou de la CIN.
  const autre = f.nature === "autre";
  const cle = autre ? "" : f.nature === "morale" ? f.ice.trim() : f.cin.trim();
  useEffect(() => {
    if (!ecriture || cle.length < 4) { setDoublon(null); return; }
    const t = setTimeout(() => {
      const p = new URLSearchParams(f.nature === "morale" ? { ice: cle } : { cin: cle.toUpperCase() });
      if (id) p.set("sauf", id);
      api<Doublon>(`/partenaires/doublon?${p}`).then(setDoublon).catch(() => setDoublon(null));
    }, 350);
    return () => clearTimeout(t);
  }, [cle, f.nature, id, ecriture]);

  // Doublons probables (même nom ou même téléphone), pour toutes les catégories : un avertissement, pas un blocage.
  const [probables, setProbables] = useState<Probable[]>([]);
  const nomSaisi = (f.nature !== "physique" ? f.raisonSociale : `${f.prenom} ${f.nomFamille}`).trim();
  const telSaisi = f.telephone.trim();
  useEffect(() => {
    if (!ecriture || (nomSaisi.length < 3 && telSaisi.length < 9)) { setProbables([]); return; }
    const t = setTimeout(() => {
      const p = new URLSearchParams({ nom: nomSaisi, telephone: telSaisi });
      if (id) p.set("sauf", id);
      api<Probable[]>(`/partenaires/doublons-probables?${p}`).then(setProbables).catch(() => setProbables([]));
    }, 400);
    return () => clearTimeout(t);
  }, [nomSaisi, telSaisi, id, ecriture]);

  const set = <K extends keyof PartenaireForm>(k: K, v: PartenaireForm[K]) => setF((x) => ({ ...x, [k]: v }));
  const err = (k: string) => erreurs[k]?.[0];
  const txt = (k: keyof PartenaireForm, extra?: React.InputHTMLAttributes<HTMLInputElement>) => (
    <Input disabled={!ecriture} value={f[k] as string} aria-invalid={!!err(k)}
      onChange={(e) => set(k, e.target.value as never)} {...extra} />
  );

  async function enregistrer() {
    setEnvoi(true); setErreurs({}); setMessage("");
    try {
      const corps = JSON.stringify({ ...f, dateNaissance: f.dateNaissance || null, attestationExpiration: f.attestationExpiration || null });
      onSaved(await api<{ id: string; nom: string }>(id ? `/partenaires/${id}` : "/partenaires", { method: id ? "PUT" : "POST", body: corps }));
    } catch (e) {
      if (e instanceof ApiError) { setErreurs(e.errors ?? {}); setMessage(e.errors ? "Corrigez les champs signalés." : e.message); }
    } finally {
      setEnvoi(false);
    }
  }

  const morale = f.nature !== "physique";

  return (
    <Sheet open onOpenChange={(o) => !o && onClose()}
      title={id ? (morale ? f.raisonSociale : `${f.prenom} ${f.nomFamille}`) || "Fiche" : "Nouveau client ou partenaire"}
      description={ecriture ? "L'identité reste modifiable à tout moment." : "Consultation seule."}
      footer={ecriture ? (
        <>
          <Button variant="outline" onClick={onClose}>Annuler</Button>
          <Button onClick={enregistrer} disabled={envoi || !pret || !!doublon?.doublon}>{envoi ? "Enregistrement..." : "Enregistrer"}</Button>
        </>
      ) : undefined}>
      {message && <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{message}</div>}
      {!pret ? <div className="text-secondaire">Chargement...</div> : (
        <div className="space-y-6">
          <div className="grid gap-4">
            <Field label="Type" required error={err("type")}>
              <Select disabled={!ecriture} value={f.type} onChange={(e) => set("type", e.target.value)}>
                {types.map((t) => <option key={t.type} value={t.type}>{t.label}</option>)}
              </Select>
            </Field>
            <Field label="Catégorie" required>
              <div className="flex rounded-lg border border-bordure-forte p-0.5">
                {(["morale", "physique", "autre"] as const).map((n) => (
                  <button key={n} type="button" disabled={!ecriture} onClick={() => set("nature", n)}
                    className={cn("h-9 flex-1 cursor-pointer whitespace-nowrap rounded-md px-3 text-[13px] font-semibold",
                      f.nature === n ? "bg-marine text-white" : "text-secondaire")}>
                    {n === "morale" ? "Personne morale" : n === "physique" ? "Personne physique" : "Autre"}
                  </button>
                ))}
              </div>
            </Field>
          </div>

          <section>
            <h3 className="mb-3 text-[15px] font-bold">Identité</h3>
            {morale ? (
              <div className="grid grid-cols-2 gap-4">
                <Field label={autre ? "Nom" : "Raison sociale"} required error={err("raisonSociale")} className="col-span-2">{txt("raisonSociale")}</Field>
                {!autre && (
                  <>
                    <Field label="ICE" error={err("ice")}>{txt("ice", { inputMode: "numeric", maxLength: 15 })}</Field>
                    <Field label="Registre de commerce">{txt("rc")}</Field>
                    <Field label="Identifiant fiscal">{txt("identifiantFiscal")}</Field>
                    <Field label="Patente">{txt("patente")}</Field>
                  </>
                )}
              </div>
            ) : (
              <div className="grid grid-cols-2 gap-4">
                <Field label="Civilité">
                  <Select disabled={!ecriture} value={f.civilite} onChange={(e) => set("civilite", e.target.value)}>
                    <option>M.</option><option>Mme</option>
                  </Select>
                </Field>
                <Field label="CIN">{txt("cin")}</Field>
                <Field label="Nom" required error={err("nomFamille")}>{txt("nomFamille")}</Field>
                <Field label="Prénom" required error={err("prenom")}>{txt("prenom")}</Field>
                <Field label="Nationalité">{txt("nationalite")}</Field>
                <Field label="Date de naissance">{txt("dateNaissance", { type: "date" })}</Field>
                <Field label="Lieu de naissance" className="col-span-2">{txt("lieuNaissance")}</Field>
              </div>
            )}
            {doublon?.doublon && (
              <div role="alert" className="mt-3 rounded-lg bg-orange-fond px-4 py-3 text-orange-texte">
                Cet identifiant est déjà utilisé par <strong>{doublon.nom}</strong>. Enregistrement impossible.
              </div>
            )}
            {!doublon?.doublon && probables.length > 0 && (
              <div role="status" className="mt-3 rounded-lg bg-orange-fond px-4 py-3 text-orange-texte">
                Doublon possible, vérifiez avant d&apos;enregistrer :
                <ul className="mt-1 list-disc pl-5">
                  {probables.map((x) => <li key={x.id}><strong>{x.nom}</strong>{x.ville ? ` (${x.ville})` : ""} : {x.motif}</li>)}
                </ul>
              </div>
            )}
          </section>

          <section>
            <h3 className="mb-3 text-[15px] font-bold">Coordonnées</h3>
            <div className="grid grid-cols-2 gap-4">
              <Field label="Adresse" className="col-span-2">{txt("adresse")}</Field>
              <Field label="Ville">{txt("ville")}</Field>
              <Field label="Téléphone">{txt("telephone", { type: "tel" })}</Field>
              <Field label="Email" error={err("email")} className="col-span-2">{txt("email", { type: "email" })}</Field>
            </div>
            <label className="mt-4 flex cursor-pointer items-start gap-3 rounded-lg border border-bordure p-3">
              <input type="checkbox" className="mt-0.5 h-4 w-4 accent-marine" disabled={!ecriture}
                checked={f.exonereTva} onChange={(e) => set("exonereTva", e.target.checked)} />
              <span>
                <span className="block font-semibold">Client exonéré de TVA</span>
                <span className="block text-[13px] text-secondaire">TVA à 0 % sur ses documents, le total TTC est égal au total HT.</span>
              </span>
            </label>
            {f.exonereTva && (
              <div className="mt-3 grid grid-cols-2 gap-4">
                <Field label="Numéro de l'attestation d'exonération">{txt("attestationNumero")}</Field>
                <Field label="Expiration de l'attestation">{txt("attestationExpiration", { type: "date" })}</Field>
                <p className="col-span-2 -mt-2 text-[12px] text-attenue">
                  Gardée sur la fiche, jamais imprimée sur les documents.
                  {f.attestationExpiration && f.attestationExpiration < new Date().toLocaleDateString("fr-CA") && <span className="font-semibold text-rouge-texte"> Attestation expirée : ce client est facturé à la TVA normale tant que la date n&apos;est pas mise à jour.</span>}
                </p>
              </div>
            )}
          </section>

          <section>
            <div className="mb-3 flex items-center justify-between">
              <h3 className="text-[15px] font-bold">Contacts</h3>
              {ecriture && (
                <Button variant="outline" size="sm"
                  onClick={() => set("contacts", [...f.contacts, { nom: "", fonction: "", telephone: "", email: "" }])}>
                  <Plus size={14} />Ajouter un contact
                </Button>
              )}
            </div>
            {f.contacts.length === 0 && <div className="text-[13px] text-attenue">Aucun contact.</div>}
            <div className="space-y-3">
              {f.contacts.map((c, i) => {
                const maj = (k: "nom" | "fonction" | "telephone" | "email", v: string) =>
                  set("contacts", f.contacts.map((x, j) => (j === i ? { ...x, [k]: v } : x)));
                return (
                  <div key={i} className="grid grid-cols-[1fr_1fr_auto] gap-2 rounded-lg border border-bordure p-3">
                    <Input disabled={!ecriture} placeholder="Nom" value={c.nom} onChange={(e) => maj("nom", e.target.value)} />
                    <Input disabled={!ecriture} placeholder="Fonction" value={c.fonction ?? ""} onChange={(e) => maj("fonction", e.target.value)} />
                    {ecriture ? (
                      <Button variant="ghost" size="icon" aria-label="Retirer le contact"
                        onClick={() => set("contacts", f.contacts.filter((_, j) => j !== i))}><Trash2 size={16} /></Button>
                    ) : <span />}
                    <Input disabled={!ecriture} placeholder="Téléphone" value={c.telephone ?? ""} onChange={(e) => maj("telephone", e.target.value)} />
                    <Input disabled={!ecriture} placeholder="Email" value={c.email ?? ""} onChange={(e) => maj("email", e.target.value)} />
                  </div>
                );
              })}
            </div>
          </section>
        </div>
      )}
    </Sheet>
  );
}

"use client";

import { useEffect, useState } from "react";
import { Plus } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { cn, formatDateHeure } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field, Input, Select } from "@/components/ui/input";
import { Sheet } from "@/components/ui/sheet";

type Utilisateur = { id: string; fullName: string; email: string; login: string; role: string; roleLabel: string; isActive: boolean; lastLoginAt: string | null };
type Role = { code: string; label: string; voitMontants: boolean };

const message = (e: unknown, defaut: string) => {
  if (!(e instanceof ApiError)) return defaut;
  const details = e.errors ? Object.values(e.errors).flat() : [];
  return details.length > 0 ? details.join(" ") : e.message;
};

/** Comptes des utilisateurs : réservé au directeur. */
export function UtilisateursParam() {
  const { user } = useAuth();
  const [liste, setListe] = useState<Utilisateur[] | null>(null);
  const [roles, setRoles] = useState<Role[]>([]);
  const [fiche, setFiche] = useState<Utilisateur | "nouveau" | null>(null);
  const [inactifs, setInactifs] = useState(false);

  const charger = () => api<Utilisateur[]>("/users").then(setListe).catch(() => setListe(null));
  const gestion = user?.role === "directeur" || user?.role === "chef_projet";
  useEffect(() => { if (gestion) { charger(); api<Role[]>("/roles").then(setRoles).catch(() => undefined); } }, [gestion]);
  if (!gestion || !liste) return null;
  const visibles = liste.filter((u) => inactifs || u.isActive);

  return (
    <div className="mt-5 rounded-xl border border-bordure bg-white p-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-[15px] font-bold">Utilisateurs</h2>
          <p className="mt-1 text-[13px] text-secondaire">{liste.filter((u) => u.isActive).length} compte(s) actif(s) sur {liste.length}. Un compte ne se supprime pas : il se désactive, et son historique reste.</p>
        </div>
        <Button variant="outline" onClick={() => setFiche("nouveau")}><Plus size={15} />Nouvel utilisateur</Button>
      </div>
      <label className="mt-3 flex cursor-pointer items-center gap-2 text-[13px] text-secondaire">
        <input type="checkbox" className="h-4 w-4 accent-marine" checked={inactifs} onChange={(e) => setInactifs(e.target.checked)} />Afficher les comptes désactivés
      </label>
      <div className="mt-3 overflow-x-auto">
        <table className="w-full text-left text-[13px]">
          <thead className="border-b border-bordure text-[12px] uppercase tracking-wide text-secondaire">
            <tr><th className="py-2 pr-4 font-semibold">Nom</th><th className="py-2 pr-4 font-semibold">Identifiant</th><th className="py-2 pr-4 font-semibold">Email</th><th className="py-2 pr-4 font-semibold">Rôle</th><th className="py-2 pr-4 font-semibold">Dernière connexion</th><th className="py-2 font-semibold">État</th></tr>
          </thead>
          <tbody>
            {visibles.map((u) => (
              <tr key={u.id} onClick={() => setFiche(u)} className={cn("cursor-pointer border-b border-bordure last:border-0 hover:bg-fond", !u.isActive && "text-attenue")}>
                <td className="py-2.5 pr-4 font-semibold">{u.fullName}{u.id === user.id && <span className="font-normal text-attenue"> · vous</span>}</td>
                <td className="py-2.5 pr-4 font-display">{u.login}</td>
                <td className="py-2.5 pr-4">{u.email}</td>
                <td className="py-2.5 pr-4">{u.roleLabel}</td>
                <td className="whitespace-nowrap py-2.5 pr-4 font-display">{u.lastLoginAt ? formatDateHeure(u.lastLoginAt) : "Jamais"}</td>
                <td className="py-2.5"><Badge ton={u.isActive ? "vert" : "neutre"}>{u.isActive ? "Actif" : "Désactivé"}</Badge></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {fiche && <Fiche u={fiche === "nouveau" ? null : fiche} roles={roles} moi={user.id} onClose={() => setFiche(null)} onFait={() => { setFiche(null); charger(); }} />}
    </div>
  );
}

function Fiche({ u, roles, moi, onClose, onFait }: { u: Utilisateur | null; roles: Role[]; moi: string; onClose: () => void; onFait: () => void }) {
  const [nom, setNom] = useState(u?.fullName ?? "");
  const [prenom, setPrenom] = useState("");
  const [famille, setFamille] = useState("");
  const [loginPropose, setLoginPropose] = useState("");
  useEffect(() => {
    if (u) return;
    const t = setTimeout(() => {
      if (!prenom.trim() || !famille.trim()) { setLoginPropose(""); return; }
      api<{ login: string }>(`/users/login-propose?${new URLSearchParams({ prenom, nom: famille })}`).then((r) => setLoginPropose(r.login)).catch(() => setLoginPropose(""));
    }, 300);
    return () => clearTimeout(t);
  }, [u, prenom, famille]);
  const [email, setEmail] = useState(u?.email ?? "");
  const [role, setRole] = useState(u?.role ?? "");
  const [actif, setActif] = useState(u?.isActive ?? true);
  const [motDePasse, setMotDePasse] = useState("");
  const [erreur, setErreur] = useState("");
  const [info, setInfo] = useState("");
  const [envoi, setEnvoi] = useState(false);
  const soi = u?.id === moi;

  const enregistrer = async () => {
    setEnvoi(true); setErreur(""); setInfo("");
    try {
      if (u) await api(`/users/${u.id}`, { method: "PUT", body: JSON.stringify({ fullName: nom, email, role, isActive: actif }) });
      else await api("/users", { method: "POST", body: JSON.stringify({ prenom, nom: famille, email, role, password: motDePasse }) });
      onFait();
    } catch (e) { setErreur(message(e, "Enregistrement impossible.")); setEnvoi(false); }
  };
  const reinitialiser = async () => {
    setEnvoi(true); setErreur(""); setInfo("");
    try { await api(`/users/${u!.id}/reset-password`, { method: "POST", body: JSON.stringify({ newPassword: motDePasse }) }); setMotDePasse(""); setInfo("Mot de passe remplacé. Communiquez-le à la personne."); }
    catch (e) { setErreur(message(e, "Changement impossible.")); }
    finally { setEnvoi(false); }
  };

  return (
    <Sheet open onOpenChange={(o) => !o && onClose()} title={u ? u.fullName : "Nouvel utilisateur"} description={u ? `Identifiant de connexion : ${u.login}` : "Le compte est utilisable dès sa création."}>
      <div className="grid gap-3">
        {u ? <Field label="Nom complet" required><Input value={nom} onChange={(e) => setNom(e.target.value)} /></Field> : (
          <>
            <div className="grid grid-cols-2 gap-3">
              <Field label="Prénom" required><Input autoFocus value={prenom} onChange={(e) => setPrenom(e.target.value)} /></Field>
              <Field label="Nom" required><Input value={famille} onChange={(e) => setFamille(e.target.value)} /></Field>
            </div>
            <div className="rounded-lg bg-fond px-4 py-3 text-[13px]">
              <span className="text-secondaire">Identifiant de connexion : </span><span className="font-display font-bold">{loginPropose || "prénom.nom"}</span>
              <div className="text-[12px] text-attenue">Formé du prénom et du nom, sans accent. Il ne pourra plus être modifié après la création.</div>
            </div>
          </>
        )}
        <Field label="Adresse email" required><Input type="email" value={email} onChange={(e) => setEmail(e.target.value)} /></Field>
        <Field label="Rôle" required>
          <Select value={role} disabled={soi} onChange={(e) => setRole(e.target.value)}>
            <option value="">Choisir un rôle</option>{roles.map((r) => <option key={r.code} value={r.code}>{r.label}{r.voitMontants ? "" : " · ne voit pas les montants"}</option>)}
          </Select>
        </Field>
        {!u && <Field label="Mot de passe initial : 8 caractères au moins, avec une majuscule, une minuscule et un chiffre" required><Input type="text" autoComplete="off" value={motDePasse} onChange={(e) => setMotDePasse(e.target.value)} /></Field>}
        {u && (
          <label className={cn("flex items-start gap-3 rounded-lg border border-bordure p-3", soi ? "opacity-60" : "cursor-pointer")}>
            <input type="checkbox" className="mt-0.5 h-4 w-4 accent-marine" disabled={soi} checked={actif} onChange={(e) => setActif(e.target.checked)} />
            <span><span className="block font-semibold">Compte actif</span><span className="block text-[13px] text-secondaire">Un compte désactivé ne peut plus se connecter et sa session en cours s&apos;arrête.</span></span>
          </label>
        )}
        {soi && <p className="text-[12px] text-attenue">Vous ne pouvez ni désactiver votre propre compte ni changer votre propre rôle.</p>}
        {u && u.role !== role && role && <p className="text-[12px] text-orange-texte">Le changement de rôle déconnecte la personne : elle devra se reconnecter.</p>}
      </div>
      {erreur && <div role="alert" className="mt-3 rounded-lg bg-rouge-fond px-4 py-3 text-[13px] text-rouge-texte">{erreur}</div>}
      <div className="mt-4 flex justify-end gap-2">
        <Button variant="outline" onClick={onClose}>Fermer</Button>
        <Button disabled={envoi || !role || !email.trim() || (u ? !nom.trim() : (!loginPropose || motDePasse.length < 8))} onClick={enregistrer}>{u ? "Enregistrer" : "Créer le compte"}</Button>
      </div>
      {u && (
        <div className="mt-6 rounded-lg border border-bordure p-4">
          <div className="font-semibold">Remplacer le mot de passe</div>
          <p className="text-[12px] text-secondaire">À utiliser si la personne a oublié le sien. L&apos;ancien mot de passe ne fonctionnera plus.</p>
          <div className="mt-2 flex items-end gap-2">
            <Field label="Nouveau mot de passe : 8 caractères, majuscule, minuscule et chiffre" className="flex-1"><Input type="text" autoComplete="off" value={motDePasse} onChange={(e) => setMotDePasse(e.target.value)} /></Field>
            <Button variant="outline" disabled={envoi || motDePasse.length < 8} onClick={reinitialiser}>Remplacer</Button>
          </div>
          {info && <div role="status" className="mt-2 text-[13px] text-vert-texte">{info}</div>}
        </div>
      )}
    </Sheet>
  );
}

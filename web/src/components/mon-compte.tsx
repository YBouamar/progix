"use client";

import { useState } from "react";
import { KeyRound } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { cn, initiales } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";

/** Bloc utilisateur de la barre latérale : ouvre la fenêtre de changement de mot de passe. */
export function MonCompte() {
  const { user } = useAuth();
  const [ouvert, setOuvert] = useState(false);
  if (!user) return null;

  return (
    <>
      <button type="button" onClick={() => setOuvert(true)}
        className="flex min-w-0 flex-1 cursor-pointer items-center gap-3 rounded-lg px-2 py-1.5 text-left hover:bg-white/10">
        <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-white/15 font-display text-[13px] font-bold">
          {initiales(user.fullName)}
        </div>
        <div className="min-w-0 flex-1">
          <div className="truncate text-[13px] font-semibold">{user.fullName}</div>
          <div className="truncate text-[12px] text-white/65">{user.roleLabel}</div>
        </div>
      </button>
      {ouvert && <ChangerMotDePasse onClose={() => setOuvert(false)} />}
    </>
  );
}

function ChangerMotDePasse({ onClose }: { onClose: () => void }) {
  const [actuel, setActuel] = useState("");
  const [nouveau, setNouveau] = useState("");
  const [confirmation, setConfirmation] = useState("");
  const [erreur, setErreur] = useState("");
  const [envoi, setEnvoi] = useState(false);
  const [fait, setFait] = useState(false);

  const discordance = confirmation.length > 0 && nouveau !== confirmation;
  const valide = actuel.length > 0 && nouveau.length >= 8 && nouveau === confirmation;

  async function enregistrer() {
    setEnvoi(true); setErreur("");
    try {
      await api("/auth/change-password", { method: "POST", body: JSON.stringify({ currentPassword: actuel, newPassword: nouveau }) });
      setFait(true);
    } catch (e) {
      setErreur(e instanceof ApiError ? (e.errors?.password?.[0] ?? e.message) : "Changement impossible.");
    } finally {
      setEnvoi(false);
    }
  }

  return (
    <Modal open onOpenChange={(o) => !o && onClose()} title="Changer mon mot de passe"
      description={fait ? undefined : "Le nouveau mot de passe s'applique à votre prochaine connexion comme dès maintenant."}
      footer={
        fait ? (
          <Button onClick={onClose}>Fermer</Button>
        ) : (
          <>
            <Button variant="outline" onClick={onClose}>Annuler</Button>
            <Button disabled={!valide || envoi} onClick={enregistrer}>Enregistrer</Button>
          </>
        )
      }>
      {fait ? (
        <div role="status" className="rounded-lg bg-vert-fond px-4 py-3 text-[13px] text-vert-texte">Mot de passe changé.</div>
      ) : (
        <div className="space-y-3">
          <Field label="Mot de passe actuel" required>
            <Input type="password" autoComplete="current-password" value={actuel} onChange={(e) => setActuel(e.target.value)} />
          </Field>
          <Field label="Nouveau mot de passe" required>
            <Input type="password" autoComplete="new-password" value={nouveau} onChange={(e) => setNouveau(e.target.value)} />
          </Field>
          <Field label="Confirmer le nouveau mot de passe" required error={discordance ? "Les deux mots de passe ne correspondent pas." : undefined}>
            <Input type="password" autoComplete="new-password" aria-invalid={discordance}
              className={cn(discordance && "border-rouge-vif")} value={confirmation} onChange={(e) => setConfirmation(e.target.value)} />
          </Field>
          <p className="text-[12px] text-attenue"><KeyRound size={12} className="mr-1 inline" />8 caractères au moins, avec une majuscule, une minuscule et un chiffre.</p>
          {erreur && <div role="alert" className="text-[13px] text-rouge-texte">{erreur}</div>}
        </div>
      )}
    </Modal>
  );
}

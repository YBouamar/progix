"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { ApiError } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/input";

export default function LoginPage() {
  const { user, loading, login } = useAuth();
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [erreur, setErreur] = useState<{ titre: string; detail: string } | null>(null);
  const [envoi, setEnvoi] = useState(false);

  useEffect(() => { if (!loading && user) router.replace("/"); }, [loading, user, router]);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setErreur(null);
    setEnvoi(true);
    try {
      await login(email.trim(), password);
      router.replace("/");
    } catch (err) {
      const s = err instanceof ApiError ? err.status : 0;
      setErreur(
        s === 401 ? { titre: "Identifiant ou mot de passe incorrect.", detail: "Vérifiez votre saisie puis réessayez." }
        : s === 403 ? { titre: "Compte désactivé.", detail: "Adressez-vous au directeur pour le réactiver." }
        : { titre: "Connexion au serveur impossible.", detail: "Vérifiez le réseau puis réessayez." });
    } finally {
      setEnvoi(false);
    }
  }

  return (
    <div className="grid min-h-screen lg:grid-cols-2">
      <div className="hidden flex-col justify-between bg-marine p-12 text-white lg:flex">
        <div className="font-display text-2xl font-bold tracking-wide">PROGIX</div>
        <div>
          <h1 className="max-w-md text-4xl font-bold leading-tight">Du devis à la réception finale, dalle par dalle.</h1>
          <p className="mt-4 max-w-md text-[15px] text-white/75">
            Projets, devis, suivi de chantier en 11 étapes, livraisons, facturation et trésorerie.
          </p>
        </div>
        <div className="text-[13px] text-white/60">Africa PT / MK4 · BECOMAR</div>
      </div>
      <div className="flex items-center justify-center p-6">
        <form onSubmit={onSubmit} className="w-full max-w-sm">
          <div className="mb-8 font-display text-2xl font-bold text-marine lg:hidden">PROGIX</div>
          <h2 className="text-2xl font-bold">Connexion</h2>
          <p className="mb-6 mt-1 text-secondaire">Votre identifiant est de la forme prénom.nom.</p>
          {erreur && (
            <div role="alert" className="mb-5 rounded-lg border border-rouge-vif/30 bg-rouge-fond px-4 py-3 text-rouge-texte">
              <div className="font-semibold">{erreur.titre}</div>
              <div className="text-[13px]">{erreur.detail}</div>
            </div>
          )}
          <Field label="Identifiant" className="mb-4">
            <Input type="text" autoComplete="username" autoCapitalize="none" spellCheck={false} placeholder="prenom.nom" required value={email} onChange={(e) => setEmail(e.target.value)} />
          </Field>
          <Field label="Mot de passe" className="mb-6">
            <Input type="password" autoComplete="current-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
          </Field>
          <Button type="submit" className="w-full" disabled={envoi}>{envoi ? "Connexion..." : "Se connecter"}</Button>
        </form>
      </div>
    </div>
  );
}

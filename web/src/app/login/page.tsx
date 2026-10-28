"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Image from "next/image";
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
      {/* Coulage d'une dalle en vidéo de fond, voilée de marine pour garder le texte lisible. Sans animation si l'utilisateur l'a demandé. */}
      <div className="relative hidden overflow-hidden bg-marine text-white lg:block">
        <video className="absolute inset-0 h-full w-full object-cover motion-reduce:hidden" src="/medias/chantier-dalle.mp4"
          autoPlay muted loop playsInline preload="auto" aria-hidden />
        <div className="absolute inset-0 bg-gradient-to-b from-marine/85 via-marine/55 to-marine/90" />
        <div className="relative flex h-full flex-col justify-between p-12">
          <div className="flex items-center gap-3">
            <Image src="/marque/icone.png" alt="" width={48} height={48} priority className="h-12 w-12" />
            <span className="font-display text-2xl font-bold tracking-wide">PROGIX</span>
          </div>
          <div>
            <h1 className="max-w-md text-4xl font-bold leading-tight">Du devis à la réception finale, dalle par dalle.</h1>
            <p className="mt-4 max-w-md text-[15px] text-white/80">
              Projets, devis, suivi de chantier en 11 étapes, livraisons, facturation et trésorerie.
            </p>
          </div>
          <div className="flex items-end justify-between gap-4 text-[13px] text-white/70">
            <span>Africa PT / MK4 · BECOMAR</span>
            <span className="text-[11px] text-white/50">Vidéo : AV, Pexels</span>
          </div>
        </div>
      </div>
      <div className="flex items-center justify-center p-6">
        <form onSubmit={onSubmit} className="w-full max-w-sm">
          <Image src="/marque/logo-vertical.png" alt="PROGIX" width={480} height={571} priority className="mx-auto mb-8 hidden h-auto w-36 lg:block" />
          <Image src="/marque/logo-horizontal.png" alt="PROGIX" width={720} height={218} priority className="mb-8 h-auto w-48 lg:hidden" />
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

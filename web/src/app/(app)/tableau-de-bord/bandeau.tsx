"use client";

import Image from "next/image";
import { useAuth } from "@/lib/auth";
import { initiales } from "@/lib/utils";

/** Bandeau d'accueil du tableau de bord : salue la personne connectée et rappelle sa fonction et la date du jour. */
export function BandeauAccueil() {
  const { user } = useAuth();
  if (!user) return null;
  const jour = new Date().toLocaleDateString("fr-FR", { weekday: "long", day: "numeric", month: "long", year: "numeric", timeZone: "Africa/Casablanca" });

  return (
    <section aria-label="Accueil" className="relative mb-6 overflow-hidden rounded-xl bg-gradient-to-r from-marine to-[#1f4fa3] px-6 py-5 text-white">
      <Image src="/marque/icone.png" alt="" width={160} height={160} aria-hidden
        className="pointer-events-none absolute -right-6 -top-8 h-40 w-40 opacity-15" />
      <div className="relative flex items-center gap-4">
        <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-white/15 font-display text-[15px] font-bold">
          {initiales(user.fullName)}
        </div>
        <div className="min-w-0">
          <h2 className="truncate font-display text-xl font-bold">Bonjour, {user.fullName}</h2>
          <p className="text-[14px] text-white/80">
            <span className="font-semibold text-white">{user.roleLabel}</span>
            <span className="mx-2 text-white/40">·</span>
            <span className="first-letter:uppercase">{jour}</span>
          </p>
        </div>
      </div>
    </section>
  );
}

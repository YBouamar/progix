"use client";

import { useAuth } from "@/lib/auth";
import { TraceCable } from "@/components/trace-cable";

/** Bandeau d'accueil du tableau de bord : salue la personne connectée et rappelle sa fonction et la date du jour. */
export function BandeauAccueil() {
  const { user } = useAuth();
  if (!user) return null;
  const jour = new Date().toLocaleDateString("fr-FR", { weekday: "long", day: "numeric", month: "long", year: "numeric", timeZone: "Africa/Casablanca" });

  return (
    <section aria-label="Accueil" className="relative mb-6 overflow-hidden rounded-xl bg-marine text-white">
      <div className="relative z-10 px-6 pb-16 pt-6 sm:pb-6 sm:pr-[46%]">
        <p className="text-[13px] text-white/70 first-letter:uppercase">{jour}</p>
        <h2 className="mt-1 truncate font-display text-2xl font-bold">Bonjour, {user.fullName}</h2>
        <p className="mt-1 text-[14px] text-white/80">{user.roleLabel}</p>
      </div>
      {/* Coupe de dalle et tracé du câble : le métier de l'entreprise, en signature. */}
      <TraceCable className="pointer-events-none absolute bottom-0 right-0 h-16 w-full text-white sm:h-full sm:w-[44%] sm:pt-5" />
    </section>
  );
}

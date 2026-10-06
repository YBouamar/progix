"use client";

import Link from "next/link";
import { useAuth } from "@/lib/auth";
import { menuPour } from "@/lib/nav";
import { TableauDirection } from "./tableau-de-bord/direction";
import { TableauCommercial } from "./tableau-de-bord/commercial";
import { TableauPersonnel } from "./tableau-de-bord/personnel";
import { TableauLogistique } from "./tableau-de-bord/logistique";

export default function Accueil() {
  const { user } = useAuth();
  if (!user) return null;
  if (user.role === "directeur" || user.role === "chef_projet") return <TableauDirection />;
  if (user.role === "responsable_administratif" || user.role === "administrateur") return <TableauCommercial />;
  if (["ingenieur", "ingenieur_chef", "dessinateur", "dessinateur_chef"].includes(user.role)) return <TableauPersonnel variante="etudes" />;
  if (user.role === "responsable_chantier" || user.role === "chef_chantier") return <TableauPersonnel variante="chantier" />;
  if (user.role === "agent_commercial") return <TableauPersonnel variante="agent" />;
  if (user.role === "responsable_logistique") return <TableauLogistique />;
  const modules = menuPour(user.role).filter((m) => m.id !== "tdb");
  return (
    <div>
      <h2 className="text-2xl font-bold">Bonjour {user.fullName}</h2>
      <p className="mt-1 text-secondaire">Le tableau de bord de votre rôle arrive bientôt. En attendant, voici vos modules.</p>
      <div className="mt-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {modules.map((m) => (
          <Link key={m.id} href={m.href} className="rounded-xl border border-bordure bg-white p-5 transition-colors hover:border-marine">
            <div className="font-display text-[15px] font-bold">{m.label}</div>
            <div className="mt-1 text-[13px] text-attenue">{m.pret ? "Disponible" : "En cours de développement"}</div>
          </Link>
        ))}
      </div>
    </div>
  );
}

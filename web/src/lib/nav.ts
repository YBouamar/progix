export type NavItem = { id: string; label: string; href: string; icon: string; pret?: boolean };

const ITEMS: Record<string, NavItem> = {
  tdb: { id: "tdb", label: "Tableau de bord", href: "/", icon: "M3 3h8v10H3zM13 3h8v6h-8zM13 11h8v10h-8zM3 15h8v6H3z", pret: true },
  projets: { id: "projets", label: "Projets", href: "/projets", pret: true, icon: "M3 7l9-4 9 4-9 4-9-4zM3 12l9 4 9-4M3 17l9 4 9-4" },
  devis: { id: "devis", label: "Devis", href: "/devis", pret: true, icon: "M6 2h9l5 5v15H6zM14 2v6h6M9 13h6M9 17h6" },
  clients: { id: "clients", label: "Clients et partenaires", href: "/clients", pret: true, icon: "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM22 21v-2a4 4 0 0 0-3-3.9M16 3.1a4 4 0 0 1 0 7.8" },
  livraisons: { id: "livraisons", label: "Livraisons", href: "/livraisons", pret: true, icon: "M1 5h14v11H1zM15 9h4l4 4v3h-8zM5.5 19a2 2 0 1 0 0-4 2 2 0 0 0 0 4zM18.5 19a2 2 0 1 0 0-4 2 2 0 0 0 0 4z" },
  facturation: { id: "facturation", label: "Facturation", href: "/facturation", pret: true, icon: "M5 2h14v20l-3-2-2 2-2-2-2 2-2-2-3 2zM9 8h6M9 12h6" },
  tresorerie: { id: "tresorerie", label: "Trésorerie", href: "/tresorerie", pret: true, icon: "M2 6h20v12H2zM12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z" },
  logistique: { id: "logistique", label: "Logistique", href: "/logistique", pret: true, icon: "M21 8l-9-5-9 5v8l9 5 9-5zM3 8l9 5 9-5M12 13v8" },
  repIng: { id: "repIng", label: "Répartition des ingénieurs", href: "/repartition/ingenieurs", pret: true, icon: "M4 4h4v16H4zM10 4h4v10h-4zM16 4h4v13h-4z" },
  repDes: { id: "repDes", label: "Répartition des dessinateurs", href: "/repartition/dessinateurs", pret: true, icon: "M12 19l7-7 3 3-7 7zM18 13l-1.5-7.5L2 2l3.5 14.5L13 18zM2 2l7.6 7.6" },
  perf: { id: "perf", label: "Performance", href: "/performance", pret: true, icon: "M3 3v18h18M7 15l4-5 3 3 5-7" },
  param: { id: "param", label: "Paramétrage", href: "/parametrage", pret: true, icon: "M4 6h10M18 6h2M4 12h4M12 12h8M4 18h12M16 4v4M10 10v4M18 16v4" },
};

const TOUT = Object.keys(ITEMS);
const COMMERCIAL = ["tdb", "projets", "devis", "clients", "livraisons", "facturation", "tresorerie"];

const MENUS: Record<string, string[]> = {
  directeur: TOUT,
  chef_projet: TOUT,
  responsable_administratif: COMMERCIAL,
  administrateur: COMMERCIAL,
  agent_commercial: ["tdb", "projets"],
  ingenieur_chef: ["tdb", "projets", "devis", "repIng", "perf"],
  ingenieur: ["tdb", "projets", "devis"],
  dessinateur_chef: ["tdb", "projets", "repDes", "perf"],
  dessinateur: ["tdb", "projets"],
  responsable_logistique: ["tdb", "projets", "logistique", "param"],
  responsable_chantier: ["tdb", "projets"],
  chef_chantier: ["tdb", "projets"],
};

export function menuPour(role: string): NavItem[] {
  return (MENUS[role] ?? ["tdb"]).map((id) => ITEMS[id]);
}

export function titrePour(pathname: string): string {
  const item = Object.values(ITEMS).filter((i) => i.href !== "/").find((i) => pathname.startsWith(i.href));
  return item?.label ?? "Tableau de bord";
}

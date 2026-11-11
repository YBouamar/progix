export type NavItem = { id: string; label: string; href: string; icon: string; pret?: boolean };

const ITEMS: Record<string, NavItem> = {
  tdb: { id: "tdb", label: "Tableau de bord", href: "/", icon: "M3 3h8v10H3zM13 3h8v6h-8zM13 11h8v10h-8zM3 15h8v6H3z", pret: true },
  projets: { id: "projets", label: "Projets", href: "/projets", pret: true, icon: "M3 7l9-4 9 4-9 4-9-4zM3 12l9 4 9-4M3 17l9 4 9-4" },
  devis: { id: "devis", label: "Devis", href: "/devis", pret: true, icon: "M6 2h9l5 5v15H6zM14 2v6h6M9 13h6M9 17h6" },
  clients: { id: "clients", label: "Clients et partenaires", href: "/clients", pret: true, icon: "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM22 21v-2a4 4 0 0 0-3-3.9M16 3.1a4 4 0 0 1 0 7.8" },
  livraisons: { id: "livraisons", label: "Livraisons", href: "/livraisons", pret: true, icon: "M1 5h14v11H1zM15 9h4l4 4v3h-8zM5.5 19a2 2 0 1 0 0-4 2 2 0 0 0 0 4zM18.5 19a2 2 0 1 0 0-4 2 2 0 0 0 0 4z" },
  facturation: { id: "facturation", label: "Facturation", href: "/facturation", pret: true, icon: "M5 2h14v20l-3-2-2 2-2-2-2 2-2-2-3 2zM9 8h6M9 12h6" },
  tresorerie: { id: "tresorerie", label: "Trésorerie", href: "/tresorerie", pret: true, icon: "M2 6h20v12H2zM12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z" },
  etude: { id: "etude", label: "Étude", href: "/etudes", pret: true, icon: "M3 17l10-10 4 4L7 21H3zM14 6l2-2 4 4-2 2M9 11l4 4" },
  execution: { id: "execution", label: "Exécution", href: "/execution", pret: true, icon: "M4 4h12l4 4v12H4zM16 4v4h4M8 13h8M8 17h5M8 9h3" },
  logistique: { id: "logistique", label: "Logistique", href: "/logistique", pret: true, icon: "M21 8l-9-5-9 5v8l9 5 9-5zM3 8l9 5 9-5M12 13v8" },
  repIng: { id: "repIng", label: "Répartition des ingénieurs", href: "/repartition/ingenieurs", pret: true, icon: "M4 4h4v16H4zM10 4h4v10h-4zM16 4h4v13h-4z" },
  perf: { id: "perf", label: "Performance", href: "/performance", pret: true, icon: "M3 3v18h18M7 15l4-5 3 3 5-7" },
  niveaux: { id: "niveaux", label: "Niveaux des dalles", href: "/niveaux", pret: true, icon: "M4 20h16M4 15h16M4 10h16M4 5h16" },
  param: { id: "param", label: "Paramétrage", href: "/parametrage", pret: true, icon: "M4 6h10M18 6h2M4 12h4M12 12h8M4 18h12M16 4v4M10 10v4M18 16v4" },
};

/** Module d'habilitation de chaque entrée du menu. Le tableau de bord n'en dépend pas. */
const MODULES: Record<string, string> = {
  projets: "projets", devis: "devis", clients: "clients", livraisons: "livraisons", facturation: "facturation", tresorerie: "tresorerie",
  etude: "etude", execution: "execution", logistique: "logistique", repIng: "repartition_ingenieurs", perf: "performance",
  niveaux: "niveaux", param: "parametrage",
};

/** Modules de consultation : ils n'ont pas de niveau « écriture ». */
const CONSULTATION = ["tresorerie", "repartition_ingenieurs", "performance"];

type Droits = { droits?: Record<string, number> };

/** Regroupement du menu par domaine. L'ordre des groupes est celui de l'affichage. */
const GROUPES: { label: string; ids: string[] }[] = [
  { label: "Suivi", ids: ["tdb", "projets"] },
  { label: "Commercial", ids: ["devis", "clients", "livraisons", "facturation", "tresorerie"] },
  { label: "Études et chantier", ids: ["etude", "execution", "logistique", "repIng", "perf", "niveaux"] },
  { label: "Réglages", ids: ["param"] },
];

/** Une entrée apparaît dès que la fonction de la personne a au moins la lecture sur son module. */
const visible = (u: Droits, id: string) => !MODULES[id] || (u.droits?.[MODULES[id]] ?? 0) >= 1;

/** Menu de la personne, par groupes non vides. */
export function groupesPour(u: Droits): { label: string; items: NavItem[] }[] {
  return GROUPES.map((g) => ({ label: g.label, items: g.ids.filter((i) => visible(u, i)).map((i) => ITEMS[i]) })).filter((g) => g.items.length > 0);
}

export function menuPour(u: Droits): NavItem[] {
  return Object.keys(ITEMS).filter((i) => visible(u, i)).map((i) => ITEMS[i]);
}

/** Accès de la personne à la page ouverte : son module, son niveau, et si ce module connaît l'écriture. */
export function accesPage(u: Droits, pathname: string): { module: string; label: string; niveau: number; consultation: boolean } | null {
  const item = Object.values(ITEMS).filter((i) => i.href !== "/").find((i) => pathname.startsWith(i.href));
  const code = item && MODULES[item.id];
  return item && code ? { module: code, label: item.label, niveau: u.droits?.[code] ?? 0, consultation: CONSULTATION.includes(code) } : null;
}

export function titrePour(pathname: string): string {
  const item = Object.values(ITEMS).filter((i) => i.href !== "/").find((i) => pathname.startsWith(i.href));
  return item?.label ?? "Tableau de bord";
}

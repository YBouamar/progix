export type Periode = { debut: string; fin: string; chiffreAffaires: number; encaisse: number; depenses: number; marge: number };
export type Finances = {
  periode: Periode; precedente: Periode;
  retards: { nb: number; total: number; factures: { id: string; numero: string; client: string; echeance: string; jours: number; reste: number }[] };
};
export type LigneDevis = { id: string; numero: string; version: number; totalTTC: number; echeance: string; envoyeLe: string | null; client: string; joursDepuisEnvoi: number; joursAvantExpiration: number };
export type DevisTdb = { enAttente: number; aRelancer: LigneDevis[]; expirent: LigneDevis[]; validesMois: number; envoyesMois: number };
export type ProjetsTdb = {
  total: number; statuts: { code: string; label: string; nb: number }[]; validationsEnAttente: number;
  bloques: { id: string; code: string; libelle: string; motif: string | null; par: string | null; le: string | null }[];
};

const iso = (d: Date) => d.toLocaleDateString("fr-CA");
const mois = (d: Date) => d.toLocaleDateString("fr-FR", { month: "long", year: "numeric" });

/** Périodes proposées : mois en cours, mois précédent, trimestre en cours, année en cours. */
export function periodes() {
  const n = new Date(), y = n.getFullYear(), m = n.getMonth(), t = Math.floor(m / 3);
  return [
    { id: "mois", label: mois(n), debut: iso(new Date(y, m, 1)), fin: iso(new Date(y, m + 1, 0)) },
    { id: "precedent", label: mois(new Date(y, m - 1, 1)), debut: iso(new Date(y, m - 1, 1)), fin: iso(new Date(y, m, 0)) },
    { id: "trimestre", label: `${t + 1}${t === 0 ? "er" : "e"} trimestre ${y}`, debut: iso(new Date(y, t * 3, 1)), fin: iso(new Date(y, t * 3 + 3, 0)) },
    { id: "annee", label: `Année ${y}`, debut: iso(new Date(y, 0, 1)), fin: iso(new Date(y, 12, 0)) },
  ];
}

/** Libellé court d'une période renvoyée par le serveur : « septembre 2026 » pour un mois entier, sinon les deux dates. */
export function libellePeriode(p: { debut: string; fin: string }) {
  const a = new Date(p.debut), b = new Date(p.fin);
  const dernier = new Date(b.getFullYear(), b.getMonth() + 1, 0).getDate();
  if (a.getDate() === 1 && b.getDate() === dernier && a.getMonth() === b.getMonth() && a.getFullYear() === b.getFullYear()) return mois(a);
  return `du ${a.toLocaleDateString("fr-FR")} au ${b.toLocaleDateString("fr-FR")}`;
}

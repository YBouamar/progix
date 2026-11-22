export type DevisListItem = {
  id: string; numero: string; variante: string | null; entite: string; version: number; projetId: string; projet: string; projetCode: string; client: string;
  commercial: string | null; ingenieur: string | null; etat: string; etatLabel: string; dateDevis: string; echeance: string; totalTTC: number; info: string; alerte: string | null;
  /** Où en est le devis : étape de la fiche à ouvrir (1 fichiers, 2 chiffrage, 3 impression et envoi). */
  avancement: { titre: string; detail: string | null; etape: 1 | 2 | 3 };
  /** Devis validé : le devis signé et cacheté par le client est-il déposé ? Null hors devis validé. */
  pdfSigne: boolean | null;
};

export type DevisListe = {
  items: DevisListItem[]; total: number; page: number; pageSize: number;
  compteurs: { etat: string; label: string; nb: number }[];
};

export type Systeme = { torons: number; ancrages: number; gaine: number };
export type Ligne = {
  id: string; dalleId: string; reference: string; emplacement: string; surface: number; ratioActif: number; ratioPassif: number;
  prixM2: number; montantHT: number; systemes: Systeme[];
  /** Brouillon : la dalle a des fichiers ADAPT importés, que le devis peut reprendre. */
  adapt?: boolean;
};
export type Echeance = { label: string; pourcentage: number; retenue: boolean };

export type DevisDetail = {
  id: string; numero: string; variante: string | null; version: number; etat: string; etatLabel: string;
  projetId: string; projet: string; projetCode: string; entite: string; typeConstruction: string; typeLabel: string; client: string; exonereTva: boolean;
  dateDevis: string; dureeValidite: number; echeance: string; commercial: string | null; ingenieurId: string | null; ingenieur: string | null;
  remiseM2: number; remiseMotif: string | null; ratioPassifSaisi: number | null; forfaitPropre: number | null; forfaitType: number | null; echeances: Echeance[]; modalitesContrat: string | null;
  forfait: number; tranchesForfait: Tranche[]; typesAncrage: number[]; surfaceTotale: number; totalHT: number; remiseTotale: number; netHT: number; tva: number; totalTTC: number;
  signePar: string | null; signeLe: string | null; signable: boolean;
  creePar: string; lignes: Ligne[]; info: string; alerte: string | null; annuleMotif: string | null;
  /** Relecture interne du brouillon (null hors brouillon) : etat vaut null (à faire), « validee » ou « refusee ». */
  revue: { etat: "validee" | "refusee" | null; par: string | null; le: string | null; remarque: string | null } | null;
  pdfSigne: boolean | null;
  actions: {
    modifier: boolean; trame: boolean; envoyer: boolean; envoyerPret: boolean; reviser: boolean; supprimer: boolean; deposerSigne: boolean;
    valider: boolean; rejeter: boolean; nouvelleVersion: boolean; dupliquer: boolean;
  };
  historique: { quand: string; quoi: string; qui: string }[];
};

export const TON_ETAT: Record<string, "neutre" | "orange" | "rouge" | "vert" | "violet"> = {
  brouillon: "neutre", envoye: "orange", expire: "rouge", valide: "vert", annule: "rouge", remplace: "violet",
};

/** Tranche de la grille des forfaits : à partir de cette surface totale (m²), le forfait vaut ce prix (DH/m²). */
export type Tranche = { aPartirDe: number; prix: number };

/** Forfait en DH/m² : même règle que le serveur, pour le calcul en direct pendant la saisie. Sous le premier seuil, la première tranche s'applique. */
export function forfaitDe(tranches: Tranche[], surfaceTotale: number) {
  const t = [...tranches].sort((a, b) => a.aPartirDe - b.aPartirDe);
  if (t.length === 0) return 0;
  return (t.filter((x) => x.aPartirDe <= surfaceTotale).pop() ?? t[0]).prix;
}

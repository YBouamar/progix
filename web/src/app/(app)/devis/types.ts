export type DevisListItem = {
  id: string; numero: string; entite: string; version: number; projetId: string; projet: string; projetCode: string; client: string;
  commercial: string | null; etat: string; etatLabel: string; dateDevis: string; echeance: string; totalTTC: number; info: string; alerte: string | null;
};

export type DevisListe = {
  items: DevisListItem[]; total: number; page: number; pageSize: number;
  compteurs: { etat: string; label: string; nb: number }[];
};

export type Systeme = { torons: number; ancrages: number; gaine: number };
export type Ligne = {
  id: string; reference: string; emplacement: string; surface: number; ratioActif: number; ratioPassif: number;
  prixM2: number; montantHT: number; systemes: Systeme[];
};
export type Echeance = { label: string; pourcentage: number; retenue: boolean };

export type DevisDetail = {
  id: string; numero: string; version: number; etat: string; etatLabel: string;
  projetId: string; projet: string; projetCode: string; entite: string; typeConstruction: string; client: string; exonereTva: boolean;
  dateDevis: string; dureeValidite: number; echeance: string; commercialId: string | null; commercial: string | null;
  remiseM2: number; remiseMotif: string | null; echeances: Echeance[]; modalitesContrat: string | null;
  forfait: number; surfaceTotale: number; totalHT: number; remiseTotale: number; netHT: number; tva: number; totalTTC: number;
  signePar: string | null; signeLe: string | null; signable: boolean;
  creePar: string; lignes: Ligne[]; info: string; alerte: string | null; annuleMotif: string | null;
  actions: { modifier: boolean; envoyer: boolean; valider: boolean; annuler: boolean; nouvelleVersion: boolean };
  historique: { quand: string; quoi: string; qui: string }[];
};

export const TON_ETAT: Record<string, "neutre" | "orange" | "rouge" | "vert" | "violet"> = {
  brouillon: "neutre", envoye: "orange", expire: "rouge", valide: "vert", annule: "rouge", remplace: "violet",
};

/** Forfait en DH/m² : même règle que le serveur, pour le calcul en direct pendant la saisie. */
export function forfaitPour(type: string, surfaceTotale: number) {
  if (type === "immeuble") return surfaceTotale >= 10000 ? 85 : 100;
  if (type === "dallage") return 50;
  return 120;
}

export const LABEL_TYPE: Record<string, string> = { immeuble: "Immeuble", villa: "Villa", radier: "Radier", dallage: "Dallage" };

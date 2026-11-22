import { filtrerDecimal, lireNombre } from "./utils";

/** Entités émettrices : la vente directe peut se faire au nom de l'une ou de l'autre, quelle que soit celle du projet. */
export const ENTITES: Record<string, string> = { APT: "Africa PT / MK4", BBR: "BECOMAR" };

export const MODES_PAIEMENT: Record<string, string> = { virement: "Virement", cheque: "Chèque", effet: "Effet (LCN)", especes: "Espèces" };
export const avecImage = (mode: string) => mode === "cheque" || mode === "effet";

export const ORIGINES: Record<string, string> = { achete: "Acheté", produit: "Produit" };

export const TYPES_MOUVEMENT: Record<string, string> = { entree: "Entrée", production: "Production", sortie: "Sortie", inventaire: "Inventaire", correction: "Correction" };

export type Ton = "neutre" | "orange" | "vert" | "rouge" | "violet";

export const tonEtatAchat = (e: string): Ton => (e === "recue" ? "vert" : e === "partielle" ? "orange" : e === "annulee" ? "rouge" : "neutre");
export const tonEtatVente = (e: string): Ton => (e === "annule" || e === "refuse" ? "rouge" : e === "accepte" || e === "emis" ? "vert" : "orange");
export const tonStatutFacture = (s: string): Ton => (s === "payee" ? "vert" : s === "partielle" ? "orange" : s === "annulee" ? "rouge" : "neutre");
export const LABEL_STATUT_FACTURE: Record<string, string> = { payee: "Payée", partielle: "Partiellement payée", impayee: "À payer", annulee: "Annulée" };

/** Quantité au format français, sans zéros inutiles : 12,5. */
export const formatQuantite = (n: number) => n.toLocaleString("fr-FR", { maximumFractionDigits: 4 }).replace(/[  ]/g, " ");

/** Saisie décimale : texte libre côté champ, nombre côté appel. */
export { filtrerDecimal, lireNombre };

export type Option = { id: string; libelle: string; symbole?: string; actif: boolean; ordre?: number };

export type Article = {
  id: string; code: string; designation: string; categorieId: string; categorie: string | null; uniteId: string; unite: string | null; uniteSymbole: string | null;
  prixVente: number; origine: string; coutMoyen: number | null; valeurStock: number | null; seuilAlerte: number; kgParMetre: number | null; actif: boolean; stock: number; sousSeuil: boolean;
};

export type LigneEdition = { cle: string; articleId: string; quantite: string; prix: string };

export const nouvelleLigne = (): LigneEdition => ({ cle: Math.random().toString(36).slice(2), articleId: "", quantite: "1", prix: "" });

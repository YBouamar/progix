export type Ref = { code: string; label: string; forfait?: string };
export type Referentiel = { entites: Ref[]; types: Ref[]; statuts: Ref[] };

export type ProjetListItem = {
  id: string; code: string; libelle: string; client: string; ville: string | null; dateCreation: string; entite: string;
  typeConstruction: string; typeLabel: string; statut: string; statutLabel: string;
  bloque: boolean; supprime: boolean; suppressionInfo: string | null; nbDalles: number; surface: number; ingenieurs: string; dallesRealisees: number;
};

export type ProjetListe = {
  items: ProjetListItem[]; total: number; page: number; pageSize: number;
  compteurs: { statut: string; label: string; nb: number }[];
};

export type ProjetDetail = {
  id: string; code: string; libelle: string; entite: string; entiteLabel: string;
  clientId: string; client: string; proprietaireId: string | null; proprietaireLibre: string | null; proprietaire: string;
  adresse: string | null; ville: string | null; dateCreation: string;
  typeConstruction: string; typeLabel: string; forfait: string; description: string | null;
  statut: string; statutLabel: string;
  bloque: boolean; blocageMotif: string | null; blocagePar: string | null; blocageLe: string | null;
  supprime: boolean; suppressionMotif: string | null; suppressionPar: string | null; suppressionLe: string | null;
  creePar: string; createdAt: string;
};

export const TON_STATUT: Record<string, "neutre" | "violet" | "orange" | "vert"> = {
  cree: "neutre", etude: "violet", realisation: "orange", termine: "vert",
};

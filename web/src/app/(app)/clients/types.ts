export type Compteur = { type: string; label: string; nb: number };

export type PartenaireListItem = {
  id: string; type: string; typeLabel: string; nature: "morale" | "physique" | "autre"; nom: string;
  identifiant: string | null; ville: string | null; telephone: string | null; email: string | null;
  exonereTva: boolean; archive: boolean; nbContacts: number;
};

export type Contact = { id?: string | null; nom: string; fonction: string | null; telephone: string | null; email: string | null };

export type PartenaireForm = {
  type: string; nature: "morale" | "physique" | "autre";
  raisonSociale: string; ice: string; rc: string; identifiantFiscal: string; patente: string;
  civilite: string; nomFamille: string; prenom: string; cin: string; nationalite: string;
  dateNaissance: string; lieuNaissance: string;
  adresse: string; ville: string; telephone: string; email: string;
  exonereTva: boolean; attestationNumero: string; attestationExpiration: string; contacts: Contact[];
};

export const FORM_VIDE: PartenaireForm = {
  type: "client_final", nature: "morale",
  raisonSociale: "", ice: "", rc: "", identifiantFiscal: "", patente: "",
  civilite: "M.", nomFamille: "", prenom: "", cin: "", nationalite: "Marocaine", dateNaissance: "", lieuNaissance: "",
  adresse: "", ville: "", telephone: "", email: "", exonereTva: false, attestationNumero: "", attestationExpiration: "", contacts: [],
};

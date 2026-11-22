import { api, ApiError } from "@/lib/api";

/** Taille des morceaux envoyés : celle du serveur (8 Mo). Le stockage exige au moins 5 Mo, sauf pour le dernier morceau. */
export const TAILLE_MORCEAU = 8 * 1024 * 1024;

/**
 * Envoi d'un fichier par morceaux : ouverture, morceaux, clôture. Aucun gros fichier ne transite en une seule requête.
 * Sert au composant Documents et aux écrans qui joignent un fichier à la création d'un objet (image d'un chèque, par exemple).
 */
export async function envoyerFichier<T = unknown>(contexte: string, cibleId: string, categorie: string | undefined, fichier: File,
  tailleMorceau: number = TAILLE_MORCEAU, suivi?: (pct: number) => void): Promise<T> {
  const { id } = await api<{ id: string }>("/fichiers/envois", {
    method: "POST", body: JSON.stringify({ contexte, cibleId, categorie, nom: fichier.name, taille: fichier.size, contentType: fichier.type }),
  });
  const morceaux: { numero: number; eTag: string }[] = [];
  const total = Math.ceil(fichier.size / tailleMorceau);
  for (let n = 1; n <= total; n++) {
    const part = fichier.slice((n - 1) * tailleMorceau, n * tailleMorceau);
    const res = await fetch(`/api/fichiers/envois/${id}/morceaux/${n}`, {
      method: "PUT", body: part, credentials: "include", headers: { "Content-Type": "application/octet-stream" },
    });
    if (!res.ok) throw new ApiError(res.status, res.status >= 500 ? "Le stockage de fichiers ne répond pas." : "Dépôt impossible.");
    morceaux.push(await res.json());
    suivi?.(Math.round((n / total) * 100));
  }
  return api<T>(`/fichiers/envois/${id}/terminer`, { method: "POST", body: JSON.stringify({ morceaux }) });
}

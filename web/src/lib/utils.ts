import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export function initiales(nom: string) {
  return nom.split(/\s+/).filter((m) => /^\p{L}/u.test(m)).slice(0, 2).map((p) => p[0]!.toUpperCase()).join("");
}

/** Dates au format JJ/MM/AAAA. */
export function formatDate(iso?: string | null) {
  if (!iso) return "";
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? "" : d.toLocaleDateString("fr-FR");
}

/** Date et heure au format JJ/MM/AAAA à HH:MM. */
export function formatDateHeure(iso?: string | null) {
  if (!iso) return "";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  return `${d.toLocaleDateString("fr-FR")} à ${d.toLocaleTimeString("fr-FR", { hour: "2-digit", minute: "2-digit" })}`;
}

/** Nombre au format français : 1 234,56. */
export function formatNombre(n: number) {
  return n.toLocaleString("fr-FR", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).replace(/[\u202f\u00a0]/g, " ");
}

export const formatSurface = (n: number) => `${formatNombre(n)} m²`;

export const formatDH = (n: number) => `${formatNombre(n)} DH`;

/** Lit un nombre saisi à la française : « 1 234,5 ». */
export function lireNombre(s: string) {
  const n = parseFloat(String(s).replace(/\s/g, "").replace(",", "."));
  return Number.isFinite(n) ? n : 0;
}

/** Taille de fichier lisible : 8,4 Mo. */
export function formatTaille(octets: number) {
  if (octets < 1024) return `${octets} o`;
  if (octets < 1024 * 1024) return `${Math.round(octets / 1024)} Ko`;
  if (octets < 1024 * 1024 * 1024) return `${(octets / 1024 / 1024).toLocaleString("fr-FR", { maximumFractionDigits: 1 })} Mo`;
  return `${(octets / 1024 / 1024 / 1024).toLocaleString("fr-FR", { maximumFractionDigits: 1 })} Go`;
}

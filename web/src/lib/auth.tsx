"use client";

import { viderCacheHorsLigne } from "@/components/hors-ligne";
import { createContext, useCallback, useContext, useEffect, useState } from "react";
import { api } from "./api";

export type User = {
  id: string; fullName: string; email: string; login: string; role: string; roleLabel: string;
  voitMontants: boolean; isActive: boolean; phoneNumber: string | null;
  /** Code de la fonction de la personne ; `role` en est la fonction de référence, l'une des douze fonctions d'origine. */
  fonction: string;
  /** Niveau d'accès par module, réglé dans les habilitations : 0 aucun, 1 lecture, 2 écriture. */
  droits: Record<string, number>;
};

type AuthState = {
  user: User | null;
  loading: boolean;
  login: (identifiant: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
};

const Ctx = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api<User>("/auth/me").then(setUser).catch(() => setUser(null)).finally(() => setLoading(false));
  }, []);

  const login = useCallback(async (identifiant: string, password: string) => {
    setUser(await api<User>("/auth/login", { method: "POST", body: JSON.stringify({ login: identifiant, password }) }));
  }, []);

  const logout = useCallback(async () => {
    await api("/auth/logout", { method: "POST" }).catch(() => undefined);
    viderCacheHorsLigne();
    setUser(null);
  }, []);

  return <Ctx.Provider value={{ user, loading, login, logout }}>{children}</Ctx.Provider>;
}

export function useAuth() {
  const v = useContext(Ctx);
  if (!v) throw new Error("useAuth doit être utilisé dans AuthProvider");
  return v;
}

const COMMERCIAL = ["directeur", "chef_projet", "responsable_administratif", "administrateur"];
/** Niveau d'accès de la personne sur un module : 0 aucun, 1 lecture, 2 écriture. */
export const niveauSur = (u: User | null, module: string) => u?.droits?.[module] ?? 0;
export const peutLire = (u: User | null, module: string) => niveauSur(u, module) >= 1;
export const peutEcrire = (u: User | null, module: string) => niveauSur(u, module) >= 2;
/** Création et modification des projets et des dalles : fonctions commerciales, si le module Projets leur est ouvert en écriture. */
export const peutGererCommercial = (u: User | null) => !!u && COMMERCIAL.includes(u.role) && peutEcrire(u, "projets");
export const peutDiriger = (u: User | null) => !!u && ["directeur", "chef_projet"].includes(u.role);
const MONTANTS = ["directeur", "chef_projet", "responsable_administratif", "administrateur", "ingenieur_chef", "ingenieur"];
export const voitMontants = (u: User | null) => !!u && MONTANTS.includes(u.role);

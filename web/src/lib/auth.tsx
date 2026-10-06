"use client";

import { viderCacheHorsLigne } from "@/components/hors-ligne";
import { createContext, useCallback, useContext, useEffect, useState } from "react";
import { api } from "./api";

export type User = {
  id: string; fullName: string; email: string; login: string; role: string; roleLabel: string;
  voitMontants: boolean; isActive: boolean; phoneNumber: string | null;
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
export const peutGererCommercial = (u: User | null) => !!u && COMMERCIAL.includes(u.role);
export const peutDiriger = (u: User | null) => !!u && ["directeur", "chef_projet"].includes(u.role);
const MONTANTS = ["directeur", "chef_projet", "responsable_administratif", "administrateur", "ingenieur_chef", "ingenieur"];
export const voitMontants = (u: User | null) => !!u && MONTANTS.includes(u.role);

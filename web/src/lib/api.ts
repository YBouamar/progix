export class ApiError extends Error {
  constructor(public status: number, message: string, public errors?: Record<string, string[]>, public data?: unknown) {
    super(message);
  }
}

export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  let res: Response;
  try {
    res = await fetch(`/api${path}`, {
      ...init,
      credentials: "include",
      headers: { "Content-Type": "application/json", ...(init?.headers ?? {}) },
    });
  } catch {
    throw new ApiError(0, "Connexion au serveur impossible. Vérifiez le réseau puis réessayez.");
  }
  if (res.status === 204) return undefined as T;
  const text = await res.text();
  let data: unknown = undefined;
  try { data = text ? JSON.parse(text) : undefined; } catch { /* réponse non JSON */ }
  if (!res.ok) {
    const d = (data ?? {}) as { message?: string; detail?: string; title?: string; errors?: Record<string, string[]> };
    const message = d.message ?? d.detail ?? (res.status === 403 ? "Vous n'avez pas le droit d'effectuer cette action."
      : res.status >= 500 ? "Le serveur a rencontré une erreur." : d.title ?? "La demande a échoué.");
    throw new ApiError(res.status, message, d.errors, data);
  }
  return data as T;
}

export type Paged<T> = { items: T[]; total: number; page: number; pageSize: number };

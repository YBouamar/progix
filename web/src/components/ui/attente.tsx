import { cn } from "@/lib/utils";

/** Lignes d'attente affichées à la place d'une liste pendant son chargement. */
export function AttenteLignes({ lignes = 6, className }: { lignes?: number; className?: string }) {
  return (
    <div role="status" aria-label="Chargement" className={cn("divide-y divide-bordure", className)}>
      {Array.from({ length: lignes }, (_, i) => (
        <div key={i} className="flex items-center gap-6 px-5 py-4">
          <span className="attente h-4 w-24 shrink-0" />
          <span className="attente h-4 flex-1" style={{ maxWidth: `${52 - (i % 3) * 9}%` }} />
          <span className="attente ml-auto h-4 w-20 shrink-0 max-sm:hidden" />
          <span className="attente h-4 w-16 shrink-0 max-sm:hidden" />
        </div>
      ))}
    </div>
  );
}

/** Bloc d'attente pour une fiche : un titre et quelques lignes. */
export function AttenteFiche({ className }: { className?: string }) {
  return (
    <div role="status" aria-label="Chargement" className={cn("rounded-xl border border-bordure bg-white p-6", className)}>
      <span className="attente block h-6 w-64 max-w-full" />
      <span className="attente mt-3 block h-4 w-40" />
      <div className="mt-8 grid gap-4 sm:grid-cols-3">
        {Array.from({ length: 6 }, (_, i) => <span key={i} className="attente block h-12" />)}
      </div>
    </div>
  );
}

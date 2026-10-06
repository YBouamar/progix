"use client";

import { useEffect, useRef, useState } from "react";
import { api, type Paged } from "@/lib/api";
import { Input } from "@/components/ui/input";

type Option = { id: string; nom: string; typeLabel: string };

/**
 * Recherche d'un client ou partenaire dans la liste.
 * Avec `libre`, le texte saisi est accepté tel quel quand aucun élément n'est choisi.
 */
export function PartenaireCombo({ id, texte, onChange, libre, invalid, placeholder, disabled, type, autoFocus }: {
  id: string | null; texte: string; onChange: (id: string | null, texte: string) => void;
  libre?: boolean; invalid?: boolean; placeholder?: string; disabled?: boolean; type?: string; autoFocus?: boolean;
}) {
  const [ouvert, setOuvert] = useState(false);
  const [options, setOptions] = useState<Option[]>([]);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!ouvert) return;
    const t = setTimeout(() => {
      const p = new URLSearchParams({ pageSize: "8" });
      if (type) p.set("type", type);
      if (texte.trim() && !id) p.set("q", texte.trim());
      api<Paged<Option>>(`/partenaires?${p}`).then((r) => setOptions(r.items)).catch(() => setOptions([]));
    }, 200);
    return () => clearTimeout(t);
  }, [texte, ouvert, id, type]);

  useEffect(() => {
    const clic = (e: MouseEvent) => { if (!ref.current?.contains(e.target as Node)) setOuvert(false); };
    document.addEventListener("mousedown", clic);
    return () => document.removeEventListener("mousedown", clic);
  }, []);

  return (
    <div ref={ref} className="relative">
      <Input value={texte} disabled={disabled} aria-invalid={invalid} placeholder={placeholder} autoComplete="off" autoFocus={autoFocus}
        onFocus={() => setOuvert(true)}
        onChange={(e) => { onChange(null, e.target.value); setOuvert(true); }}
        onBlur={() => { if (!libre && !id) onChange(null, ""); }} />
      {ouvert && options.length > 0 && (
        <ul className="absolute z-20 mt-1 max-h-64 w-full overflow-y-auto rounded-lg border border-bordure-forte bg-white py-1 shadow-lg">
          {options.map((o) => (
            <li key={o.id}>
              <button type="button" onMouseDown={(e) => { e.preventDefault(); onChange(o.id, o.nom); setOuvert(false); }}
                className="flex w-full cursor-pointer items-center justify-between gap-3 px-3 py-2 text-left hover:bg-fond">
                <span className="truncate font-semibold">{o.nom}</span>
                <span className="shrink-0 text-[12px] text-attenue">{o.typeLabel}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

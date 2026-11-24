"use client";

import Link from "next/link";
import { cn } from "@/lib/utils";

/** Carte d'indicateur : libellé, valeur, évolution éventuelle et règle de calcul. */
export function Carte({ label, valeur, evolution, aide, ton, href }: { label: string; valeur: string; evolution?: { avant: number; v: number; hausseBonne: boolean } | null; aide?: string; ton?: "rouge" | "orange"; href?: string }) {
  const delta = evolution && evolution.avant > 0 ? (evolution.v - evolution.avant) / evolution.avant * 100 : null;
  const bon = delta === null || delta === 0 ? null : (delta > 0) === evolution!.hausseBonne;
  const corps = (
    <div className="h-full rounded-xl border border-bordure bg-white p-5">
      <div className="flex items-start justify-between gap-2">
        <div className="text-[13px] text-secondaire">{label}</div>
        {delta !== null && <span className={cn("font-display text-[13px] font-bold", bon === null ? "text-secondaire" : bon ? "text-vert-texte" : "text-orange-texte")}>{delta > 0 ? "+" : ""}{delta.toLocaleString("fr-FR", { maximumFractionDigits: 1 })} %</span>}
      </div>
      <div className={cn("mt-1 font-display text-xl font-bold", ton === "rouge" && "text-rouge-texte", ton === "orange" && "text-orange-texte")}>{valeur}</div>
      {aide && <div className="mt-1 text-[12px] text-attenue">{aide}</div>}
    </div>
  );
  return href ? <Link href={href} className="block transition-opacity hover:opacity-80">{corps}</Link> : corps;
}

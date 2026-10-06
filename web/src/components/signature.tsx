"use client";

import { useEffect, useRef } from "react";
import { Button } from "@/components/ui/button";

/** Zone de signature au doigt ou au stylet. La valeur est une image PNG encodée. */
export function Signature({ valeur, onChange, disabled }: { valeur: string | null; onChange: (v: string | null) => void; disabled?: boolean }) {
  const ref = useRef<HTMLCanvasElement>(null);
  const trace = useRef(false);

  useEffect(() => {
    const c = ref.current;
    if (!c) return;
    const ctx = c.getContext("2d")!;
    ctx.clearRect(0, 0, c.width, c.height);
    if (valeur) { const img = new Image(); img.onload = () => ctx.drawImage(img, 0, 0, c.width, c.height); img.src = valeur; }
    // L'image n'est redessinée qu'à l'ouverture ou après effacement, pas à chaque trait.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [valeur === null]);

  const point = (e: React.PointerEvent<HTMLCanvasElement>) => {
    const c = ref.current!, r = c.getBoundingClientRect();
    return { x: ((e.clientX - r.left) / r.width) * c.width, y: ((e.clientY - r.top) / r.height) * c.height };
  };

  return (
    <div>
      <canvas ref={ref} width={600} height={180} aria-label="Signature du client"
        className={`w-full touch-none rounded-lg border bg-white ${disabled ? "border-bordure" : "cursor-crosshair border-bordure-forte"}`}
        onPointerDown={(e) => {
          if (disabled) return;
          const ctx = ref.current!.getContext("2d")!, p = point(e);
          ctx.lineWidth = 2.5; ctx.lineCap = "round"; ctx.lineJoin = "round"; ctx.strokeStyle = "#1a1d2e";
          ctx.beginPath(); ctx.moveTo(p.x, p.y); trace.current = true;
          ref.current!.setPointerCapture(e.pointerId);
        }}
        onPointerMove={(e) => { if (!trace.current) return; const ctx = ref.current!.getContext("2d")!, p = point(e); ctx.lineTo(p.x, p.y); ctx.stroke(); }}
        onPointerUp={() => { if (!trace.current) return; trace.current = false; onChange(ref.current!.toDataURL("image/png")); }} />
      {!disabled && (
        <div className="mt-1 flex items-center justify-between text-[12px] text-secondaire">
          <span>Signature du client : au doigt sur tablette, ou à la souris. Elle est obligatoire pour valider l&apos;étape.</span>
          <Button variant="ghost" size="sm" disabled={!valeur} onClick={() => onChange(null)}>Effacer</Button>
        </div>
      )}
    </div>
  );
}

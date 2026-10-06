"use client";

import { useEffect, useState } from "react";
import { WifiOff } from "lucide-react";

/** Enregistre le service worker et prévient quand la tablette n'a plus de réseau. */
export function HorsLigne() {
  const [coupe, setCoupe] = useState(false);

  useEffect(() => {
    if ("serviceWorker" in navigator) navigator.serviceWorker.register("/sw.js").catch(() => undefined);
    const maj = () => setCoupe(!navigator.onLine);
    maj();
    window.addEventListener("online", maj);
    window.addEventListener("offline", maj);
    return () => { window.removeEventListener("online", maj); window.removeEventListener("offline", maj); };
  }, []);

  if (!coupe) return null;
  return (
    <div role="status" className="fixed inset-x-0 bottom-0 z-[100] flex items-center justify-center gap-2 bg-orange-fond px-4 py-2 text-[13px] font-medium text-orange-texte shadow-[0_-1px_4px_rgba(0,0,0,0.12)]">
      <WifiOff size={16} aria-hidden />
      Hors ligne : consultation des écrans déjà ouverts. Les saisies et validations reprendront au retour du réseau.
    </div>
  );
}

/** À la déconnexion : les données gardées pour le hors ligne sont effacées de l'appareil. */
export function viderCacheHorsLigne() {
  if (typeof navigator !== "undefined" && "serviceWorker" in navigator) navigator.serviceWorker.controller?.postMessage("vider");
}

"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import * as Dialog from "@radix-ui/react-dialog";
import { Bell, BellOff, BellRing } from "lucide-react";
import { api } from "@/lib/api";
import { cn, formatDateHeure } from "@/lib/utils";
import { Button } from "@/components/ui/button";

type Avis = { id: string; genre: "a_tour" | "a_valider" | "affecte"; le: string; titre: string; texte: string; par: string | null; lien: string };
type Reponse = { maintenant: string; items: Avis[] };

/** Intervalle de relecture, en millisecondes. */
const PERIODE = 60_000;
const cleVu = (userId: string) => `progix-notifications-vu-${userId}`;

function lire(cle: string) { try { return localStorage.getItem(cle); } catch { return null; } }
function ecrire(cle: string, v: string) { try { localStorage.setItem(cle, v); } catch { /* stockage indisponible */ } }

/** Affiche une notification du navigateur ; sur téléphone, elle passe par le service worker. */
async function afficher(a: Avis, ouvrir: (lien: string) => void) {
  const options = { body: a.texte, tag: a.id, icon: "/icone-192.png", data: { lien: a.lien } };
  try {
    const n = new Notification(a.titre, options);
    n.onclick = () => { window.focus(); ouvrir(a.lien); n.close(); };
  } catch {
    const sw = await navigator.serviceWorker?.getRegistration();
    await sw?.showNotification(a.titre, options).catch(() => undefined);
  }
}

/**
 * Cloche de l'en-tête : avis de changement d'étape des sept derniers jours pour la personne connectée,
 * relus toutes les minutes tant que PROGIX est ouvert. Chaque nouvel avis déclenche une notification du navigateur, si elle est autorisée.
 */
export function Notifications({ userId }: { userId: string }) {
  const router = useRouter();
  const [avis, setAvis] = useState<Avis[]>([]);
  const [vu, setVu] = useState<string | null>(null);
  const [permission, setPermission] = useState<NotificationPermission | "absente">("default");
  const dejaMontres = useRef<Set<string> | null>(null);

  useEffect(() => {
    setPermission(typeof Notification === "undefined" ? "absente" : Notification.permission);
    setVu(lire(cleVu(userId)));
  }, [userId]);

  const relire = useCallback(async () => {
    let r: Reponse;
    try { r = await api<Reponse>("/notifications/etapes"); } catch { return; }
    setAvis(r.items);
    // Première lecture de la session : ce qui existe déjà ne sonne pas, seuls les avis apparus ensuite déclenchent une notification.
    if (dejaMontres.current === null) { dejaMontres.current = new Set(r.items.map((a) => a.id)); return; }
    const nouveaux = r.items.filter((a) => !dejaMontres.current!.has(a.id));
    nouveaux.forEach((a) => dejaMontres.current!.add(a.id));
    if (typeof Notification !== "undefined" && Notification.permission === "granted")
      for (const a of nouveaux.slice(0, 5).reverse()) await afficher(a, (lien) => router.push(lien));
  }, [router]);

  useEffect(() => {
    dejaMontres.current = null;
    void relire();
    const t = setInterval(() => void relire(), PERIODE);
    return () => clearInterval(t);
  }, [relire, userId]);

  const nonLus = avis.filter((a) => !vu || a.le > vu).length;
  const marquerVus = () => { const t = new Date().toISOString(); ecrire(cleVu(userId), t); setVu(t); };
  const activer = async () => setPermission(await Notification.requestPermission());
  const Icone = permission === "granted" ? (nonLus > 0 ? BellRing : Bell) : BellOff;

  return (
    <Dialog.Root onOpenChange={(o) => { if (!o) marquerVus(); }}>
      <Dialog.Trigger asChild>
        <button type="button" aria-label={nonLus > 0 ? `Notifications : ${nonLus} nouvelle${nonLus > 1 ? "s" : ""}` : "Notifications"} title="Notifications"
          className="relative cursor-pointer rounded-lg p-2 text-secondaire hover:bg-neutre-fond hover:text-texte">
          <Icone size={20} aria-hidden />
          {nonLus > 0 && <span className="absolute -right-0.5 -top-0.5 rounded-full bg-gaine px-1.5 py-0.5 font-display text-[11px] font-bold leading-none text-marine">{nonLus > 9 ? "9+" : nonLus}</span>}
        </button>
      </Dialog.Trigger>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-40" />
        <Dialog.Content className="fixed right-3 top-[60px] z-50 w-[min(94vw,400px)] rounded-xl border border-bordure bg-white shadow-xl focus:outline-none lg:right-8">
          <div className="border-b border-bordure px-4 py-3">
            <Dialog.Title className="text-[15px] font-bold">Notifications</Dialog.Title>
            <Dialog.Description className="text-[12px] text-secondaire">Les étapes qui attendent votre fonction et vos nouvelles affectations, depuis sept jours.</Dialog.Description>
          </div>
          {permission !== "granted" && (
            <div className="border-b border-bordure bg-fond px-4 py-3 text-[13px]">
              {permission === "default" && (
                <div className="flex flex-wrap items-center gap-3">
                  <span className="min-w-[180px] flex-1">Recevez un avis du navigateur à chaque étape qui vous revient, tant que PROGIX est ouvert.</span>
                  <Button size="sm" onClick={activer}>Activer</Button>
                </div>
              )}
              {permission === "denied" && <span>Les notifications sont bloquées pour ce site : autorisez-les dans les réglages du navigateur (cadenas de la barre d&apos;adresse), puis rechargez la page.</span>}
              {permission === "absente" && <span>Ce navigateur n&apos;affiche pas de notifications : les avis restent consultables ici.</span>}
            </div>
          )}
          <ul className="max-h-[60vh] overflow-y-auto">
            {avis.map((a) => (
              <li key={a.id} className="border-b border-bordure last:border-0">
                <Dialog.Close asChild>
                  <button type="button" onClick={() => router.push(a.lien)} className={cn("block w-full cursor-pointer px-4 py-3 text-left hover:bg-fond", (!vu || a.le > vu) && "bg-orange-fond/40")}>
                    <div className="text-[13px] font-bold text-marine">{a.titre}</div>
                    <div className="text-[13px]">{a.texte}</div>
                    <div className="mt-0.5 text-[12px] text-secondaire">{formatDateHeure(a.le)}{a.par ? ` · ${a.par}` : ""}</div>
                  </button>
                </Dialog.Close>
              </li>
            ))}
          </ul>
          {avis.length === 0 && <p className="px-4 py-8 text-center text-[13px] text-secondaire">Aucune étape ni affectation nouvelle depuis sept jours.</p>}
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}

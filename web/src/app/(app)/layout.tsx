"use client";

import { useEffect, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import * as Dialog from "@radix-ui/react-dialog";
import { LogOut, Menu, PanelLeftClose, PanelLeftOpen, Search, X } from "lucide-react";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { accesPage, groupesPour, titrePour } from "@/lib/nav";
import { cn } from "@/lib/utils";
import { MonCompte } from "@/components/mon-compte";
import { Recherche } from "@/components/recherche";
import { Notifications } from "@/components/notifications";

const CLE_MENU = "progix-menu-replie";

export default function AppLayout({ children }: { children: React.ReactNode }) {
  const { user, loading, logout } = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  const [replie, setReplie] = useState(false);
  const [tiroir, setTiroir] = useState(false);
  const [recherche, setRecherche] = useState(false);

  useEffect(() => { if (!loading && !user) router.replace("/login"); }, [loading, user, router]);
  // Le choix du menu replié est gardé sur l'appareil.
  useEffect(() => { try { setReplie(localStorage.getItem(CLE_MENU) === "1"); } catch { /* stockage indisponible */ } }, []);
  // Le menu mobile se referme à chaque changement de page.
  useEffect(() => { setTiroir(false); }, [pathname]);
  // Chiffres du menu : ce qui reste à traiter, au nom de la personne ou au total selon sa fonction. Relus à chaque changement de page.
  const [aTraiter, setATraiter] = useState<Record<string, number>>({});
  const voitDevis = (user?.droits?.devis ?? 0) >= 1;
  const connecte = !!user;
  useEffect(() => {
    if (!connecte) return;
    let actuel = true;
    Promise.all([
      api<Record<string, number>>("/a-traiter").catch(() => ({} as Record<string, number>)),
      voitDevis ? api<{ nb: number }>("/devis/a-traiter").then((r) => ({ devis: r.nb })).catch(() => ({})) : Promise.resolve({}),
    ]).then(([a, b]) => { if (actuel) setATraiter({ ...a, ...b }); });
    return () => { actuel = false; };
  }, [pathname, connecte, voitDevis]);

  if (loading || !user) {
    return (
      <div role="status" aria-label="Chargement" className="flex min-h-screen items-center justify-center">
        <Image src="/marque/icone.png" alt="" width={56} height={56} priority className="attente h-14 w-14 bg-transparent" />
      </div>
    );
  }

  const groupes = groupesPour(user);
  const acces = accesPage(user, pathname);
  const actif = (href: string) => (href === "/" ? pathname === "/" : pathname.startsWith(href));
  const basculer = () => setReplie((r) => { try { localStorage.setItem(CLE_MENU, r ? "0" : "1"); } catch { /* idem */ } return !r; });

  /** Contenu du menu, partagé entre la barre latérale et le tiroir mobile. `court` : icônes seules. */
  const menu = (court: boolean) => (
    <>
      <nav aria-label="Menu principal" className={cn("flex-1 overflow-y-auto pb-3", court ? "px-2" : "px-3")}>
        {groupes.map((g, gi) => (
          <div key={g.label} className={cn(gi > 0 && "mt-4")}>
            {/* Les titres de groupe n'apparaissent que si le menu en compte plusieurs. */}
            {groupes.length > 1 && (court
              ? gi > 0 && <div className="mx-2 mb-3 border-t border-white/15" />
              : <div className="px-3 pb-1.5 text-[12px] font-semibold text-white/50">{g.label}</div>)}
            <div className="space-y-0.5">
              {g.items.map((m) => (
                <Link key={m.id} href={m.href} title={court ? m.label : undefined} aria-current={actif(m.href) ? "page" : undefined}
                  className={cn("relative flex items-center gap-3 rounded-lg py-2.5 text-[14px] transition-colors",
                    court ? "justify-center px-0" : "px-3",
                    actif(m.href) ? "bg-white/15 font-bold text-white" : "font-medium text-[#dfe3f5] hover:bg-white/10")}>
                  {actif(m.href) && <span className="absolute inset-y-2 left-0 w-[3px] rounded-full bg-gaine" aria-hidden />}
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden className="shrink-0">
                    <path d={m.icon} />
                  </svg>
                  {court ? <span className="sr-only">{m.label}</span> : <span className="truncate">{m.label}</span>}
                  {aTraiter[m.id] > 0 && (
                    <span title={`${aTraiter[m.id]} à traiter`}
                      className={cn("rounded-full bg-gaine font-display text-[11px] font-bold leading-none text-marine", court ? "absolute right-1 top-1 px-1 py-0.5" : "ml-auto px-1.5 py-1")}>
                      {aTraiter[m.id] > 99 ? "99+" : aTraiter[m.id]}<span className="sr-only"> à traiter</span>
                    </span>
                  )}
                </Link>
              ))}
            </div>
          </div>
        ))}
      </nav>
      <div className={cn("flex items-center gap-1 border-t border-white/15 px-2 py-2", court && "flex-col")}>
        {!court && <MonCompte />}
        <button onClick={() => logout()} title="Se déconnecter" aria-label="Se déconnecter"
          className="cursor-pointer rounded-lg p-2 text-white/70 hover:bg-white/10 hover:text-white">
          <LogOut size={16} />
        </button>
      </div>
    </>
  );

  const marque = (court: boolean) => (
    <Link href="/" className={cn("flex items-center gap-3 py-5", court ? "justify-center px-0" : "px-5")} aria-label="PROGIX, tableau de bord">
      <Image src="/marque/icone.png" alt="" width={40} height={40} priority className="h-10 w-10 shrink-0" />
      {!court && <span className="font-display text-xl font-bold tracking-wide">PROGIX</span>}
    </Link>
  );

  return (
    <div className="flex min-h-screen" style={{ "--largeur-menu": replie ? "68px" : "248px" } as React.CSSProperties}>
      <a href="#contenu" className="sr-only focus:not-sr-only focus:fixed focus:left-3 focus:top-3 focus:z-[70] focus:rounded-lg focus:bg-white focus:px-4 focus:py-2 focus:font-semibold focus:text-marine">
        Aller au contenu
      </a>

      {/* Barre latérale : écrans larges. Repliable pour laisser la place aux tableaux. */}
      <aside className={cn("sticky top-0 hidden h-screen shrink-0 flex-col bg-marine text-white transition-[width] duration-150 lg:flex", replie ? "w-[68px]" : "w-[248px]")}>
        {marque(replie)}
        {menu(replie)}
      </aside>

      {/* Tiroir : tablettes et téléphones. */}
      <Dialog.Root open={tiroir} onOpenChange={setTiroir}>
        <Dialog.Portal>
          <Dialog.Overlay className="fixed inset-0 z-40 bg-texte/40 lg:hidden" />
          <Dialog.Content className="fixed inset-y-0 left-0 z-50 flex w-[280px] max-w-[85vw] flex-col bg-marine text-white shadow-2xl focus:outline-none lg:hidden">
            <Dialog.Title className="sr-only">Menu</Dialog.Title>
            <Dialog.Description className="sr-only">Navigation de l&apos;application</Dialog.Description>
            <div className="flex items-center justify-between pr-3">
              {marque(false)}
              <Dialog.Close aria-label="Fermer le menu" className="cursor-pointer rounded-lg p-2 text-white/70 hover:bg-white/10 hover:text-white"><X size={18} /></Dialog.Close>
            </div>
            {menu(false)}
          </Dialog.Content>
        </Dialog.Portal>
      </Dialog.Root>

      <div className="min-w-0 flex-1">
        <header className="sticky top-0 z-10 flex h-16 items-center gap-3 border-b border-bordure bg-white px-4 lg:px-8">
          <button type="button" onClick={() => setTiroir(true)} aria-label="Ouvrir le menu"
            className="cursor-pointer rounded-lg p-2 text-secondaire hover:bg-neutre-fond lg:hidden"><Menu size={20} /></button>
          <button type="button" onClick={basculer} aria-label={replie ? "Déplier le menu" : "Replier le menu"} title={replie ? "Déplier le menu" : "Replier le menu"}
            className="-ml-2 hidden cursor-pointer rounded-lg p-2 text-secondaire hover:bg-neutre-fond hover:text-texte lg:block">
            {replie ? <PanelLeftOpen size={18} /> : <PanelLeftClose size={18} />}
          </button>
          <h1 className="min-w-0 truncate text-xl font-bold">{titrePour(pathname)}</h1>
          <span className="ml-auto"><Notifications userId={user.id} /></span>
          <button type="button" onClick={() => setRecherche(true)} aria-label="Rechercher"
            className="flex h-10 cursor-pointer items-center gap-2 rounded-lg border border-bordure-forte bg-fond px-3 text-[13px] text-secondaire hover:border-marine hover:text-texte sm:w-60">
            <Search size={16} aria-hidden />
            <span className="flex-1 truncate text-left max-sm:hidden">Rechercher</span>
            <kbd className="rounded border border-bordure-forte bg-white px-1.5 py-0.5 font-sans text-[11px] font-semibold max-sm:hidden">Ctrl K</kbd>
          </button>
        </header>
        <main id="contenu" className="p-4 lg:p-8">
          {acces && acces.niveau === 0 ? (
            <div role="alert" className="rounded-xl border border-bordure bg-white px-6 py-14 text-center">
              <div className="font-display font-bold">Votre fonction n&apos;a pas accès au module « {acces.label} »</div>
              <div className="text-secondaire">Les accès se règlent dans le paramétrage, par le directeur ou le chef de projet.</div>
            </div>
          ) : (
            <>
              {acces && acces.niveau === 1 && !acces.consultation && (
                <p role="status" className="mb-4 rounded-lg bg-violet-fond px-4 py-2.5 text-[13px] text-violet-texte">
                  Lecture seule : votre fonction consulte ce module sans pouvoir y enregistrer.
                </p>
              )}
              {children}
            </>
          )}
        </main>
      </div>

      <Recherche open={recherche} onOpenChange={setRecherche} />
    </div>
  );
}

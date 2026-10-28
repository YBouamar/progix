"use client";

import { useEffect } from "react";
import Image from "next/image";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { LogOut } from "lucide-react";
import { useAuth } from "@/lib/auth";
import { menuPour, titrePour } from "@/lib/nav";
import { cn } from "@/lib/utils";
import { MonCompte } from "@/components/mon-compte";

export default function AppLayout({ children }: { children: React.ReactNode }) {
  const { user, loading, logout } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => { if (!loading && !user) router.replace("/login"); }, [loading, user, router]);

  if (loading || !user) {
    return <div className="flex min-h-screen items-center justify-center text-secondaire">Chargement...</div>;
  }

  const menu = menuPour(user.role);
  const actif = (href: string) => (href === "/" ? pathname === "/" : pathname.startsWith(href));

  return (
    <div className="flex min-h-screen">
      <aside className="sticky top-0 flex h-screen w-[248px] shrink-0 flex-col bg-marine text-white">
        {/* Icône de la marque et nom en blanc : le logo complet, en bleu marine, ne se lit pas sur le fond du menu. */}
        <Link href="/" className="flex items-center gap-3 px-5 py-5" aria-label="PROGIX, tableau de bord">
          <Image src="/marque/icone.png" alt="" width={40} height={40} priority className="h-10 w-10 shrink-0" />
          <span className="font-display text-xl font-bold tracking-wide">PROGIX</span>
        </Link>
        <nav className="flex-1 space-y-1 overflow-y-auto px-3">
          {menu.map((m) => (
            <Link key={m.id} href={m.href}
              className={cn("flex items-center gap-3 rounded-lg px-3 py-2.5 text-[14px] transition-colors",
                actif(m.href) ? "bg-white/15 font-bold text-white" : "font-medium text-[#dfe3f5] hover:bg-white/10")}>
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
                <path d={m.icon} />
              </svg>
              <span className="truncate">{m.label}</span>
            </Link>
          ))}
        </nav>
        <div className="flex items-center gap-1 border-t border-white/15 px-2 py-2">
          <MonCompte />
          <button onClick={() => logout()} title="Se déconnecter" aria-label="Se déconnecter"
            className="cursor-pointer rounded-lg p-2 text-white/70 hover:bg-white/10 hover:text-white">
            <LogOut size={16} />
          </button>
        </div>
      </aside>
      <div className="min-w-0 flex-1">
        <header className="sticky top-0 z-10 flex h-16 items-center border-b border-bordure bg-white px-8">
          <h1 className="text-xl font-bold">{titrePour(pathname)}</h1>
        </header>
        <main className="p-8">{children}</main>
      </div>
    </div>
  );
}

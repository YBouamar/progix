import type { Metadata, Viewport } from "next";
import { IBM_Plex_Sans, IBM_Plex_Sans_Condensed } from "next/font/google";
import "./globals.css";
import { AuthProvider } from "@/lib/auth";
import { HorsLigne } from "@/components/hors-ligne";

export const metadata: Metadata = {
  title: "PROGIX",
  description: "Du devis à la réception finale, dalle par dalle.",
  manifest: "/manifest.webmanifest",
  appleWebApp: { capable: true, title: "PROGIX", statusBarStyle: "default" },
};

// Polices servies par l'application elle-même (téléchargées à la construction, aucun appel externe à l'usage).
const policeTexte = IBM_Plex_Sans({ subsets: ["latin"], weight: ["400", "500", "600", "700"], variable: "--police-texte", display: "swap" });
const policeTitre = IBM_Plex_Sans_Condensed({ subsets: ["latin"], weight: ["500", "600", "700"], variable: "--police-titre", display: "swap" });

export const viewport: Viewport = { themeColor: "#1b2a6b" };

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="fr" className={`${policeTexte.variable} ${policeTitre.variable}`}>
      <body className="antialiased">
        <AuthProvider>{children}</AuthProvider>
        <HorsLigne />
      </body>
    </html>
  );
}

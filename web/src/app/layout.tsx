import type { Metadata, Viewport } from "next";
import "./globals.css";
import { AuthProvider } from "@/lib/auth";
import { HorsLigne } from "@/components/hors-ligne";

export const metadata: Metadata = {
  title: "PROGIX",
  description: "Du devis à la réception finale, dalle par dalle.",
  manifest: "/manifest.webmanifest",
  appleWebApp: { capable: true, title: "PROGIX", statusBarStyle: "default" },
};

export const viewport: Viewport = { themeColor: "#1b2a6b" };

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="fr">
      <body className="antialiased">
        <AuthProvider>{children}</AuthProvider>
        <HorsLigne />
      </body>
    </html>
  );
}

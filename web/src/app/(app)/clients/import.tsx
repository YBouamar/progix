"use client";

import { useRef, useState } from "react";
import { FileDown, Upload } from "lucide-react";
import { cn } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Sheet } from "@/components/ui/sheet";

type Ligne = { ligne: number; nom: string; type: string | null; categorie: string | null; statut: "a_creer" | "cree" | "doublon" | "erreur"; message: string };
type Resultat = { enregistre: boolean; valides: number; doublons: number; erreurs: number; avertissements: number; lignes: Ligne[] };

const STATUTS: Record<Ligne["statut"], [string, "vert" | "orange" | "rouge" | "neutre"]> = {
  a_creer: ["À créer", "vert"], cree: ["Créé", "vert"], doublon: ["Doublon, ignoré", "orange"], erreur: ["Erreur", "rouge"],
};
const CATEGORIES: Record<string, string> = { morale: "Personne morale", physique: "Personne physique", autre: "Autre" };

/** Import des clients et partenaires depuis Excel : aperçu ligne par ligne, puis création des lignes valides sur confirmation. */
export function ImportPartenaires({ onClose, onImporte }: { onClose: () => void; onImporte: () => void }) {
  const [fichier, setFichier] = useState<File | null>(null);
  const [r, setR] = useState<Resultat | null>(null);
  const [erreur, setErreur] = useState("");
  const [envoi, setEnvoi] = useState(false);
  const champ = useRef<HTMLInputElement>(null);

  const envoyer = async (f: File, enregistrer: boolean) => {
    setEnvoi(true); setErreur("");
    if (!enregistrer) setR(null);
    const corps = new FormData(); corps.append("fichier", f);
    try {
      const res = await fetch(`/api/partenaires/import?enregistrer=${enregistrer}`, { method: "POST", body: corps, credentials: "include" });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) throw new Error(res.status === 403 ? "Votre rôle ne permet pas d'importer des clients." : data.message ?? "Lecture du fichier impossible.");
      setR(data as Resultat);
      if (enregistrer) onImporte();
    } catch (e) { setErreur(e instanceof Error ? e.message : "Lecture du fichier impossible."); }
    finally { setEnvoi(false); }
  };

  return (
    <Sheet open onOpenChange={(o) => !o && onClose()} title="Importer des clients et partenaires"
      description="Une ligne par client ou partenaire, suivant la trame. Rien n'est enregistré avant votre confirmation.">
      <div className="flex flex-wrap items-center gap-2">
        <a href="/api/partenaires/import/trame.xlsx" className="inline-flex items-center gap-2 rounded-lg border border-bordure-forte bg-white px-3.5 py-2 text-[13px] font-semibold hover:bg-fond"><FileDown size={15} />Télécharger la trame Excel</a>
        <input ref={champ} type="file" accept=".xlsx" className="hidden" onChange={(e) => { const f = e.target.files?.[0]; if (f) { setFichier(f); envoyer(f, false); } e.target.value = ""; }} />
        <Button variant="outline" disabled={envoi} onClick={() => champ.current?.click()}><Upload size={15} />{fichier ? "Changer de fichier" : "Choisir un fichier"}</Button>
      </div>
      <p className="mt-2 text-[12px] text-attenue">
        L&apos;onglet « Aide » de la trame détaille chaque colonne. Une ligne dont l&apos;ICE ou la CIN existe déjà est ignorée ; un même nom ou un même téléphone est signalé.
      </p>
      {erreur && <div role="alert" className="mt-3 rounded-lg bg-rouge-fond px-4 py-3 text-[13px] text-rouge-texte">{erreur}</div>}
      {r && (
        <>
          <div role="status" className={cn("mt-4 rounded-lg px-4 py-3 text-[13px]", r.enregistre ? "bg-vert-fond text-vert-texte" : "bg-fond")}>
            {r.enregistre ? <strong>{r.valides} client(s) ou partenaire(s) créé(s).</strong> : <><span className="font-semibold">{fichier?.name}</span> · {r.lignes.length} ligne(s) lue(s) · <strong>{r.valides} à créer</strong></>}
            {r.doublons > 0 && ` · ${r.doublons} doublon(s) ignoré(s)`}
            {r.erreurs > 0 && ` · ${r.erreurs} ligne(s) en erreur`}
            {r.avertissements > 0 && ` · ${r.avertissements} doublon(s) possible(s) à vérifier`}
          </div>
          <div className="mt-3 max-h-[55vh] overflow-auto rounded-lg border border-bordure">
            <table className="w-full text-left text-[13px]">
              <thead className="sticky top-0 border-b border-bordure bg-fond text-[11px] uppercase tracking-wide text-secondaire">
                <tr><th className="px-3 py-2 font-semibold">Ligne</th><th className="px-3 py-2 font-semibold">Nom</th><th className="px-3 py-2 font-semibold">Type</th><th className="px-3 py-2 font-semibold">Contrôle</th></tr>
              </thead>
              <tbody>
                {r.lignes.map((l) => (
                  <tr key={l.ligne} className={cn("border-b border-bordure last:border-0", l.statut === "erreur" && "bg-rouge-fond/40")}>
                    <td className="px-3 py-2 font-display">{l.ligne}</td>
                    <td className="px-3 py-2"><div className="font-semibold">{l.nom || "?"}</div><div className="text-[12px] text-secondaire">{CATEGORIES[l.categorie ?? ""] ?? l.categorie}</div></td>
                    <td className="px-3 py-2 text-secondaire">{l.type}</td>
                    <td className="px-3 py-2">
                      <Badge ton={STATUTS[l.statut][1]}>{STATUTS[l.statut][0]}</Badge>
                      {l.message && <div className={cn("mt-1 text-[12px]", l.statut === "erreur" ? "text-rouge-texte" : "text-orange-texte")}>{l.message}</div>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {!r.enregistre && <p className="mt-2 text-[12px] text-attenue">Les lignes en erreur et les doublons ne sont pas importés : corrigez le fichier puis rechargez-le, ou saisissez-les à la main.</p>}
        </>
      )}
      <div className="mt-4 flex justify-end gap-2">
        <Button variant="outline" onClick={onClose}>{r?.enregistre ? "Fermer" : "Annuler"}</Button>
        {!r?.enregistre && (
          <Button disabled={envoi || !fichier || !r || r.valides === 0} onClick={() => fichier && envoyer(fichier, true)}>
            {envoi && r ? "Import en cours..." : `Créer les ${r?.valides ?? 0} client(s) et partenaire(s)`}
          </Button>
        )}
      </div>
    </Sheet>
  );
}

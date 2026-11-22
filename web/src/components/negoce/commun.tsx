"use client";

import { useEffect, useState } from "react";
import { Plus, Trash2 } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { ENTITES, formatQuantite, type Article, type LigneEdition, nouvelleLigne, filtrerDecimal, lireNombre } from "@/lib/negoce";
import { formatDH } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Input, Select } from "@/components/ui/input";

/** Message d'une erreur d'appel, prêt à afficher. */
export const messageErreur = (e: unknown, defaut = "L'opération a échoué.") => (e instanceof ApiError ? e.message : defaut);

export function Alerte({ message }: { message: string }) {
  return message ? <div role="alert" className="mb-4 rounded-lg bg-rouge-fond px-4 py-3 text-rouge-texte">{message}</div> : null;
}

/** Articles actifs du catalogue, pour choisir une ligne. */
export function useArticles() {
  const [articles, setArticles] = useState<Article[]>([]);
  useEffect(() => { api<Article[]>("/negoce/articles?actif=true").then(setArticles).catch(() => setArticles([])); }, []);
  return articles;
}

export function EntiteSelect({ valeur, onChange, disabled }: { valeur: string; onChange: (v: string) => void; disabled?: boolean }) {
  return (
    <Select value={valeur} disabled={disabled} onChange={(e) => onChange(e.target.value)}>
      {Object.entries(ENTITES).map(([code, label]) => <option key={code} value={code}>{label}</option>)}
    </Select>
  );
}

export function Totaux({ ht, tva, ttc }: { ht: number; tva: number; ttc: number }) {
  return (
    <dl className="ml-auto w-full max-w-xs space-y-1 text-[14px]">
      <div className="flex justify-between"><dt className="text-secondaire">Total HT</dt><dd className="font-display">{formatDH(ht)}</dd></div>
      <div className="flex justify-between"><dt className="text-secondaire">TVA</dt><dd className="font-display">{formatDH(tva)}</dd></div>
      <div className="flex justify-between border-t border-bordure pt-1 font-bold"><dt>Total TTC</dt><dd className="font-display">{formatDH(ttc)}</dd></div>
    </dl>
  );
}

/** Calcule un total de lignes saisies (hors taxe), avec le prix par défaut quand aucun prix n'est saisi. */
export function totalHT(lignes: LigneEdition[], prixPar: (articleId: string) => number | undefined) {
  return lignes.reduce((s, l) => s + Math.round(lireNombre(l.quantite) * (l.prix.trim() ? lireNombre(l.prix) : prixPar(l.articleId) ?? 0) * 100) / 100, 0);
}

/**
 * Lignes d'un devis, d'une commande ou d'une commande d'achat : article, quantité, prix unitaire.
 * `prixPar` donne le prix proposé quand le champ prix reste vide (prix du groupe du client, prix d'achat du fournisseur).
 */
export function LignesEditeur({ lignes, onChange, articles, prixPar, filtre, libellePrix = "Prix unitaire HT" }: {
  lignes: LigneEdition[]; onChange: (l: LigneEdition[]) => void; articles: Article[]; prixPar: (articleId: string) => number | undefined;
  filtre?: (a: Article) => boolean; libellePrix?: string;
}) {
  const liste = filtre ? articles.filter(filtre) : articles;
  const parCategorie = liste.reduce<Record<string, Article[]>>((m, a) => { (m[a.categorie ?? "Sans catégorie"] ??= []).push(a); return m; }, {});
  const maj = (cle: string, p: Partial<LigneEdition>) => onChange(lignes.map((l) => (l.cle === cle ? { ...l, ...p } : l)));
  return (
    <div className="space-y-2">
      <div className="hidden grid-cols-[1fr_90px_130px_110px_36px] gap-2 text-[12px] font-semibold text-secondaire sm:grid">
        <span>Article</span><span>Quantité</span><span>{libellePrix}</span><span className="text-right">Montant HT</span><span />
      </div>
      {lignes.map((l) => {
        const propose = prixPar(l.articleId);
        const prix = l.prix.trim() ? lireNombre(l.prix) : propose ?? 0;
        const a = articles.find((x) => x.id === l.articleId);
        return (
          <div key={l.cle} className="grid items-center gap-2 sm:grid-cols-[1fr_90px_130px_110px_36px]">
            <Select aria-label="Article" value={l.articleId} onChange={(e) => maj(l.cle, { articleId: e.target.value, prix: "" })}>
              <option value="">Choisir un article</option>
              {Object.entries(parCategorie).map(([cat, arts]) => (
                <optgroup key={cat} label={cat}>{arts.map((x) => <option key={x.id} value={x.id}>{x.code} - {x.designation}</option>)}</optgroup>
              ))}
            </Select>
            <Input aria-label="Quantité" inputMode="decimal" value={l.quantite} onChange={(e) => maj(l.cle, { quantite: filtrerDecimal(e.target.value) })} />
            <Input aria-label={libellePrix} inputMode="decimal" value={l.prix} placeholder={propose !== undefined ? formatQuantite(propose) : ""}
              onChange={(e) => maj(l.cle, { prix: filtrerDecimal(e.target.value) })} />
            <div className="text-right font-display text-[14px]">
              {a ? formatDH(Math.round(lireNombre(l.quantite) * prix * 100) / 100) : ""}
              {a && <span className="ml-1 text-[12px] text-attenue">{a.uniteSymbole}</span>}
            </div>
            <Button type="button" variant="ghost" size="icon" aria-label="Retirer la ligne" disabled={lignes.length === 1} onClick={() => onChange(lignes.filter((x) => x.cle !== l.cle))}><Trash2 size={16} /></Button>
          </div>
        );
      })}
      <Button type="button" variant="outline" size="sm" onClick={() => onChange([...lignes, nouvelleLigne()])}><Plus size={14} />Ajouter une ligne</Button>
    </div>
  );
}

/** Lignes prêtes à envoyer : les lignes vides sont écartées, le prix vide laisse le serveur calculer. */
export const lignesEnvoi = (lignes: LigneEdition[]) =>
  lignes.filter((l) => l.articleId).map((l) => ({ articleId: l.articleId, quantite: lireNombre(l.quantite), prixUnitaire: l.prix.trim() ? lireNombre(l.prix) : null }));

/** En-tête de colonne et cellules de tableau : mêmes classes que les autres listes de l'application. */
export const TH = "px-5 py-3 font-semibold";
export const TD = "px-5 py-3";
export const TABLE_BOITE = "overflow-x-auto rounded-xl border border-bordure bg-white";
export const THEAD = "border-b border-bordure bg-fond text-[12px] text-secondaire";
export const LIGNE_CLIQUABLE = "cursor-pointer border-b border-bordure last:border-0 hover:bg-fond";

export function Vide({ texte }: { texte: string }) {
  return <div className="rounded-xl border border-dashed border-bordure-forte bg-white px-5 py-10 text-center text-secondaire">{texte}</div>;
}

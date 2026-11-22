"use client";

import { useCallback, useEffect, useState } from "react";
import { Plus, Search } from "lucide-react";
import { api } from "@/lib/api";
import { peutEcrire, useAuth } from "@/lib/auth";
import { TYPES_MOUVEMENT, formatQuantite } from "@/lib/negoce";
import { filtrerDecimal, formatDate, formatDH, lireNombre } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field, Input, Select } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";
import { Alerte, TABLE_BOITE, TD, TH, THEAD, Vide, messageErreur } from "@/components/negoce/commun";

type LigneStock = { id: string; code: string; designation: string; categorie: string | null; unite: string | null; origine: string; stock: number; seuilAlerte: number; sousSeuil: boolean; kgParMetre: number | null; coutMoyen: number | null; valeur: number | null };
type Resume = { articles: LigneStock[]; sousSeuil: number; valeurTotale: number | null };
type Mouvement = { id: string; code: string; designation: string; type: string; quantite: number; coutUnitaire: number | null; date: string; motif: string | null; piece: string | null; par: string };

export default function StockPage() {
  const { user } = useAuth();
  const ecrit = peutEcrire(user, "negoce_stock");
  const [vue, setVue] = useState<"stock" | "mouvements">("stock");
  const [r, setR] = useState<Resume | null>(null);
  const [mouvements, setMouvements] = useState<Mouvement[] | null>(null);
  const [recherche, setRecherche] = useState("");
  const [sousSeuil, setSousSeuil] = useState(false);
  const [articleFiltre, setArticleFiltre] = useState<string>("");
  const [erreur, setErreur] = useState("");
  const [saisie, setSaisie] = useState<LigneStock | "nouveau" | null>(null);

  const charger = useCallback(() => {
    const p = new URLSearchParams();
    if (recherche.trim()) p.set("recherche", recherche.trim());
    if (sousSeuil) p.set("sousSeuil", "true");
    api<Resume>(`/negoce/stock?${p}`).then((x) => { setR(x); setErreur(""); }).catch((e) => setErreur(messageErreur(e, "Chargement impossible.")));
    const m = new URLSearchParams();
    if (articleFiltre) m.set("articleId", articleFiltre);
    api<Mouvement[]>(`/negoce/mouvements?${m}`).then(setMouvements).catch(() => undefined);
  }, [recherche, sousSeuil, articleFiltre]);
  useEffect(() => { const t = setTimeout(charger, 250); return () => clearTimeout(t); }, [charger]);

  const voitValeur = r?.valeurTotale !== null && r?.valeurTotale !== undefined;

  return (
    <div>
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <div className="flex gap-1.5" role="group" aria-label="Vue">
          {(["stock", "mouvements"] as const).map((v) => (
            <Button key={v} size="sm" variant={vue === v ? "default" : "outline"} onClick={() => setVue(v)}>{v === "stock" ? "État du stock" : "Mouvements"}</Button>
          ))}
        </div>
        {vue === "stock" && (
          <>
            <div className="relative w-full max-w-xs">
              <Search size={16} className="pointer-events-none absolute left-3 top-3 text-attenue" />
              <Input className="pl-9" placeholder="Code ou désignation" value={recherche} onChange={(e) => setRecherche(e.target.value)} />
            </div>
            <label className="flex items-center gap-2 text-[14px]"><input type="checkbox" checked={sousSeuil} onChange={(e) => setSousSeuil(e.target.checked)} />Sous le seuil d&apos;alerte{r && r.sousSeuil > 0 ? ` (${r.sousSeuil})` : ""}</label>
          </>
        )}
        {vue === "mouvements" && articleFiltre && <Button size="sm" variant="ghost" onClick={() => setArticleFiltre("")}>Tous les articles</Button>}
        {ecrit && <Button className="ml-auto" onClick={() => setSaisie("nouveau")}><Plus size={16} />Nouveau mouvement</Button>}
      </div>
      <Alerte message={erreur} />

      {vue === "stock" && (
        <>
          {voitValeur && <p className="mb-3 text-[14px] text-secondaire">Valeur du stock au coût moyen pondéré : <span className="font-display font-bold text-marine">{formatDH(r!.valeurTotale!)}</span></p>}
          {r && r.articles.length === 0 ? <Vide texte="Aucun article en stock." /> : (
            <div className={TABLE_BOITE}>
              <table className="w-full text-left">
                <thead className={THEAD}><tr><th className={TH}>Article</th><th className={`${TH} text-right`}>Stock</th><th className={`${TH} text-right`}>Seuil</th>{voitValeur && <><th className={`${TH} text-right`}>Coût moyen</th><th className={`${TH} text-right`}>Valeur</th></>}<th className={TH}></th></tr></thead>
                <tbody>
                  {r?.articles.map((a) => (
                    <tr key={a.id} className="border-b border-bordure last:border-0">
                      <td className={TD}><div className="font-semibold">{a.designation}</div><div className="font-display text-[12px] text-attenue">{a.code}{a.categorie ? ` · ${a.categorie}` : ""}</div></td>
                      <td className={`${TD} text-right font-display font-bold ${a.sousSeuil ? "text-rouge-texte" : ""}`}>{formatQuantite(a.stock)} {a.unite}</td>
                      <td className={`${TD} text-right font-display text-secondaire`}>{a.seuilAlerte > 0 ? formatQuantite(a.seuilAlerte) : "-"}</td>
                      {voitValeur && <><td className={`${TD} text-right font-display`}>{formatDH(a.coutMoyen ?? 0)}</td><td className={`${TD} text-right font-display`}>{formatDH(a.valeur ?? 0)}</td></>}
                      <td className={`${TD} text-right`}>
                        {a.sousSeuil && <Badge ton="rouge">Sous le seuil</Badge>}{" "}
                        <Button variant="ghost" size="sm" onClick={() => { setArticleFiltre(a.id); setVue("mouvements"); }}>Historique</Button>
                        {ecrit && <Button variant="ghost" size="sm" onClick={() => setSaisie(a)}>Mouvement</Button>}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}

      {vue === "mouvements" && (
        mouvements && mouvements.length === 0 ? <Vide texte="Aucun mouvement de stock." /> : (
          <div className={TABLE_BOITE}>
            <table className="w-full text-left">
              <thead className={THEAD}><tr><th className={TH}>Date</th><th className={TH}>Article</th><th className={TH}>Type</th><th className={`${TH} text-right`}>Quantité</th><th className={TH}>Origine</th></tr></thead>
              <tbody>
                {mouvements?.map((m) => (
                  <tr key={m.id} className="border-b border-bordure last:border-0">
                    <td className={TD}>{formatDate(m.date)}</td>
                    <td className={TD}><div className="font-semibold">{m.designation}</div><div className="font-display text-[12px] text-attenue">{m.code}</div></td>
                    <td className={TD}>{TYPES_MOUVEMENT[m.type] ?? m.type}</td>
                    <td className={`${TD} text-right font-display font-bold ${m.quantite < 0 ? "text-rouge-texte" : "text-vert-texte"}`}>{m.quantite > 0 ? "+" : ""}{formatQuantite(m.quantite)}</td>
                    <td className={TD}><div>{m.piece ?? m.motif ?? "-"}</div><div className="text-[12px] text-attenue">{m.par}{m.piece && m.motif ? ` · ${m.motif}` : ""}</div></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )
      )}

      {saisie && <FormMouvement depart={saisie === "nouveau" ? null : saisie} articles={r?.articles ?? []} onFermer={() => setSaisie(null)} onFait={() => { setSaisie(null); charger(); }} />}
    </div>
  );
}

function FormMouvement({ depart, articles, onFermer, onFait }: { depart: LigneStock | null; articles: LigneStock[]; onFermer: () => void; onFait: () => void }) {
  const { user } = useAuth();
  const modifieCout = peutEcrire(user, "negoce_prix_achat");
  const [articleId, setArticleId] = useState(depart?.id ?? "");
  const [type, setType] = useState("entree");
  const [quantite, setQuantite] = useState("");
  const [enMetres, setEnMetres] = useState(false);
  const [cout, setCout] = useState("");
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10));
  const [motif, setMotif] = useState("");
  const [erreur, setErreur] = useState("");
  const article = articles.find((a) => a.id === articleId);
  const peutMetres = !!article?.kgParMetre;
  const types = Object.entries(TYPES_MOUVEMENT).filter(([k]) => k !== "production" || article?.origine === "produit");
  const motifObligatoire = type === "inventaire" || type === "correction" || type === "sortie";
  const libelleQuantite = type === "inventaire" ? "Quantité comptée" : type === "correction" ? "Écart (négatif pour retirer)" : "Quantité";

  const valider = async () => {
    setErreur("");
    try {
      const n = lireNombre(quantite);
      const corps: Record<string, unknown> = { articleId, type, date, motif };
      if (enMetres && peutMetres) corps.metres = n; else corps.quantite = n;
      if (modifieCout && cout && (type === "entree" || type === "production")) corps.coutUnitaire = lireNombre(cout);
      await api("/negoce/mouvements", { method: "POST", body: JSON.stringify(corps) });
      onFait();
    } catch (e) { setErreur(messageErreur(e)); }
  };

  return (
    <Modal open onOpenChange={(o) => !o && onFermer()} title="Mouvement de stock" description="Un mouvement enregistré ne se modifie pas : corrigez-le par un autre mouvement."
      footer={<><Button variant="outline" onClick={onFermer}>Annuler</Button><Button disabled={!articleId || !quantite || (motifObligatoire && !motif.trim())} onClick={valider}>Enregistrer</Button></>}>
      <Alerte message={erreur} />
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Article" required className="sm:col-span-2">
          <Select value={articleId} onChange={(e) => { setArticleId(e.target.value); setEnMetres(false); }}>
            <option value="">Choisir un article</option>{articles.map((a) => <option key={a.id} value={a.id}>{a.code} · {a.designation}</option>)}
          </Select>
        </Field>
        <div className="sm:col-span-2">
          <div className="mb-1.5 text-[13px] font-semibold">Type</div>
          <div className="flex flex-wrap gap-1.5" role="group" aria-label="Type de mouvement">
            {types.map(([k, v]) => <Button key={k} type="button" size="sm" variant={type === k ? "default" : "outline"} onClick={() => setType(k)}>{v}</Button>)}
          </div>
        </div>
        <Field label={enMetres ? "Longueur en mètres" : `${libelleQuantite}${article?.unite ? ` (${article.unite})` : ""}`} required>
          <Input inputMode="decimal" value={quantite} onChange={(e) => setQuantite(filtrerDecimal(e.target.value, type === "correction"))} />
        </Field>
        <Field label="Date"><Input type="date" value={date} onChange={(e) => setDate(e.target.value)} /></Field>
        {peutMetres && <label className="flex items-center gap-2 text-[14px] sm:col-span-2"><input type="checkbox" checked={enMetres} onChange={(e) => setEnMetres(e.target.checked)} />Saisir en mètres (converti en tonnes à {String(article!.kgParMetre).replace(".", ",")} kg le mètre)</label>}
        {modifieCout && (type === "entree" || type === "production") && (
          <Field label="Coût de revient unitaire (facultatif)" className="sm:col-span-2">
            <Input inputMode="decimal" value={cout} onChange={(e) => setCout(filtrerDecimal(e.target.value))} />
            <p className="mt-1 text-[12px] text-attenue">Sans coût, l&apos;entrée se fait au coût moyen en cours. Avec un coût, le coût moyen pondéré est recalculé.</p>
          </Field>
        )}
        <Field label={motifObligatoire ? "Motif" : "Motif (facultatif)"} required={motifObligatoire} className="sm:col-span-2"><Input value={motif} onChange={(e) => setMotif(e.target.value)} /></Field>
      </div>
    </Modal>
  );
}

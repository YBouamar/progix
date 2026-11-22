"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { Plus, Search, Settings2, Trash2 } from "lucide-react";
import { api } from "@/lib/api";
import { peutEcrire, useAuth } from "@/lib/auth";
import { type Article, ORIGINES, type Option, filtrerDecimal, formatQuantite, lireNombre } from "@/lib/negoce";
import { formatDH } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field, Input, Select } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";
import { Sheet } from "@/components/ui/sheet";
import { Alerte, LIGNE_CLIQUABLE, TABLE_BOITE, TD, TH, THEAD, Vide, messageErreur } from "@/components/negoce/commun";

type Detail = { article: Article; prixFournisseurs: { id: string; fournisseurId: string; fournisseur: string; prixAchat: number }[] | null };
type Fournisseur = { id: string; nom: string };
type Form = { code: string; designation: string; categorieId: string; uniteId: string; origine: string; prixVente: string; coutMoyen: string; seuilAlerte: string; kgParMetre: string; actif: boolean };

const VIDE: Form = { code: "", designation: "", categorieId: "", uniteId: "", origine: "achete", prixVente: "", coutMoyen: "", seuilAlerte: "", kgParMetre: "", actif: true };
const texte = (n: number | null | undefined) => (n === null || n === undefined ? "" : String(n).replace(".", ","));

export default function ArticlesPage() {
  const { user } = useAuth();
  const ecrit = peutEcrire(user, "negoce_articles");
  const fixePrixVente = peutEcrire(user, "negoce_groupes");
  const fixeCout = peutEcrire(user, "negoce_prix_achat");
  const [articles, setArticles] = useState<Article[] | null>(null);
  const [categories, setCategories] = useState<Option[]>([]);
  const [unites, setUnites] = useState<Option[]>([]);
  const [recherche, setRecherche] = useState("");
  const [categorie, setCategorie] = useState("");
  const [origine, setOrigine] = useState("");
  const [sousSeuil, setSousSeuil] = useState(false);
  const [erreur, setErreur] = useState("");
  const [ouvert, setOuvert] = useState<Detail | "nouveau" | null>(null);
  const [reglages, setReglages] = useState(false);

  const charger = useCallback(() => {
    api<Article[]>("/negoce/articles").then((r) => { setArticles(r); setErreur(""); }).catch((e) => setErreur(messageErreur(e, "Chargement impossible.")));
    api<Option[]>("/negoce/categories").then(setCategories).catch(() => undefined);
    api<Option[]>("/negoce/unites").then(setUnites).catch(() => undefined);
  }, []);
  useEffect(() => { charger(); }, [charger]);

  const voitCouts = articles?.some((a) => a.coutMoyen !== null) ?? false;
  const liste = useMemo(() => (articles ?? []).filter((a) =>
    (!categorie || a.categorieId === categorie) && (!origine || a.origine === origine) && (!sousSeuil || a.sousSeuil)
    && (!recherche.trim() || `${a.code} ${a.designation}`.toLowerCase().includes(recherche.trim().toLowerCase()))), [articles, categorie, origine, sousSeuil, recherche]);

  const ouvrir = (a: Article) => api<Detail>(`/negoce/articles/${a.id}`).then(setOuvert).catch((e) => setErreur(messageErreur(e)));

  return (
    <div>
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <div className="relative w-full max-w-xs">
          <Search size={16} className="pointer-events-none absolute left-3 top-3 text-attenue" />
          <Input className="pl-9" placeholder="Code ou désignation" value={recherche} onChange={(e) => setRecherche(e.target.value)} />
        </div>
        <Select className="w-auto" aria-label="Catégorie" value={categorie} onChange={(e) => setCategorie(e.target.value)}>
          <option value="">Toutes les catégories</option>{categories.map((c) => <option key={c.id} value={c.id}>{c.libelle}</option>)}
        </Select>
        <Select className="w-auto" aria-label="Origine" value={origine} onChange={(e) => setOrigine(e.target.value)}>
          <option value="">Acheté ou produit</option>{Object.entries(ORIGINES).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
        </Select>
        <label className="flex items-center gap-2 text-[13px] text-secondaire"><input type="checkbox" checked={sousSeuil} onChange={(e) => setSousSeuil(e.target.checked)} />Sous le seuil d&apos;alerte</label>
        {ecrit && (
          <div className="ml-auto flex gap-2">
            <Button variant="outline" onClick={() => setReglages(true)}><Settings2 size={16} />Catégories et unités</Button>
            <Button onClick={() => setOuvert("nouveau")}><Plus size={16} />Nouvel article</Button>
          </div>
        )}
      </div>
      <Alerte message={erreur} />
      {articles && liste.length === 0 ? <Vide texte="Aucun article ne correspond." /> : (
        <div className={TABLE_BOITE}>
          <table className="w-full text-left">
            <thead className={THEAD}>
              <tr>
                <th className={TH}>Article</th><th className={TH}>Catégorie</th><th className={TH}>Origine</th>
                <th className={`${TH} text-right`}>Prix de vente HT</th>{voitCouts && <th className={`${TH} text-right`}>Coût moyen</th>}
                <th className={`${TH} text-right`}>Stock</th>
              </tr>
            </thead>
            <tbody>
              {liste.map((a) => (
                <tr key={a.id} onClick={() => ouvrir(a)} className={LIGNE_CLIQUABLE}>
                  <td className={TD}><div className="font-semibold">{a.designation}</div><div className="font-display text-[12px] text-attenue">{a.code}{!a.actif && " · désactivé"}</div></td>
                  <td className={`${TD} text-secondaire`}>{a.categorie}</td>
                  <td className={TD}><Badge ton={a.origine === "produit" ? "violet" : "neutre"}>{ORIGINES[a.origine]}</Badge></td>
                  <td className={`${TD} text-right font-display`}>{a.prixVente > 0 ? formatDH(a.prixVente) : <span className="text-attenue">À fixer</span>}</td>
                  {voitCouts && <td className={`${TD} text-right font-display`}>{a.coutMoyen ? formatDH(a.coutMoyen) : <span className="text-attenue">-</span>}</td>}
                  <td className={`${TD} text-right font-display`}>{formatQuantite(a.stock)} <span className="text-[12px] text-attenue">{a.uniteSymbole}</span>{a.sousSeuil && <Badge ton="rouge" className="ml-2">Sous le seuil</Badge>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {ouvert && (
        <FicheArticle detail={ouvert === "nouveau" ? null : ouvert} categories={categories} unites={unites} ecrit={ecrit} fixePrixVente={fixePrixVente} fixeCout={fixeCout}
          onFermer={() => setOuvert(null)} onChange={() => { charger(); }} />
      )}
      {reglages && <Reglages categories={categories} unites={unites} onFermer={() => setReglages(false)} onChange={charger} />}
    </div>
  );
}

function FicheArticle({ detail, categories, unites, ecrit, fixePrixVente, fixeCout, onFermer, onChange }: {
  detail: Detail | null; categories: Option[]; unites: Option[]; ecrit: boolean; fixePrixVente: boolean; fixeCout: boolean; onFermer: () => void; onChange: () => void;
}) {
  const a = detail?.article ?? null;
  const [f, setF] = useState<Form>(a ? { code: a.code, designation: a.designation, categorieId: a.categorieId, uniteId: a.uniteId, origine: a.origine, prixVente: texte(a.prixVente), coutMoyen: texte(a.coutMoyen), seuilAlerte: texte(a.seuilAlerte), kgParMetre: texte(a.kgParMetre), actif: a.actif }
    : { ...VIDE, categorieId: categories.find((c) => c.actif)?.id ?? "", uniteId: unites.find((u) => u.actif)?.id ?? "" });
  const [prix, setPrix] = useState(detail?.prixFournisseurs ?? null);
  const [fournisseurs, setFournisseurs] = useState<Fournisseur[]>([]);
  const [nouveau, setNouveau] = useState({ fournisseurId: "", prixAchat: "" });
  const [erreur, setErreur] = useState("");
  const [enCours, setEnCours] = useState(false);
  const maj = (p: Partial<Form>) => setF((x) => ({ ...x, ...p }));
  const unite = unites.find((u) => u.id === f.uniteId);

  useEffect(() => { if (prix !== null && fixeCout) api<Fournisseur[]>("/negoce/fournisseurs?actif=true").then(setFournisseurs).catch(() => undefined); }, [prix, fixeCout]);

  const enregistrer = async () => {
    setErreur(""); setEnCours(true);
    try {
      const corps = {
        code: f.code, designation: f.designation, categorieId: f.categorieId, uniteId: f.uniteId, origine: f.origine, actif: f.actif,
        prixVente: f.prixVente.trim() ? lireNombre(f.prixVente) : null, coutMoyen: f.origine === "produit" && f.coutMoyen.trim() ? lireNombre(f.coutMoyen) : null,
        seuilAlerte: f.seuilAlerte.trim() ? lireNombre(f.seuilAlerte) : 0, kgParMetre: f.kgParMetre.trim() ? lireNombre(f.kgParMetre) : null,
      };
      if (a) await api(`/negoce/articles/${a.id}`, { method: "PUT", body: JSON.stringify(corps) });
      else await api("/negoce/articles", { method: "POST", body: JSON.stringify(corps) });
      onChange(); onFermer();
    } catch (e) { setErreur(messageErreur(e)); } finally { setEnCours(false); }
  };

  const rafraichirPrix = async () => { if (a) setPrix((await api<Detail>(`/negoce/articles/${a.id}`)).prixFournisseurs); };
  const ajouterPrix = async () => {
    if (!a) return;
    setErreur("");
    try {
      await api("/negoce/prix-achat", { method: "PUT", body: JSON.stringify({ articleId: a.id, fournisseurId: nouveau.fournisseurId, prixAchat: lireNombre(nouveau.prixAchat) }) });
      setNouveau({ fournisseurId: "", prixAchat: "" }); await rafraichirPrix();
    } catch (e) { setErreur(messageErreur(e)); }
  };
  const retirerPrix = async (id: string) => { try { await api(`/negoce/prix-achat/${id}`, { method: "DELETE" }); await rafraichirPrix(); } catch (e) { setErreur(messageErreur(e)); } };

  return (
    <Sheet open onOpenChange={(o) => !o && onFermer()} title={a ? a.designation : "Nouvel article"} description={a ? a.code : "Catalogue des ventes directes"}
      footer={ecrit && <><Button variant="outline" onClick={onFermer}>Annuler</Button><Button onClick={enregistrer} disabled={enCours}>Enregistrer</Button></>}>
      <Alerte message={erreur} />
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Code" required><Input value={f.code} disabled={!ecrit} onChange={(e) => maj({ code: e.target.value })} /></Field>
        <Field label="Désignation" required><Input value={f.designation} disabled={!ecrit} onChange={(e) => maj({ designation: e.target.value })} /></Field>
        <Field label="Catégorie" required>
          <Select value={f.categorieId} disabled={!ecrit} onChange={(e) => maj({ categorieId: e.target.value })}>{categories.filter((c) => c.actif || c.id === f.categorieId).map((c) => <option key={c.id} value={c.id}>{c.libelle}</option>)}</Select>
        </Field>
        <Field label="Unité" required>
          <Select value={f.uniteId} disabled={!ecrit} onChange={(e) => maj({ uniteId: e.target.value })}>{unites.filter((u) => u.actif || u.id === f.uniteId).map((u) => <option key={u.id} value={u.id}>{u.libelle}</option>)}</Select>
        </Field>
        <Field label="Origine">
          <Select value={f.origine} disabled={!ecrit} onChange={(e) => maj({ origine: e.target.value })}>{Object.entries(ORIGINES).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</Select>
        </Field>
        <Field label="Prix de vente HT (DH)">
          <Input inputMode="decimal" value={f.prixVente} disabled={!ecrit || !fixePrixVente} onChange={(e) => maj({ prixVente: filtrerDecimal(e.target.value) })} />
          {!fixePrixVente && <span className="mt-1 block text-[12px] text-attenue">Le prix de vente est fixé par les ventes directes.</span>}
        </Field>
        {f.origine === "produit" ? (
          <Field label="Coût moyen pondéré (DH)">
            <Input inputMode="decimal" value={f.coutMoyen} disabled={!ecrit || !fixeCout} onChange={(e) => maj({ coutMoyen: filtrerDecimal(e.target.value) })} />
            <span className="mt-1 block text-[12px] text-attenue">Article produit : pas de prix d&apos;achat. Le stock y entre par une entrée « Production ».</span>
          </Field>
        ) : a?.coutMoyen !== null && a?.coutMoyen !== undefined ? (
          <Field label="Coût moyen pondéré"><div className="flex h-10 items-center font-display">{formatDH(a.coutMoyen)}</div></Field>
        ) : null}
        <Field label="Seuil d'alerte"><Input inputMode="decimal" value={f.seuilAlerte} disabled={!ecrit} onChange={(e) => maj({ seuilAlerte: filtrerDecimal(e.target.value) })} /></Field>
        {unite?.libelle === "Tonne" && (
          <Field label="Poids d'un mètre (kg)"><Input inputMode="decimal" value={f.kgParMetre} disabled={!ecrit} placeholder="1,18 pour le toron T15,7" onChange={(e) => maj({ kgParMetre: filtrerDecimal(e.target.value) })} /></Field>
        )}
        {a && <label className="flex items-center gap-2 text-[14px] sm:col-span-2"><input type="checkbox" checked={f.actif} disabled={!ecrit} onChange={(e) => maj({ actif: e.target.checked })} />Article actif (proposé dans les devis, commandes et mouvements)</label>}
      </div>

      {a && prix !== null && f.origine === "achete" && (
        <section className="mt-8">
          <h3 className="mb-2 font-display text-[15px] font-bold">Prix d&apos;achat par fournisseur</h3>
          {prix.length === 0 ? <p className="text-secondaire">Aucun prix d&apos;achat enregistré.</p> : (
            <ul className="divide-y divide-bordure rounded-lg border border-bordure">
              {prix.map((p) => (
                <li key={p.id} className="flex items-center justify-between px-4 py-2.5">
                  <span>{p.fournisseur}</span>
                  <span className="flex items-center gap-3"><span className="font-display font-semibold">{formatDH(p.prixAchat)}</span>
                    {fixeCout && <Button variant="ghost" size="icon" aria-label={`Retirer le prix de ${p.fournisseur}`} onClick={() => retirerPrix(p.id)}><Trash2 size={15} /></Button>}</span>
                </li>
              ))}
            </ul>
          )}
          {fixeCout && (
            <div className="mt-3 grid gap-2 sm:grid-cols-[1fr_140px_auto]">
              <Select aria-label="Fournisseur" value={nouveau.fournisseurId} onChange={(e) => setNouveau({ ...nouveau, fournisseurId: e.target.value })}>
                <option value="">Fournisseur</option>{fournisseurs.map((x) => <option key={x.id} value={x.id}>{x.nom}</option>)}
              </Select>
              <Input aria-label="Prix d'achat HT" inputMode="decimal" placeholder="Prix HT" value={nouveau.prixAchat} onChange={(e) => setNouveau({ ...nouveau, prixAchat: filtrerDecimal(e.target.value) })} />
              <Button variant="outline" disabled={!nouveau.fournisseurId || !nouveau.prixAchat} onClick={ajouterPrix}>Enregistrer le prix</Button>
            </div>
          )}
        </section>
      )}
    </Sheet>
  );
}

/** Catégories et unités : ajout, renommage, désactivation. */
function Reglages({ categories, unites, onFermer, onChange }: { categories: Option[]; unites: Option[]; onFermer: () => void; onChange: () => void }) {
  const [categorie, setCategorie] = useState("");
  const [unite, setUnite] = useState({ libelle: "", symbole: "" });
  const [erreur, setErreur] = useState("");
  const appel = async (chemin: string, methode: string, corps: object) => {
    setErreur("");
    try { await api(chemin, { method: methode, body: JSON.stringify(corps) }); onChange(); } catch (e) { setErreur(messageErreur(e)); }
  };
  return (
    <Modal open onOpenChange={(o) => !o && onFermer()} large title="Catégories et unités" description="Les catégories et les unités se désactivent, elles ne se suppriment pas." footer={<Button onClick={onFermer}>Fermer</Button>}>
      <Alerte message={erreur} />
      <div className="grid gap-6 sm:grid-cols-2">
        <section>
          <h3 className="mb-2 font-display text-[15px] font-bold">Catégories</h3>
          <ul className="mb-3 divide-y divide-bordure rounded-lg border border-bordure">
            {categories.map((c) => (
              <li key={c.id} className="flex items-center justify-between px-3 py-2">
                <span className={c.actif ? "" : "text-attenue line-through"}>{c.libelle}</span>
                <Button variant="ghost" size="sm" onClick={() => appel(`/negoce/categories/${c.id}`, "PUT", { actif: !c.actif })}>{c.actif ? "Désactiver" : "Réactiver"}</Button>
              </li>
            ))}
          </ul>
          <div className="flex gap-2">
            <Input aria-label="Nouvelle catégorie" placeholder="Nouvelle catégorie" value={categorie} onChange={(e) => setCategorie(e.target.value)} />
            <Button variant="outline" disabled={!categorie.trim()} onClick={() => { appel("/negoce/categories", "POST", { libelle: categorie }); setCategorie(""); }}>Ajouter</Button>
          </div>
        </section>
        <section>
          <h3 className="mb-2 font-display text-[15px] font-bold">Unités</h3>
          <ul className="mb-3 divide-y divide-bordure rounded-lg border border-bordure">
            {unites.map((u) => (
              <li key={u.id} className="flex items-center justify-between px-3 py-2">
                <span className={u.actif ? "" : "text-attenue line-through"}>{u.libelle} <span className="text-[12px] text-attenue">({u.symbole})</span></span>
                <Button variant="ghost" size="sm" onClick={() => appel(`/negoce/unites/${u.id}`, "PUT", { actif: !u.actif })}>{u.actif ? "Désactiver" : "Réactiver"}</Button>
              </li>
            ))}
          </ul>
          <div className="grid grid-cols-[1fr_80px_auto] gap-2">
            <Input aria-label="Nouvelle unité" placeholder="Nouvelle unité" value={unite.libelle} onChange={(e) => setUnite({ ...unite, libelle: e.target.value })} />
            <Input aria-label="Symbole" placeholder="Symbole" value={unite.symbole} onChange={(e) => setUnite({ ...unite, symbole: e.target.value })} />
            <Button variant="outline" disabled={!unite.libelle.trim()} onClick={() => { appel("/negoce/unites", "POST", unite); setUnite({ libelle: "", symbole: "" }); }}>Ajouter</Button>
          </div>
        </section>
      </div>
    </Modal>
  );
}

"use client";

import { useCallback, useEffect, useState } from "react";
import { Plus, Trash2 } from "lucide-react";
import { api } from "@/lib/api";
import { peutEcrire, useAuth } from "@/lib/auth";
import { type Option } from "@/lib/negoce";
import { filtrerDecimal, formatDH, lireNombre } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field, Input, Select } from "@/components/ui/input";
import { Sheet } from "@/components/ui/sheet";
import { PartenaireCombo } from "@/components/partenaire-combo";
import { Alerte, LIGNE_CLIQUABLE, TABLE_BOITE, TD, TH, THEAD, Vide, messageErreur, useArticles } from "@/components/negoce/commun";

type Ligne = { id: string; nom: string; description: string | null; actif: boolean; membres: number; regles: number };
type Regle = { cle: string; portee: "article" | "categorie"; articleId: string; categorieId: string; type: string; valeur: string };
type Detail = {
  id: string; nom: string; description: string | null; actif: boolean;
  regles: { id: string; articleId: string | null; categorieId: string | null; type: string; valeur: number }[];
  membres: { clientId: string; nom: string }[];
};

const TYPES: Record<string, string> = { pourcent: "Remise en %", montant: "Remise en DH par unité", prix: "Prix fixé" };
let compteur = 0;
const cle = () => `r${++compteur}`;

export default function GroupesPage() {
  const { user } = useAuth();
  const ecrit = peutEcrire(user, "negoce_groupes");
  const [liste, setListe] = useState<Ligne[] | null>(null);
  const [erreur, setErreur] = useState("");
  const [ouvert, setOuvert] = useState<string | "nouveau" | null>(null);

  const charger = useCallback(() => {
    api<Ligne[]>("/negoce/groupes").then((r) => { setListe(r); setErreur(""); }).catch((e) => setErreur(messageErreur(e, "Chargement impossible.")));
  }, []);
  useEffect(() => { charger(); }, [charger]);

  return (
    <div>
      <div className="mb-5 flex items-center gap-3">
        <p className="max-w-2xl text-[14px] text-secondaire">Un groupe de clients regroupe des tarifs spéciaux : remise en pourcentage, remise en dirhams par unité ou prix fixé par article. Un client appartient à un seul groupe ; un prix fixé l&apos;emporte sur une remise.</p>
        {ecrit && <Button className="ml-auto shrink-0" onClick={() => setOuvert("nouveau")}><Plus size={16} />Nouveau groupe</Button>}
      </div>
      <Alerte message={erreur} />
      {liste && liste.length === 0 ? <Vide texte="Aucun groupe de clients." /> : (
        <div className={TABLE_BOITE}>
          <table className="w-full text-left">
            <thead className={THEAD}><tr><th className={TH}>Groupe</th><th className={`${TH} text-right`}>Clients</th><th className={`${TH} text-right`}>Règles</th><th className={TH}></th></tr></thead>
            <tbody>
              {liste?.map((g) => (
                <tr key={g.id} className={LIGNE_CLIQUABLE} onClick={() => setOuvert(g.id)}>
                  <td className={TD}><div className="font-semibold">{g.nom}</div>{g.description && <div className="text-[12px] text-attenue">{g.description}</div>}</td>
                  <td className={`${TD} text-right font-display`}>{g.membres}</td>
                  <td className={`${TD} text-right font-display`}>{g.regles}</td>
                  <td className={TD}>{!g.actif && <Badge ton="neutre">Inactif</Badge>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {ouvert && <GroupeSheet id={ouvert === "nouveau" ? null : ouvert} ecrit={ecrit} onFermer={() => setOuvert(null)} onChange={charger} />}
    </div>
  );
}

function GroupeSheet({ id, ecrit, onFermer, onChange }: { id: string | null; ecrit: boolean; onFermer: () => void; onChange: () => void }) {
  const articles = useArticles();
  const [categories, setCategories] = useState<Option[]>([]);
  const [idCourant, setIdCourant] = useState(id);
  const [nom, setNom] = useState("");
  const [description, setDescription] = useState("");
  const [actif, setActif] = useState(true);
  const [regles, setRegles] = useState<Regle[]>([]);
  const [membres, setMembres] = useState<Detail["membres"]>([]);
  const [client, setClient] = useState<{ id: string | null; texte: string }>({ id: null, texte: "" });
  const [erreur, setErreur] = useState("");
  const [info, setInfo] = useState("");

  const charger = useCallback((gid: string) => {
    api<Detail>(`/negoce/groupes/${gid}`).then((d) => {
      setNom(d.nom); setDescription(d.description ?? ""); setActif(d.actif); setMembres(d.membres);
      setRegles(d.regles.map((r) => ({ cle: cle(), portee: r.articleId ? "article" : "categorie", articleId: r.articleId ?? "", categorieId: r.categorieId ?? "", type: r.type, valeur: String(r.valeur).replace(".", ",") })));
    }).catch((e) => setErreur(messageErreur(e, "Chargement impossible.")));
  }, []);
  useEffect(() => { if (id) charger(id); }, [id, charger]);
  useEffect(() => { api<Option[]>("/negoce/categories").then(setCategories).catch(() => undefined); }, []);

  const maj = (k: string, p: Partial<Regle>) => setRegles((rs) => rs.map((r) => (r.cle === k ? { ...r, ...p } : r)));
  const enregistrer = async () => {
    setErreur(""); setInfo("");
    const corps = {
      nom, description, actif,
      regles: regles.map((r) => ({ articleId: r.portee === "article" ? r.articleId || null : null, categorieId: r.portee === "categorie" ? r.categorieId || null : null, type: r.type, valeur: lireNombre(r.valeur) })),
    };
    try {
      if (idCourant) await api(`/negoce/groupes/${idCourant}`, { method: "PUT", body: JSON.stringify(corps) });
      else { const r = await api<{ id: string }>("/negoce/groupes", { method: "POST", body: JSON.stringify(corps) }); setIdCourant(r.id); }
      setInfo("Groupe enregistré."); onChange();
    } catch (e) { setErreur(messageErreur(e)); }
  };
  const ajouterClient = async () => {
    if (!idCourant || !client.id) return;
    try { await api(`/negoce/groupes/${idCourant}/membres`, { method: "POST", body: JSON.stringify({ clientId: client.id }) }); setClient({ id: null, texte: "" }); charger(idCourant); onChange(); } catch (e) { setErreur(messageErreur(e)); }
  };
  const retirerClient = async (cid: string) => {
    try { await api(`/negoce/groupes/${idCourant}/membres/${cid}`, { method: "DELETE" }); charger(idCourant!); onChange(); } catch (e) { setErreur(messageErreur(e)); }
  };
  const prixPublic = (aid: string) => articles.find((a) => a.id === aid)?.prixVente;

  return (
    <Sheet open onOpenChange={(o) => !o && onFermer()} large title={idCourant ? nom || "Groupe" : "Nouveau groupe"} description="Tarifs spéciaux et clients du groupe"
      footer={<><Button variant="outline" onClick={onFermer}>Fermer</Button>{ecrit && <Button onClick={enregistrer} disabled={!nom.trim()}>Enregistrer</Button>}</>}>
      <Alerte message={erreur} />
      {info && <p className="mb-4 rounded-lg bg-vert-fond px-4 py-2 text-[14px] text-vert-texte" role="status">{info}</p>}
      <div className="mb-8 grid gap-4 sm:grid-cols-2">
        <Field label="Nom du groupe" required><Input value={nom} disabled={!ecrit} onChange={(e) => setNom(e.target.value)} /></Field>
        <Field label="Description"><Input value={description} disabled={!ecrit} onChange={(e) => setDescription(e.target.value)} /></Field>
        <label className="flex items-center gap-2 text-[14px]"><input type="checkbox" checked={actif} disabled={!ecrit} onChange={(e) => setActif(e.target.checked)} />Groupe actif</label>
      </div>

      <section className="mb-8">
        <h3 className="mb-2 font-display text-[15px] font-bold">Règles de prix</h3>
        {regles.length === 0 && <p className="mb-3 text-secondaire">Aucune règle : les clients du groupe paient le prix public.</p>}
        <ul className="mb-3 space-y-2">
          {regles.map((r) => (
            <li key={r.cle} className="grid items-center gap-2 sm:grid-cols-[120px_1fr_170px_110px_auto]">
              <Select aria-label="Portée" disabled={!ecrit} value={r.portee} onChange={(e) => maj(r.cle, { portee: e.target.value as Regle["portee"] })}><option value="article">Article</option><option value="categorie">Catégorie</option></Select>
              {r.portee === "article" ? (
                <Select aria-label="Article" disabled={!ecrit} value={r.articleId} onChange={(e) => maj(r.cle, { articleId: e.target.value })}>
                  <option value="">Choisir un article</option>{articles.map((a) => <option key={a.id} value={a.id}>{a.code} · {a.designation}</option>)}
                </Select>
              ) : (
                <Select aria-label="Catégorie" disabled={!ecrit} value={r.categorieId} onChange={(e) => maj(r.cle, { categorieId: e.target.value })}>
                  <option value="">Choisir une catégorie</option>{categories.map((c) => <option key={c.id} value={c.id}>{c.libelle}</option>)}
                </Select>
              )}
              <Select aria-label="Type de règle" disabled={!ecrit} value={r.type} onChange={(e) => maj(r.cle, { type: e.target.value })}>{Object.entries(TYPES).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</Select>
              <Input aria-label="Valeur" inputMode="decimal" disabled={!ecrit} value={r.valeur} onChange={(e) => maj(r.cle, { valeur: filtrerDecimal(e.target.value) })} />
              {ecrit && <Button variant="ghost" size="sm" aria-label="Retirer la règle" onClick={() => setRegles((rs) => rs.filter((x) => x.cle !== r.cle))}><Trash2 size={16} /></Button>}
              {r.portee === "article" && r.articleId && prixPublic(r.articleId) !== undefined && <span className="text-[12px] text-attenue sm:col-span-5">Prix public : {formatDH(prixPublic(r.articleId)!)}</span>}
            </li>
          ))}
        </ul>
        {ecrit && <Button variant="outline" size="sm" onClick={() => setRegles((rs) => [...rs, { cle: cle(), portee: "article", articleId: "", categorieId: "", type: "pourcent", valeur: "" }])}><Plus size={14} />Ajouter une règle</Button>}
      </section>

      <section>
        <h3 className="mb-2 font-display text-[15px] font-bold">Clients du groupe</h3>
        {!idCourant ? <p className="text-secondaire">Enregistrez le groupe pour y ajouter des clients.</p> : (
          <>
            {membres.length === 0 ? <p className="mb-3 text-secondaire">Aucun client dans ce groupe.</p> : (
              <ul className="mb-3 divide-y divide-bordure rounded-lg border border-bordure">
                {membres.map((m) => (
                  <li key={m.clientId} className="flex items-center justify-between px-4 py-2 text-[14px]"><span>{m.nom}</span>{ecrit && <Button variant="ghost" size="sm" onClick={() => retirerClient(m.clientId)}>Retirer</Button>}</li>
                ))}
              </ul>
            )}
            {ecrit && (
              <div className="grid gap-2 sm:grid-cols-[1fr_auto]">
                <PartenaireCombo id={client.id} texte={client.texte} placeholder="Rechercher un client à ajouter" onChange={(cid, texte) => setClient({ id: cid, texte })} />
                <Button variant="outline" disabled={!client.id} onClick={ajouterClient}>Ajouter au groupe</Button>
              </div>
            )}
            <p className="mt-2 text-[12px] text-attenue">Ajouter un client déjà membre d&apos;un autre groupe le déplace dans celui-ci.</p>
          </>
        )}
      </section>
    </Sheet>
  );
}

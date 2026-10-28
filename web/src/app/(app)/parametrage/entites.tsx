"use client";

import { useEffect, useRef, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { cn, formatDateHeure, lireNombre } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/input";

type Entite = {
  code: string; raisonSociale: string; formeJuridique: string | null; capital: number | null; siege: string | null; ville: string | null; telephone: string | null; email: string | null;
  ice: string | null; identifiantFiscal: string | null; rc: string | null; tribunalRc: string | null; patente: string | null; cnss: string | null; banque: string | null; rib: string | null;
  logo: boolean; modifiePar: string | null; modifieLe: string | null;
};
type Vue = { modifiable: boolean; entites: Entite[] };

/** Identité légale de chaque entité : elle figure en tête des factures et des avoirs. */
export function EntitesParam() {
  const [vue, setVue] = useState<Vue | null>(null);
  const [code, setCode] = useState("APT");
  useEffect(() => { api<Vue>("/parametres/entites").then(setVue).catch(() => undefined); }, []);
  if (!vue) return null;
  const e = vue.entites.find((x) => x.code === code) ?? vue.entites[0];
  if (!e) return null;
  return (
    <div className="rounded-xl border border-bordure bg-white p-6">
      <h2 className="text-[15px] font-bold">Entités : identité légale et logo</h2>
      <p className="mt-1 text-[13px] text-secondaire">Ces informations figurent sur les factures et les avoirs de chaque entité. Les valeurs de départ sont des données de test, à remplacer.</p>
      <div className="mt-4 flex gap-2">
        {vue.entites.map((x) => (
          <button key={x.code} aria-pressed={x.code === e.code} onClick={() => setCode(x.code)}
            className={cn("cursor-pointer rounded-full border px-3.5 py-1.5 text-[13px] font-semibold", x.code === e.code ? "border-marine bg-marine text-white" : "border-bordure-forte bg-white hover:bg-fond")}>{x.code} · {x.raisonSociale}</button>
        ))}
      </div>
      <Fiche key={e.code} e={e} modifiable={vue.modifiable} onChange={(n) => setVue({ ...vue, entites: vue.entites.map((x) => (x.code === n.code ? n : x)) })} />
    </div>
  );
}

function Fiche({ e, modifiable, onChange }: { e: Entite; modifiable: boolean; onChange: (e: Entite) => void }) {
  const [f, setF] = useState({
    raisonSociale: e.raisonSociale, formeJuridique: e.formeJuridique ?? "", capital: e.capital === null ? "" : String(e.capital).replace(".", ","), siege: e.siege ?? "", ville: e.ville ?? "",
    telephone: e.telephone ?? "", email: e.email ?? "", ice: e.ice ?? "", identifiantFiscal: e.identifiantFiscal ?? "", rc: e.rc ?? "", tribunalRc: e.tribunalRc ?? "",
    patente: e.patente ?? "", cnss: e.cnss ?? "", banque: e.banque ?? "", rib: e.rib ?? "",
  });
  const [message, setMessage] = useState<{ ok: boolean; texte: string } | null>(null);
  const [version, setVersion] = useState(0);
  const fichier = useRef<HTMLInputElement>(null);
  const echec = (x: unknown, defaut: string) => setMessage({ ok: false, texte: x instanceof ApiError ? x.message : defaut });

  const enregistrer = async () => {
    try {
      onChange(await api<Entite>(`/parametres/entites/${e.code}`, { method: "PUT", body: JSON.stringify({ ...f, capital: f.capital.trim() ? lireNombre(f.capital) : null }) }));
      setMessage({ ok: true, texte: "Entité enregistrée." });
    } catch (x) { echec(x, "Enregistrement impossible."); }
  };
  const deposer = async (file: File) => {
    const corps = new FormData(); corps.append("fichier", file);
    try {
      const res = await fetch(`/api/parametres/entites/${e.code}/logo`, { method: "POST", body: corps, credentials: "include" });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) throw new ApiError(res.status, data.message ?? "Dépôt du logo impossible.");
      onChange(data as Entite); setVersion(version + 1); setMessage({ ok: true, texte: "Logo enregistré." });
    } catch (x) { echec(x, "Dépôt du logo impossible."); }
  };
  const retirer = async () => {
    try { onChange(await api<Entite>(`/parametres/entites/${e.code}/logo`, { method: "DELETE" })); setMessage({ ok: true, texte: "Logo retiré." }); }
    catch (x) { echec(x, "Retrait impossible."); }
  };
  const champ = (k: keyof typeof f, label: string, opts?: { className?: string; required?: boolean; placeholder?: string }) => (
    <Field label={label} required={opts?.required} className={opts?.className}>
      <Input disabled={!modifiable} placeholder={opts?.placeholder} value={f[k]} onChange={(ev) => { setF({ ...f, [k]: ev.target.value }); setMessage(null); }} />
    </Field>
  );

  return (
    <div className="mt-5">
      <div className="flex flex-wrap items-center gap-4 rounded-lg bg-fond p-4">
        <div className="flex h-16 w-32 items-center justify-center rounded border border-dashed border-bordure-forte bg-white text-[12px] text-attenue">
          {/* eslint-disable-next-line @next/next/no-img-element */}
          {e.logo ? <img src={`/api/parametres/entites/${e.code}/logo?v=${version}`} alt={`Logo de ${e.raisonSociale}`} className="max-h-full max-w-full object-contain" /> : "Aucun logo"}
        </div>
        <div className="text-[13px] text-secondaire">
          <div>Le logo n&apos;apparaît sur les documents qu&apos;une fois déposé ici. Image PNG ou JPEG, 1 Mo au plus.</div>
          {modifiable && (
            <div className="mt-2 flex gap-2">
              <input ref={fichier} type="file" accept="image/png,image/jpeg" className="hidden" onChange={(ev) => { const x = ev.target.files?.[0]; if (x) deposer(x); ev.target.value = ""; }} />
              <Button variant="outline" size="sm" onClick={() => fichier.current?.click()}>{e.logo ? "Remplacer le logo" : "Déposer un logo"}</Button>
              {e.logo && <Button variant="outline" size="sm" onClick={retirer}>Retirer</Button>}
            </div>
          )}
        </div>
      </div>
      <div className="mt-4 grid gap-4 sm:grid-cols-2">
        {champ("raisonSociale", "Raison sociale", { required: true })}
        {champ("formeJuridique", "Forme juridique", { placeholder: "SARL ou SARL AU" })}
        {champ("capital", "Capital social, en DH")}
        {champ("ville", "Ville")}
        {champ("siege", "Siège social, adresse complète", { className: "sm:col-span-2" })}
        {champ("telephone", "Téléphone")}
        {champ("email", "Email")}
        {champ("ice", "ICE, 15 chiffres")}
        {champ("identifiantFiscal", "I.F., identifiant fiscal")}
        {champ("rc", "R.C., registre du commerce")}
        {champ("tribunalRc", "Tribunal du R.C., ville")}
        {champ("patente", "T.P., taxe professionnelle")}
        {champ("cnss", "C.N.S.S.")}
        {champ("banque", "Banque")}
        {champ("rib", "RIB, 24 chiffres")}
      </div>
      {e.modifiePar && <p className="mt-3 text-[12px] text-attenue">Modifié par {e.modifiePar} le {formatDateHeure(e.modifieLe)}.</p>}
      {modifiable ? (
        <div className="mt-4 flex items-center gap-3">
          <Button onClick={enregistrer}>Enregistrer</Button>
          {message && <span role={message.ok ? "status" : "alert"} className={cn("text-[13px]", message.ok ? "text-vert-texte" : "text-rouge-texte")}>{message.texte}</span>}
        </div>
      ) : <p className="mt-4 text-[13px] text-secondaire">Consultation seule.</p>}
    </div>
  );
}

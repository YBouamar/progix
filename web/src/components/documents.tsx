"use client";

import { useEffect, useRef, useState } from "react";
import { Download, Trash2, Upload } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { cn, formatDate, formatTaille } from "@/lib/utils";
import { Button } from "@/components/ui/button";

type Fichier = { id: string; nom: string; taille: number; contentType: string; ajoutePar: string; date: string; peutSupprimer: boolean };

type Liste = { peutDeposer: boolean; imagesSeulement?: boolean; tailleMax: number; tailleMorceau: number; fichiers: Fichier[] };

const extension = (nom: string) => (nom.includes(".") ? nom.split(".").pop()!.slice(0, 4).toUpperCase() : "?");

/** Dépôt et liste de fichiers rattachés à un objet (projet, étape, version de plan). */
export function Documents({ contexte, cibleId, lectureSeule, titre = "Documents", aide, categorie, compact }: {
  contexte: string; cibleId: string; lectureSeule?: boolean; titre?: string; aide?: string; categorie?: string; compact?: boolean;
}) {
  const [liste, setListe] = useState<Fichier[]>([]);
  const [envoi, setEnvoi] = useState<string | null>(null);
  const [regles, setRegles] = useState<{ peutDeposer: boolean; imagesSeulement?: boolean; tailleMax: number; tailleMorceau: number }>({ peutDeposer: false, tailleMax: 0, tailleMorceau: 8 * 1024 * 1024 });
  const [survol, setSurvol] = useState(false);
  const [erreur, setErreur] = useState("");
  const champ = useRef<HTMLInputElement>(null);
  const requete = `contexte=${contexte}&cibleId=${cibleId}${categorie ? `&categorie=${categorie}` : ""}`;

  useEffect(() => {
    api<Liste>(`/fichiers?${requete}`).then((r) => { setListe(r.fichiers); setRegles(r); }).catch(() => undefined);
  }, [requete]);

  // Envoi par morceaux : aucun gros fichier ne transite en une seule requête, et la progression est visible.
  async function envoyer(fichier: File, suivi: (pct: number) => void) {
    const { id } = await api<{ id: string }>("/fichiers/envois", {
      method: "POST", body: JSON.stringify({ contexte, cibleId, categorie, nom: fichier.name, taille: fichier.size, contentType: fichier.type }),
    });
    const morceaux: { numero: number; eTag: string }[] = [];
    const total = Math.ceil(fichier.size / regles.tailleMorceau);
    for (let n = 1; n <= total; n++) {
      const part = fichier.slice((n - 1) * regles.tailleMorceau, n * regles.tailleMorceau);
      const res = await fetch(`/api/fichiers/envois/${id}/morceaux/${n}`, {
        method: "PUT", body: part, credentials: "include", headers: { "Content-Type": "application/octet-stream" },
      });
      if (!res.ok) throw new ApiError(res.status, res.status >= 500 ? "Le stockage de fichiers ne répond pas." : "Dépôt impossible.");
      morceaux.push(await res.json());
      suivi(Math.round((n / total) * 100));
    }
    return api<Fichier>(`/fichiers/envois/${id}/terminer`, { method: "POST", body: JSON.stringify({ morceaux }) });
  }

  async function deposer(fichiers: FileList | null) {
    if (!fichiers || fichiers.length === 0) return;
    setErreur("");
    try {
      for (const f of Array.from(fichiers)) {
        if (f.size > regles.tailleMax) throw new ApiError(409, `${f.name} dépasse la taille maximale de ${formatTaille(regles.tailleMax)}.`);
        if (f.size === 0) throw new ApiError(409, `${f.name} est vide.`);
      }
      for (const f of Array.from(fichiers)) {
        setEnvoi(`${f.name} · 0 %`);
        const ajoute = await envoyer(f, (pct) => setEnvoi(`${f.name} · ${pct} %`));
        setListe((l) => [ajoute, ...l]);
      }
    } catch (e) {
      setErreur(e instanceof ApiError ? (e.status === 403 ? "Votre rôle ne permet pas de déposer ce type de fichier." : e.message) : "Connexion au serveur impossible.");
    } finally {
      setEnvoi(null);
      if (champ.current) champ.current.value = "";
    }
  }

  async function supprimer(f: Fichier) {
    try {
      await api(`/fichiers/${f.id}`, { method: "DELETE" });
      setListe((l) => l.filter((x) => x.id !== f.id));
    } catch (e) {
      setErreur(e instanceof ApiError ? e.message : "Suppression impossible.");
    }
  }

  return (
    <div className={compact ? "mt-3" : "mt-5 rounded-xl border border-bordure bg-white p-6"}>
      <h3 className={compact ? "mb-1.5 text-[13px] font-semibold text-secondaire" : "mb-4 text-[15px] font-bold"}>{titre} <span className="font-display text-attenue">{liste.length}</span></h3>
      {erreur && <div role="alert" className="mb-3 rounded-lg bg-rouge-fond px-4 py-2 text-rouge-texte">{erreur}</div>}

      {!lectureSeule && regles.peutDeposer && (
        <div
          onDragOver={(e) => { e.preventDefault(); setSurvol(true); }} onDragLeave={() => setSurvol(false)}
          onDrop={(e) => { e.preventDefault(); setSurvol(false); deposer(e.dataTransfer.files); }}
          className={cn("rounded-lg border-2 border-dashed text-center", compact ? "mb-2 px-3 py-2 text-[13px]" : "mb-4 px-4 py-6", survol ? "border-marine bg-marine/5" : "border-bordure-forte")}>
          {!compact && <Upload size={20} className="mx-auto mb-1 text-attenue" />}
          <div className="font-semibold">
            {envoi ? `Envoi de ${envoi}...` : <>{regles.imagesSeulement ? "Déposer des photos ici ou" : "Déposer des fichiers ici ou"} <button type="button" className="cursor-pointer text-marine underline" onClick={() => champ.current?.click()}>parcourir</button></>}
          </div>
          {!compact && <div className="text-[13px] text-secondaire">{aide} Jusqu&apos;à {formatTaille(regles.tailleMax)} par fichier.</div>}
          <input ref={champ} type="file" multiple hidden accept={regles.imagesSeulement ? "image/jpeg,image/png,image/webp,image/heic,image/heif" : undefined} onChange={(e) => deposer(e.target.files)} />
        </div>
      )}

      {liste.length === 0 && (lectureSeule || !regles.peutDeposer) && <div className="text-[13px] text-attenue">Aucun fichier.</div>}
      <ul className="divide-y divide-bordure">
        {liste.map((f) => (
          <li key={f.id} className="flex items-center gap-3 py-2.5">
            {f.contentType.startsWith("image/") ? (
              // eslint-disable-next-line @next/next/no-img-element
              <img src={`/api/fichiers/${f.id}/contenu`} alt="" loading="lazy" className="h-11 w-14 shrink-0 rounded-md object-cover" />
            ) : (
              <span className="flex h-9 w-11 shrink-0 items-center justify-center rounded-md bg-neutre-fond font-display text-[11px] font-bold text-neutre-texte">{extension(f.nom)}</span>
            )}
            <div className="min-w-0 flex-1">
              <div className="truncate font-semibold">{f.nom}</div>
              <div className="text-[12px] text-secondaire">{formatTaille(f.taille)} · ajouté par {f.ajoutePar} le {formatDate(f.date)}</div>
            </div>
            <Button asChild variant="outline" size="sm"><a href={`/api/fichiers/${f.id}/contenu`} download={f.nom} aria-label={`Télécharger ${f.nom}`}><Download size={14} />{!compact && "Télécharger"}</a></Button>
            {f.peutSupprimer && !lectureSeule && (
              <Button variant="ghost" size="icon" aria-label={`Supprimer ${f.nom}`} onClick={() => supprimer(f)}><Trash2 size={15} /></Button>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}

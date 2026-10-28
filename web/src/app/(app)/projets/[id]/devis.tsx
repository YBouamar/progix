"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Plus } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { formatDate, formatDH } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { TON_ETAT, type DevisDetail, type DevisListe } from "../../devis/types";

export function DevisProjet({ projetId, lectureSeule }: { projetId: string; lectureSeule: boolean }) {
  const router = useRouter();
  // L'administrateur consulte les devis sans en créer.
  const { user } = useAuth();
  const peutCreer = !lectureSeule && user?.role !== "administrateur";
  const [liste, setListe] = useState<DevisListe["items"]>([]);
  const [erreur, setErreur] = useState("");
  const [envoi, setEnvoi] = useState(false);

  useEffect(() => {
    api<DevisListe>(`/devis?projetId=${projetId}&pageSize=100`).then((r) => setListe(r.items)).catch(() => undefined);
  }, [projetId]);

  async function creer() {
    setEnvoi(true); setErreur("");
    try {
      const d = await api<DevisDetail>("/devis", { method: "POST", body: JSON.stringify({ projetId }) });
      router.push(`/devis/${d.id}`);
    } catch (e) {
      setErreur(e instanceof ApiError ? e.message : "Création impossible.");
      setEnvoi(false);
    }
  }

  return (
    <div className="mt-5 rounded-xl border border-bordure bg-white p-6">
      <div className="mb-3 flex items-center justify-between gap-3">
        <h3 className="text-[15px] font-bold">Devis</h3>
        {peutCreer && <Button size="sm" variant="outline" disabled={envoi} onClick={creer}><Plus size={14} />Nouveau devis</Button>}
      </div>
      {erreur && <div role="alert" className="mb-3 rounded-lg bg-rouge-fond px-4 py-2 text-rouge-texte">{erreur}</div>}
      {liste.length === 0 ? <div className="text-[13px] text-attenue">Aucun devis pour ce projet.</div> : (
        <ul className="divide-y divide-bordure">
          {liste.map((d) => (
            <li key={d.id}>
              <Link href={`/devis/${d.id}`} className="flex flex-wrap items-center gap-3 py-2.5 hover:text-marine">
                <span className="font-display font-bold text-marine">{d.numero} · version {d.version}</span>
                <Badge ton={TON_ETAT[d.etat] ?? "neutre"}>{d.etatLabel}</Badge>
                <span className="text-[13px] text-secondaire">{formatDate(d.dateDevis)}</span>
                <span className="ml-auto font-display font-bold">{formatDH(d.totalTTC)} TTC</span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

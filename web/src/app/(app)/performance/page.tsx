"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { peutLire, useAuth } from "@/lib/auth";

/** Ancienne page unique : elle renvoie vers la performance des dessinateurs ou des ingénieurs, selon les accès de la personne. */
export default function Performance() {
  const { user } = useAuth();
  const router = useRouter();
  useEffect(() => {
    if (!user) return;
    router.replace(peutLire(user, "performance_dessinateurs") ? "/performance/dessinateurs" : peutLire(user, "performance_ingenieurs") ? "/performance/ingenieurs" : "/");
  }, [user, router]);
  return null;
}

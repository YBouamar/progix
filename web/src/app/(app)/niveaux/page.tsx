"use client";

import { NiveauxParam } from "../parametrage/niveaux";

/** Page des niveaux des dalles, accessible depuis le menu. */
export default function NiveauxPage() {
  return (
    <div className="max-w-4xl">
      <NiveauxParam />
    </div>
  );
}

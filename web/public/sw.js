// Service worker de PROGIX : consultation hors ligne des écrans déjà ouverts.
// Lecture seule : aucune saisie n'est mise en file d'attente, une action sans réseau échoue clairement.
const VERSION = "progix-v1";
const STATIQUE = VERSION + "-statique";
const DONNEES = VERSION + "-donnees";

self.addEventListener("install", () => self.skipWaiting());

self.addEventListener("activate", (e) => {
  e.waitUntil(
    caches.keys().then((cles) => Promise.all(cles.filter((c) => !c.startsWith(VERSION)).map((c) => caches.delete(c)))).then(() => self.clients.claim())
  );
});

// À la déconnexion, les données consultées quittent l'appareil.
self.addEventListener("message", (e) => {
  if (e.data === "vider") e.waitUntil(caches.delete(DONNEES));
});

async function reseauPuisCache(requete, nom) {
  const cache = await caches.open(nom);
  try {
    const reponse = await fetch(requete);
    if (reponse.ok) cache.put(requete, reponse.clone());
    return reponse;
  } catch (erreur) {
    const connue = await cache.match(requete);
    if (connue) return connue;
    throw erreur;
  }
}

self.addEventListener("fetch", (e) => {
  const requete = e.request;
  if (requete.method !== "GET") return;
  const url = new URL(requete.url);
  if (url.origin !== self.location.origin) return;

  // Fichiers versionnés de l'application : le cache suffit.
  if (url.pathname.startsWith("/_next/static/") || url.pathname.startsWith("/icone-")) {
    e.respondWith(
      caches.open(STATIQUE).then(async (cache) => (await cache.match(requete)) || fetch(requete).then((r) => { if (r.ok) cache.put(requete, r.clone()); return r; }))
    );
    return;
  }

  // Documents PDF, exports et fichiers : jamais gardés.
  if (/\/(pdf|export|telecharger|contenu|modele)(\/|\.|$)/.test(url.pathname) || url.pathname.startsWith("/api/fichiers") || url.pathname.startsWith("/api/verification")) return;

  if (url.pathname.startsWith("/api/")) { e.respondWith(reseauPuisCache(requete, DONNEES)); return; }

  // Pages : le réseau d'abord, sinon la dernière version vue, sinon l'accueil en cache.
  if (requete.mode === "navigate") {
    e.respondWith(reseauPuisCache(requete, STATIQUE).catch(async () => (await caches.match("/")) || Response.error()));
    return;
  }
  if (requete.headers.get("RSC") || url.searchParams.has("_rsc")) e.respondWith(reseauPuisCache(requete, STATIQUE));
});

# TODO — PROGIX

**Dernière mise à jour** : 2026-10-06

Liste des tâches de développement, par priorité. Cocher `[x]` quand c'est fait, et reporter l'état dans [docs/AVANCEMENT.md](docs/AVANCEMENT.md).

Les tâches marquées **(à valider)** demandent une décision avant de commencer.

## P1 — Fondations (à faire en premier)

- [ ] **Initialiser le dépôt git** (à valider) : aucun historique n'existe, donc pas de diff ni de revue possibles. Ajouter un premier commit de référence avant toute modification importante.
- [ ] **Établir la base de référence** : lancer `cd web && npm run build && npm run lint` et `dotnet build backend/Progix.sln`, puis noter le résultat (succès ou erreurs) dans `docs/AVANCEMENT.md`.
- [ ] **Créer le projet de tests backend** (xUnit) dans `backend/tests/`, et l'ajouter à `Progix.sln`.
- [ ] **Tester les règles métier de calcul et de numérotation** :
  - calcul des montants du devis (`Devis/Domain/Calcul.cs`) ;
  - numérotation devis, courriers et bordereaux (`NumerotationDevis.cs`, `NumerotationCourriers.cs`, `NumerotationLivraisons.cs`) ;
  - ordre des 11 étapes, double validation et rôles autorisés (`Projets/Domain/Etape.cs`) ;
  - transitions de statut du projet (`Projet.cs`).
- [ ] **Mettre en place l'intégration continue** (GitHub Actions ou équivalent) : build backend, build et lint frontend, tests backend. Dépend du dépôt git.

## P2 — Qualité et finition

- [ ] **Reprendre les derniers ajouts du module Projets** (migrations du 5 et 6 octobre) : rapports d'écrasement, import des tendons, conformité des dalles, auto-contrôle, tension sur deux côtés, paramètres de coulis, validation chantier. Vérifier que chaque onglet correspond à son endpoint et au bon rôle.
- [ ] **Vérifier le parcours complet d'une dalle** avec les données de démonstration : étapes 1 à 11 dans l'ordre, validations technique et administrative, blocage des étapes hors ordre.
- [ ] **Vérifier les documents PDF** (devis, contrat, courrier, fiche, bordereau, attestation) : contenu, numérotation et montants en lettres (`Facturation/Pdf/`).
- [ ] **Ajouter des tests frontend** au besoin (outil à choisir, par exemple Vitest) — à faire après le backend.
- [ ] **Supprimer ou archiver `_to_delete/`** (74 archives d'étapes passées) (à valider) : ne rien supprimer sans accord explicite.

## P3 — Exploitation et sécurité

- [ ] **Sauvegarde hors serveur automatisée** : aujourd'hui le rapatriement est manuel (`DEPLOIEMENT.md`, section 7). Étudier une copie automatique vers un stockage distant (à valider : lieu et outil).
- [ ] **Compte Metabase en lecture seule en production** : le README le recommande, mais ce n'est pas encore documenté dans `DEPLOIEMENT.md`. À ajouter à la procédure.
- [ ] **Nettoyer les valeurs de développement** dans `backend/src/Progix.Api/appsettings.json` (clé JWT, identifiants MinIO, mot de passe administrateur). La production les surcharge déjà via `docker-compose.prod.yml` ; réduire le risque en cas de mauvaise configuration.
- [ ] **Revoir `AllowedHosts: "*"`** dans `appsettings.json`, à restreindre au domaine de production.

## P4 — Fonctionnel (à préciser avec vous)

- [ ] **Préciser les écrans à livrer** : aujourd'hui, toutes les entrées du menu ont une page, mais le niveau de finition de chacune n'est pas établi. Décider de la liste des écrans à considérer comme « livrés ».
- [ ] **Mode hors ligne pour les PDF et fichiers joints** : aujourd'hui non gardés hors ligne (limitation documentée dans le README). À confirmer si c'est attendu.

## Terminé

- [x] **2026-10-06** : fichiers de contexte et de suivi créés (`CLAUDE.md`, `docs/ARCHITECTURE.md`, `docs/AVANCEMENT.md`, `TODO.md`).

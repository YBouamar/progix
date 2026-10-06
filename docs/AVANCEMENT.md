# Avancement de PROGIX

**Dernière mise à jour** : 2026-10-06

## Légende

- **Présent** : le code existe (endpoints, entités, écran).
- **Vérifié** : testé de bout en bout (démo ou test automatisé). Aucun écran n'est encore marqué comme vérifié, faute de tests automatisés.
- **À reprendre** : travail commencé, signalé par une dernière migration ou un écran incomplet.

Cet état est établi à partir du code, pas d'une exécution. Il sera mis à jour au fil des tâches de [TODO.md](../TODO.md).

## 1. Modules backend

| Module | Présent | Routes (≈) | Migrations | Dernière migration | Statut |
|---|---|---|---|---|---|
| Identity | oui | 11 | 1 | 2026-10-04 | Présent |
| Partenaires | oui | 7 | 2 | 2026-10-05 | Présent |
| Projets | oui | 61 | 24 | 2026-10-06 (validation chantier) | Présent, **à reprendre** (évolutions récentes) |
| Devis | oui | 15 | 5 | 2026-10-06 (paramètres devis) | Présent |
| Fichiers | oui | 6 | 3 | 2026-10-05 (catégories) | Présent |
| Facturation | oui | 53 | 19 | 2026-10-06 (montant d'acompte) | Présent |

Les migrations les plus récentes du module Projets (du 5 au 6 octobre) concernent les rapports d'écrasement, l'import des tendons, la conformité des dalles, l'auto-contrôle, la tension sur deux côtés, les paramètres de coulis et la validation chantier. Ces ajouts n'ont pas de test automatisé.

## 2. Écrans frontend

Toutes les entrées du menu (`web/src/lib/nav.ts`) ont une page.

| Écran | Route | Rôles principaux | Statut |
|---|---|---|---|
| Tableau de bord (direction, commercial, logistique, personnel) | `/` | Tous | Présent |
| Projets (liste, fiche à onglets) | `/projets`, `/projets/[id]` | Chef de projet, directeur, dessinateurs, ingénieurs, chantier | Présent, **à reprendre** (16 onglets) |
| Devis (liste, détail, import) | `/devis`, `/devis/[id]` | Commercial, ingénieurs, administratif | Présent |
| Clients et partenaires | `/clients` | Commercial | Présent |
| Livraisons | `/livraisons` | Commercial | Présent |
| Facturation | `/facturation` | Commercial | Présent |
| Trésorerie | `/tresorerie` | Commercial | Présent |
| Logistique | `/logistique` | Logistique, direction | Présent |
| Répartition des ingénieurs | `/repartition/ingenieurs` | Ingénieur en chef | Présent |
| Répartition des dessinateurs | `/repartition/dessinateurs` | Dessinateur en chef | Présent |
| Performance | `/performance` | Ingénieur en chef, dessinateur en chef | Présent |
| Paramétrage (entités, utilisateurs, accessoires, catégories de dépense, défauts) | `/parametrage` | Logistique, direction | Présent |
| Vérification publique d'un document | `/verification/[jeton]` | Public | Présent |
| Connexion | `/login` | Public | Présent |
| Pages non définies | `[...slug]` | — | Affiche « Écran en cours de développement » |

## 3. Infrastructure et exploitation

| Élément | Statut |
|---|---|
| Docker Compose développement (`docker-compose.yml`) | Présent |
| Docker Compose production + Caddy (HTTPS) | Présent |
| Script de sauvegarde (`deploiement/sauvegarde.sh`) | Présent |
| Données de démonstration (`scripts/donnees-demo.py`) | Présent |
| Metabase (analyse, facultatif) | Présent dans `docker-compose.yml` |
| PWA et mode hors ligne (lecture seule) | Présent |
| Tâche de purge des fichiers (Hangfire, quotidienne) | Présent |
| Dépôt git | **Absent** |
| Tests automatisés (backend, frontend) | **Absents** |
| Intégration continue | **Absente** |

## 4. Journal

- **2026-10-06** : mise en place des fichiers de contexte (`CLAUDE.md`, `docs/ARCHITECTURE.md`) et de suivi (`docs/AVANCEMENT.md`, `TODO.md`), à partir de l'analyse du code.

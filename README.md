# PROGIX

Système d'information métier pour les dalles en béton précontraint par post-tension.

## Stack

| Couche | Technologie |
| --- | --- |
| Front web | Next.js (React, TypeScript), Tailwind CSS, composants de type shadcn/ui |
| API | ASP.NET Core 8 (C#), REST + OpenAPI (Swagger) |
| Architecture | Monolithe modulaire, un schéma PostgreSQL et un DbContext par module |
| Données | PostgreSQL 16, Entity Framework Core (migrations appliquées au démarrage) |
| Cache et sessions | Redis |
| Fichiers | MinIO (compatible S3) |
| Tâches planifiées | Hangfire |
| Authentification | ASP.NET Identity, jeton JWT dans un cookie HttpOnly |

## Démarrer

```bash
docker compose up --build
```

| Service | Adresse |
| --- | --- |
| Application | http://localhost:3000 |
| API (Swagger) | http://localhost:8080/swagger |
| Hangfire | http://localhost:8080/hangfire |
| MinIO (console) | http://localhost:9001 |

## Comptes de démonstration

Mot de passe commun : `Progix#2026`

| Rôle | Identifiant |
| --- | --- |
| Directeur | directeur |
| Chef de projet | chef.projet |
| Responsable administratif | responsable.administratif |
| Administrateur | administrateur |
| Agent commercial | agent.commercial |
| Ingénieur en chef | ingenieur.chef |
| Ingénieur | ingenieur |
| Dessinateur en chef | dessinateur.chef |
| Dessinateur | dessinateur |
| Responsable logistique | responsable.logistique |
| Responsable chantier | responsable.chantier |
| Chef de chantier | chef.chantier |

## Données fictives

Une fois PROGIX démarré, cette commande remplit la base de données inventées :

```
docker compose --profile demo run --rm demo
```

Elle crée 10 clients, 10 partenaires, 5 comptes de plus, 10 projets à tous les stades (du devis en brouillon au projet terminé), leurs dalles et leur suivi de chantier,
les devis, contrats, bons de livraison, factures, règlements, avoirs, dépenses, bordereaux et attestations, ainsi que le stock.
Tout passe par l'API, avec les comptes de démonstration : les règles métier et les numérotations s'appliquent comme à l'écran.
La commande s'ajoute aux données existantes et ne s'exécute qu'une fois (elle s'arrête si le premier projet fictif existe déjà).
Les documents portent des dates étalées sur cinq mois ; les étapes de chantier, elles, sont horodatées au moment de l'exécution.

## Tableaux d'analyse (Metabase, facultatif)

```
docker compose --profile analyse up -d
```

Metabase s'ouvre sur http://localhost:3001. Au premier lancement, créer le compte administrateur Metabase puis ajouter la base PROGIX :
type PostgreSQL, hôte `postgres`, port `5432`, base `progix`, utilisateur `progix`, mot de passe de `POSTGRES_PASSWORD`.
Chaque module a son schéma (identity, partenaires, projets, devis, fichiers, facturation). Pour la production, préférer un utilisateur PostgreSQL en lecture seule.

## Tablette et hors ligne

PROGIX s'installe sur tablette comme une application : dans Chrome ou Safari, menu puis « Ajouter à l'écran d'accueil ».
L'installation et le mode hors ligne demandent une adresse en HTTPS (ou `localhost`).

Sans réseau, les écrans déjà ouverts restent consultables, avec leurs dernières données connues, et un bandeau le signale.
Les saisies, validations et signatures demandent le réseau : rien n'est mis en attente. Les PDF et les fichiers joints ne sont pas gardés hors ligne.
À la déconnexion, les données gardées sur l'appareil sont effacées.

## Mise en production

L'installation sur un serveur (HTTPS, secrets, sauvegardes, mises à jour) est décrite dans [DEPLOIEMENT.md](DEPLOIEMENT.md).
Elle utilise `docker-compose.prod.yml`, distinct du fichier de développement.

## Organisation

```
backend/src/Progix.Api                    Hôte : charge les modules, Swagger, Hangfire, Redis
backend/src/Progix.SharedKernel           Contrat IModule, rôles, politiques
backend/src/Modules/Progix.Modules.*      Un module métier = Domain, Application, Infrastructure, Endpoints
web/src/app                               Écrans Next.js
```

Pour repartir d'une base vide : `docker compose down -v`.

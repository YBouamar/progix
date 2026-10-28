# PROGIX : contexte pour Claude

Système d'information métier d'une entreprise marocaine de dalles en béton précontraint par post-tension : clients et partenaires, devis, contrats, livraisons, factures, paiements, trésorerie, suivi de chantier en 11 étapes par dalle, commandes usine, stock, signatures électroniques, courriels aux directeurs.

Ce dépôt est une **refonte complète**. L'ancienne version (Next.js, Supabase, Vercel) est abandonnée : ne rien reprendre de Supabase ni de Vercel.

**Langue** : application, code métier, commentaires, documentation et réponses en **français**. **Pas de tiret long** dans les fichiers.

## Début de session (obligatoire)

1. Lire [docs/PROGRESS.md](docs/PROGRESS.md) (état réel, bloquants, écarts code / règles métier).
2. Lire [docs/NEXT_STEPS.md](docs/NEXT_STEPS.md) (prochaines étapes priorisées).
3. Avant toute modification **fonctionnelle ou d'architecture** : consulter [docs/BUSINESS_RULES.md](docs/BUSINESS_RULES.md) et [docs/DECISIONS.md](docs/DECISIONS.md). Si le code contredit une règle, le signaler dans PROGRESS.md au lieu de trancher seul.

Autres références : [docs/PROJECT.md](docs/PROJECT.md) (périmètre, rôles), [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) (modules, données, flux), [README.md](README.md), [DEPLOIEMENT.md](DEPLOIEMENT.md).
`TODO.md` et `docs/AVANCEMENT.md` sont les anciens fichiers de suivi (2026-10-06), remplacés par PROGRESS.md et NEXT_STEPS.md : ne plus les tenir à jour.

## Fin de session

Lancer `/handoff` : réécrit PROGRESS.md, met à jour NEXT_STEPS.md et DECISIONS.md, propose d'éventuels ajouts à ce fichier, sans toucher au code.

## Stack réellement en place

- **Backend** : ASP.NET Core 8 (C#, `net8.0`), monolithe modulaire. Hôte `backend/src/Progix.Api`, noyau `backend/src/Progix.SharedKernel`, modules `backend/src/Modules/Progix.Modules.*` (Identity, Partenaires, Projets, Devis, Fichiers, Facturation).
- **Données** : PostgreSQL 16, EF Core 8 (Npgsql). Un DbContext et un schéma par module. Migrations appliquées au démarrage de l'API.
- **Frontend** : Next.js 15 (App Router), React 19, TypeScript, Tailwind CSS 4, composants maison de style shadcn/ui (Radix, cva, `web/src/components/ui/`). PWA lecture seule hors ligne.
- **Services** : Redis (enregistré, pas encore utilisé dans le code), MinIO (S3, bucket `progix`), Hangfire (purge des fichiers, envoi des courriels), Metabase (profil `analyse`, facultatif), MailKit (SMTP).
- **Documents** : PDFsharp-MigraDoc (PDF), ClosedXML (Excel), QRCoder (QR de vérification).
- **Auth** : ASP.NET Identity, JWT (12 h) dans un cookie HttpOnly, rôles et politiques dans `Progix.SharedKernel/Roles.cs`.
- **Tests** : xUnit (`backend/tests/Progix.Tests`, règles de domaine sans base de données). Intégration continue : `.github/workflows/ci.yml` (build, tests, lint, build web).
- **Absents pour l'instant** : application mobile React Native + Expo, tests frontend, tests d'intégration avec base.

## Commandes

```bash
# Environnement complet (Postgres, Redis, MinIO, API, web). docker-compose.override.yml est chargé
# automatiquement : code monté, dotnet watch et next dev, rechargement à chaud.
docker compose up --build
# Images figées, comme en production
docker compose -f docker-compose.yml up --build
# Données de démonstration (une seule fois, passe par l'API)
docker compose --profile demo run --rm demo
# Metabase sur http://localhost:3001
docker compose --profile analyse up -d

# Frontend seul
cd web && npm ci && npm run dev     # npm run build sert aussi de contrôle de types ; npm run lint

# Backend seul (SDK .NET 8 requis)
dotnet build backend/Progix.sln
dotnet test backend/Progix.sln          # tests xUnit de backend/tests/Progix.Tests
dotnet run --project backend/src/Progix.Api

# Nouvelle migration (exemple : module Projets)
dotnet ef migrations add NomDeLaMigration --project backend/src/Modules/Progix.Modules.Projets --startup-project backend/src/Progix.Api
```

Adresses en développement : web http://localhost:3000, Swagger http://localhost:8080/swagger, Hangfire http://localhost:8080/hangfire, console MinIO http://localhost:9001. Comptes de démonstration : voir README.

Sur le poste Windows actuel : pas de SDK .NET 8 ni de Python. Builder le backend dans un conteneur (Git Bash, depuis `backend/`) :
`MSYS_NO_PATHCONV=1 docker run --rm -v "$(pwd -W):/src" -w /src mcr.microsoft.com/dotnet/sdk:8.0 dotnet test Progix.sln` (build + tests).
Scripts de vérification de l'API : Node (`fetch`), pas Python. Après plusieurs modifications d'un même fichier C#, `dotnet watch` peut afficher des erreurs transitoires : `docker compose restart api`.

En développement, le service worker de la PWA peut servir d'anciens fichiers JavaScript (non versionnés par `next dev`) : recharger avec Ctrl+Maj+R pour voir un changement d'écran.

**Danger** : `docker compose down -v` efface la base et le stockage.

## Conventions

- **Module** = `Domain/`, `Application/` (si besoin), `Infrastructure/` (DbContext, migrations, numérotation, lookups), `Endpoints/`, plus une classe `XxxModule : IModule` enregistrée dans `Program.cs`.
- **Communication entre modules** : uniquement par les interfaces de `Progix.SharedKernel/Contracts.cs` (`IProjetsLookup`, `IPartenairesLookup`, `IDevisLookup`, `IReaffectationEntite`, `INotificationsDirection`...). Jamais de lecture des tables d'un autre module, jamais de table dans le schéma d'un autre module.
- **Routes** : minimal API sous `/api/...`, regroupées par `MapGroup`, identifiants typés (`{id:guid}`, `{n:int}`), autorisation par politique (`Policies.*`) ou par rôles.
- **Erreurs métier** : `Results.Conflict(new { message })` (helper `Conflit`) avec un message en français prêt à afficher ; erreurs de saisie par `Results.ValidationProblem`.
- **Nommage** : classes, propriétés et méthodes en français (`NumeroSuivantAsync`, `EtapesFaites`, `PeutSaisir`, `Verrou`, `Refus`). Codes de rôle en snake_case français. Tables en snake_case.
- **Rôles** : ne jamais écrire un code de rôle en dur ; utiliser `Roles.*` ou les tableaux de `Policies`.
- **Statuts et étapes** : constantes de domaine (`StatutsProjet` dans `Projet.cs`, `Etapes` dans `Etape.cs`). Ne pas changer ordre ni numéros sans migration de données.
- **Numérotation** : toujours côté serveur, par compteur en base, par entité et par an. Jamais côté client.
- **Documents émis** (facture, avoir, BL, contrat, courrier, reçu) : ne se modifient ni ne se suppriment ; ils s'annulent avec un motif tracé.
- **Frontend** : écrans dans `web/src/app/(app)/`, un fichier par onglet dans `projets/[id]/`. Menus par rôle dans `web/src/lib/nav.ts`. Appels API via `web/src/lib/api.ts`. Formats français via `web/src/lib/utils.ts` (`formatDH`, `formatDate`, `lireNombre`, `filtrerDecimal`).
- **Commentaires** : brefs, en français, expliquant la règle métier (style `/// <summary>` côté C#).

## Règles strictes

- Ne jamais modifier une migration committée ou appliquée : en ajouter une nouvelle.
- Ne jamais écrire de secret dans un fichier suivi. `.env`, `deploiement/`, `docker-compose.prod.yml` : configuration de production, ne pas toucher sans demande explicite.
- Les valeurs de `backend/src/Progix.Api/appsettings.json` (clé JWT, mots de passe) sont des valeurs de développement.
- Monnaie unique : dirham (DH). Aucun champ pays, aucune devise étrangère.
- Ne pas inventer une règle métier : ce qui n'est pas dans BUSINESS_RULES.md se demande.
- Commits : seulement sur demande. Le dépôt git existe (remote `origin` : github.com/YBouamar/progix).

## Vérifier un changement

`dotnet test backend/Progix.sln` (API, à compléter d'un test pour toute règle métier ajoutée ou modifiée), `npm run build` et `npm run lint` (web), puis l'usage dans l'écran concerné avec les données de démonstration. Classes internes testables : `InternalsVisibleTo Progix.Tests` dans les modules Projets et Facturation.

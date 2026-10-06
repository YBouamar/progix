# PROGIX — contexte pour Claude

Système d'information métier pour les dalles en béton précontraint par post-tension (suivi du devis à la réception finale, facturation, trésorerie, logistique, chantier).

**Langue** : l'application, les commentaires, les noms de domaine et la documentation sont en **français**. Répondre et écrire le code métier en français, en gardant la convention déjà en place.

## Documents de suivi (à lire selon le besoin)

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) : modules, flux, rôles, règles métier clés.
- [docs/AVANCEMENT.md](docs/AVANCEMENT.md) : état de chaque module et écran.
- [TODO.md](TODO.md) : tâches de développement à faire, par priorité.
- [README.md](README.md) et [DEPLOIEMENT.md](DEPLOIEMENT.md) : démarrage et mise en production.

Quand une tâche est terminée, cocher l'entrée dans `TODO.md` et mettre à jour l'état concerné dans `docs/AVANCEMENT.md`.

## Stack

- **Backend** : ASP.NET Core 8 (C#), monolithe modulaire. Hôte dans `backend/src/Progix.Api`, modules dans `backend/src/Modules/Progix.Modules.*`.
- **Frontend** : Next.js 15 (App Router), React 19, TypeScript, Tailwind CSS 4. Code dans `web/src`.
- **Données** : PostgreSQL 16, Entity Framework Core. Un schéma et un DbContext par module. Migrations appliquées au démarrage de l'API.
- **Autres** : Redis (cache), MinIO (fichiers, compatible S3), Hangfire (tâches planifiées, tableau de bord en développement sur `/hangfire`), Metabase (analyse, facultatif).
- **Auth** : ASP.NET Identity, JWT dans un cookie HttpOnly, politiques d'autorisation dans `Progix.SharedKernel`.

## Commandes

```bash
# Tout l'environnement (Postgres, Redis, MinIO, API, web)
docker compose up --build

# Données de démonstration (une seule fois)
docker compose --profile demo run --rm demo

# Frontend seul
cd web && npm run dev      # serveur de développement
cd web && npm run build    # build de production, sert aussi de contrôle de types
cd web && npm run lint

# Backend seul
dotnet build backend/Progix.sln
dotnet run --project backend/src/Progix.Api

# Nouvelle migration EF Core (exemple, module Projets)
dotnet ef migrations add NomDeLaMigration --project backend/src/Modules/Progix.Modules.Projets --startup-project backend/src/Progix.Api
```

Attention : `docker compose down -v` efface la base et le stockage.

## Conventions

- **Un module = Domain, Application, Infrastructure, Endpoints**, plus une classe `XxxModule` qui implémente `IModule` (`Progix.SharedKernel/IModule.cs`) et est enregistrée dans `Program.cs`.
- **Routes API** sous `/api/...`, regroupées par `MapGroup`. Les identifiants de route sont typés (`{id:guid}`, `{n:int}`).
- **Codes de rôle** en snake_case français (`chef_projet`, `responsable_administratif`…), définis dans `Progix.SharedKernel/Roles.cs`. Ne jamais écrire un code de rôle en dur dans un module : utiliser la constante ou le tableau de rôles.
- **Statuts et étapes** : les statuts de projet et les 11 étapes de suivi d'une dalle sont des constantes de domaine (`Projet.cs`, `Etape.cs`). Ne pas changer leur ordre ni leur numéro sans migration de données.
- **Migrations** : ne jamais modifier une migration déjà committée ou déjà appliquée ; en ajouter une nouvelle.
- **Schéma PostgreSQL** : chaque module écrit dans son propre schéma (identity, partenaires, projets, devis, fichiers, facturation). Ne pas créer de table dans le schéma d'un autre module ; passer par son `Lookup` ou son contrat.
- **Frontend** : les écrans sont dans `web/src/app/(app)/`. La navigation par rôle est dans `web/src/lib/nav.ts`. Les appels API passent par `web/src/lib/api.ts`. Les composants UI génériques sont dans `web/src/components/ui/`.
- **Pas de tests automatisés** pour l'instant (aucun projet de test, aucun CI). Vérifier les changements avec `npm run build` / `npm run lint` côté web et `dotnet build` côté API, puis par l'usage dans l'écran concerné.

## Ne pas toucher sans demande explicite

- `_to_delete/` : archives de l'historique du projet (74 fichiers). Ne rien supprimer sans accord.
- `deploiement/`, `docker-compose.prod.yml`, `.env` : configuration de production. Ne jamais écrire de secret dans un fichier suivi.
- Les valeurs de `backend/src/Progix.Api/appsettings.json` (clé JWT, mots de passe de développement) sont des valeurs de développement. Ne pas les réutiliser en production.

## Points d'attention

- Le dépôt n'est **pas un dépôt git** pour l'instant : pas d'historique ni de diff disponibles.
- Le dossier `Écran en cours de développement` (`web/src/app/(app)/[...slug]/page.tsx`) s'affiche pour toute route non définie.
- Les comptes de démonstration (mot de passe commun dans le README) n'existent pas en production.

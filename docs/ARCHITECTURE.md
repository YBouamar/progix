# Architecture de PROGIX

**Dernière mise à jour** : 2026-10-22 (constat établi à partir du code, sans exécution).

## 1. Vue d'ensemble

```
Navigateur ou tablette (PWA)
   |  HTTPS, cookie JWT HttpOnly
   v
web/ (Next.js 15, port 3000)  -- /api/* -->  backend/src/Progix.Api (ASP.NET Core 8, port 8080)
                                               |-- Identity     schéma identity
                                               |-- Partenaires  schéma partenaires
                                               |-- Projets      schéma projets
                                               |-- Devis        schéma devis
                                               |-- Fichiers     schéma fichiers
                                               |-- Facturation  schéma facturation
                                               |
                        PostgreSQL 16 (+ schéma hangfire)   Redis   MinIO (bucket progix)   SMTP
```

Monolithe modulaire : un seul processus API ; chaque module a son Domain, son DbContext, son schéma, sa table d'historique de migrations (`__ef_migrations` dans son schéma) et ses endpoints. Les modules communiquent par les interfaces de `Progix.SharedKernel/Contracts.cs`.

## 2. Démarrage de l'API (`Progix.Api/Program.cs`)

1. Enregistrement des six modules (`IModule.AddServices`).
2. Redis (cache distribué, préfixe `progix:`), Hangfire (stockage PostgreSQL, schéma `hangfire`), Swagger, ProblemDetails.
3. `InitializeAsync` de chaque module : migrations, données de référence, compte directeur, comptes de démonstration si `Seed:DemoUsers`. Jusqu'à 15 essais espacés de 3 s si PostgreSQL n'est pas prêt.
4. Swagger et tableau de bord Hangfire seulement en développement.
5. `/api/health`, puis les endpoints de chaque module.
6. Tâche récurrente quotidienne à 3 h : `PurgeFichiers` efface du stockage les fichiers supprimés depuis plus d'un mois.
7. Notifications : `NotificationsDirection` (Progix.Api/Notifications/Courriels.cs) met en file Hangfire un courriel MailKit (5 essais) vers les directeurs actifs.

## 3. Modules et modèle de données

| Module | Tables (schéma) | Migrations | Routes principales |
|---|---|---|---|
| Identity | `users`, `roles` + tables Identity | 1 (2026-10-04) | `/api/auth` (login, logout, me, change-password), `/api/users`, `/api/roles` |
| Partenaires | `partenaires`, `contacts` | 2 | `/api/partenaires` (liste, compteurs, doublon, doublons-probables, archive, import Excel et trame) |
| Projets | `projets`, `dalles`, `etapes`, `etape_traces`, `versions_plans`, `rapports_ecrasement`, `commandes_usine`, `accessoires`, `articles_stock`, `mouvements_stock`, `fiches_auto_controle`, `fiches_tension`, `mesures_tension`, `fiches_injection`, `parametres_tension`, `parametres_coulis`, `niveaux`, `types_construction`, `affectations`, `projet_partenaires`, `remarques`, `remarque_traces`, `compteurs` | 27 (jusqu'au 2026-10-28) | `/api/projets/...` (dalles, etapes, commande, tension, injection, versions, rapports, auto-contrôle, équipe, partenaires, remarques), `/api/logistique`, `/api/repartition`, `/api/performance`, `/api/parametres/{tension,accessoires,coulis,niveaux,types-construction}` |
| Devis | `devis`, `lignes`, `evenements`, `parametres` (dont la grille des forfaits), `compteurs` | 6 | `/api/devis` (tableau de bord, ingénieurs, CRUD brouillon, envoyer, valider, annuler = rejeter, signer, nouvelle-version, trame de calcul et import), `/api/parametres/{devis,forfaits}` |
| Fichiers | `fichiers` | 3 | `/api/fichiers` (dépôt par morceaux, catégories, suppression logique) |
| Facturation | `bons_livraison`, `lignes_livraison`, `factures`, `lignes_facture`, `avoirs`, `encaissements`, `depenses`, `categories_depense`, `entites`, `contrats`, `courriers`, `signatures_fiches`, `compteurs` | 19 | `/api/factures`, `/api/livraisons`, `/api/avoirs`, `/api/encaissements`, `/api/tresorerie`, `/api/depenses`, `/api/tableau-de-bord/finances`, contrats, courriers, fiches, PDF, `/api/parametres/{entites,categories-depense}` |

Toutes les entités héritent de `Entity` (`Id` Guid, `CreatedAt`, `UpdatedAt`). Les listes imbriquées (lignes de commande, échéances, systèmes, points de contrôle, câbles d'injection) sont stockées en JSON dans leur ligne parente (**À confirmer** sur le mapping exact, non relu pour chaque cas).

Relations principales (par identifiant, sans clé étrangère entre schémas) :

```
Partenaire 1--n Projet (ClientId)          Projet 1--n Dalle 1--n EtapeDalle (n = 1..11)
Projet 1--n ProjetPartenaire (bet, bct, architecte, laboratoire)
Dalle 1--1 CommandeUsine, FicheTension + n MesureTension, FicheInjection, FicheAutoControle
Dalle 1--n VersionPlan (lettre A, B, C...), RapportEcrasement
Projet 1--n DevisEntete (Numero commun à toutes les versions) 1--n LigneDevis (une par dalle)
Projet 1--n BonLivraison 1--n LigneLivraison (une dalle sur un seul BL)
Facture 1--n LigneFacture, Encaissement, Avoir ; BonLivraison n--1 Facture
```

## 4. Contrats entre modules (`Contracts.cs`)

`IPartenairesLookup`, `IUtilisateursLookup` (dont vérification du mot de passe pour signer), `IDevisLookup` (devis validés et prix net par dalle), `IProjetsLookup`, `IProjetsStatut` (passage En étude / retour Créé), `IReaffectationEntite` (renumérotation de tous les documents), `IContexteFichiers` (droits de dépôt par contexte), `IFichiersLookup`, `IVerificationDocuments` (QR code), `IDevisDocuments`, `IFichesChantier`, `IEncoursProjet` (rappel avant injection), `INotificationsDirection`.

## 5. Flux principaux

1. **Commercial** : client (contrôle de doublon) puis projet (code `APT-2026-00018`) puis dalles puis devis brouillon, envoyé, signé, validé (le projet passe En étude) puis contrat puis facture d'acompte éventuelle.
2. **Chantier** : pour chaque dalle, étapes 1 à 11 dans l'ordre ; le projet passe En réalisation dès qu'une dalle a terminé l'étape 3, Terminé quand toutes ses dalles ont terminé l'étape 11.
3. **Logistique** : la commande usine (étape 4) est saisie ou importée, validée (technique puis administrative), puis suivie : validée, en fabrication, livrée (le stock est consommé), reçue.
4. **Livraison et facturation** : BL sur des dalles dont la commande usine est faite ; facture de livraison au prix net du devis validé le plus récent, acompte déduit au prorata ; encaissements signés par mot de passe, reçus `REC-` ; avoirs pour toute correction.
5. **Trésorerie** : journal mensuel des encaissements, remboursements et dépenses ; un effet compte à son échéance.
6. **Alertes** : courriel aux directeurs après réception de dalle (6), mise en tension (9), injection (10) et chaque paiement.

## 6. Frontend (`web/src`)

- `app/login`, `app/verification/[jeton]` (public), `app/(app)/layout.tsx` (coque, menu par rôle, bandeau hors ligne).
- Écrans : tableau de bord (direction, commercial, logistique, personnel), projets (liste, fiche à onglets : informations, dalles et suivi, commercial, courriers, dépenses, intervenants, documents...), devis, clients, livraisons, facturation, trésorerie, logistique (et fiche `logistique/[dalleId]`), répartition des ingénieurs et des dessinateurs, performance, paramétrage.
- `[...slug]/page.tsx` affiche « Écran en cours de développement » pour toute route inconnue.
- `lib/api.ts` (fetch `/api` avec cookie, `ApiError`), `lib/auth.tsx`, `lib/nav.ts`, `lib/utils.ts`. PWA : `public/sw.js`, `components/hors-ligne.tsx`.

## 7. Environnement Docker

| Fichier | Usage |
|---|---|
| `docker-compose.yml` | postgres, redis, minio (ports 5432, 9000, 9001), api (8080), web (3000) ; profils `demo` (script Python) et `analyse` (Metabase sur 3001) |
| `docker-compose.override.yml` | chargé d'office en local : `Dockerfile.dev`, code monté, `dotnet watch`, `next dev` |
| `docker-compose.prod.yml` | production : Caddy (80/443, HTTPS automatique), aucun autre port exposé, secrets obligatoires depuis `.env`, pas de comptes de démonstration |

Volumes : `pgdata`, `miniodata` (+ `caddydata`, `caddyconfig` en production).

## 8. Déploiement cible

VPS Contabo, domaine `progix.bacybrains.com` (cible donnée dans le cadrage, absente des fichiers du dépôt). Procédure générique dans [DEPLOIEMENT.md](../DEPLOIEMENT.md) : rsync du code, `.env` depuis `deploiement/env.production.exemple`, `docker compose -f docker-compose.prod.yml --env-file .env up -d --build`, sauvegarde nocturne `deploiement/sauvegarde.sh` (14 jours, sur le même serveur).

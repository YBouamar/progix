# Architecture de PROGIX

Document de référence technique et métier. Constat établi à partir du code le 2026-10-06.

## 1. Vue d'ensemble

```
Navigateur (PWA, tablette)
   │  HTTPS, cookie JWT HttpOnly
   ▼
web/ (Next.js)  ──────────────►  backend/src/Progix.Api (ASP.NET Core 8)
                                     │
                                     ├── Identity      (comptes, JWT)
                                     ├── Partenaires   (clients, partenaires)
                                     ├── Projets       (projets, dalles, suivi de chantier, logistique, performance)
                                     ├── Devis         (devis, signature, import)
                                     ├── Facturation   (factures, avoirs, livraisons, contrats, trésorerie, PDF)
                                     └── Fichiers      (stockage MinIO, envois)
                                     │
            ┌────────────────────────┼────────────────────────┐
         PostgreSQL 16           Redis (cache)           MinIO (fichiers)
     (1 schéma par module)                              Hangfire (tâches)
```

Monolithe modulaire : un seul processus API, mais chaque module a son Domain, son DbContext, son schéma et ses endpoints. Les modules communiquent par leurs contrats (`Progix.SharedKernel/Contracts.cs`) et leurs classes `Lookup`, pas en lisant les tables des autres.

## 2. Backend

### 2.1 Démarrage (`Progix.Api/Program.cs`)

1. Chargement des six modules (`IModule`) et de leurs services.
2. Redis (cache distribué), Hangfire (stockage PostgreSQL, schéma `hangfire`), Swagger (uniquement en développement).
3. Initialisation de chaque module : migrations, données de référence, compte directeur. Reprise jusqu'à 15 fois si PostgreSQL n'est pas prêt.
4. Mapping des endpoints `/api/...` de chaque module.
5. Tâche quotidienne à 3 h : `PurgeFichiers` efface du stockage les fichiers supprimés depuis plus d'un mois.

### 2.2 Modules

| Module | Rôle | Endpoints principaux | Migrations |
|---|---|---|---|
| Identity | Comptes, connexion, JWT, rôles | `/api/auth`, `/api/users` | 1 |
| Partenaires | Clients et partenaires, attestations d'exonération | `/api/partenaires` | 2 |
| Projets | Projets, dalles, étapes de chantier, équipe, commandes usine, tension, injection, rapports d'écrasement, auto-contrôle, logistique, stock, répartition, performance, tableau de bord | `/api/projets/...`, `/api/logistique`, `/api/repartition`, `/api/parametres/tension`, `/api/parametres/accessoires`, `/api/parametres/coulis` | 24 |
| Devis | Devis, états, signature, import | `/api/devis/...` | 5 |
| Fichiers | Stockage, pièces jointes, catégories, envois | `/api/fichiers` | 3 |
| Facturation | Factures, avoirs, bons de livraison, contrats, courriers, encaissements, dépenses, trésorerie, entités, PDF, signatures | `/api/factures`, `/api/livraisons`, `/api/parametres/entites`, `/api/parametres/categories-depense` | 19 |

Nombre de routes HTTP (comptage des `Map*` dans le code, indicatif) : Projets ≈ 61, Facturation ≈ 53, Devis ≈ 15, Identity ≈ 11, Partenaires ≈ 7, Fichiers ≈ 6.

Chaque module contient :
- `Domain/` : entités et règles (ex. `Projet.cs`, `Etape.cs`, `Devis.cs`, `Calcul.cs`, `Facture.cs`).
- `Application/` : DTO et logique d'application (présent dans Identity, Partenaires, Projets).
- `Infrastructure/` : DbContext, migrations, numérotation, stockage, PDF de dépendance, lookups.
- `Endpoints/` : routes minimales (`MapGroup`).
- `Pdf/` (Facturation) : génération des documents (contrat, courrier, fiche, exports de trésorerie, montants en lettres).

### 2.3 Numérotation

Les numéros de devis, de factures, de courriers et de bordereaux sont générés côté serveur (`NumerotationDevis.cs`, `NumerotationCourriers.cs`, `NumerotationLivraisons.cs`). Ne jamais les calculer côté client.

### 2.4 Sécurité

- Politiques dans `Progix.SharedKernel/Roles.cs` : `Commercial`, `Devis`, `GestionUtilisateurs`, etc. Les listes de rôles par domaine (`RolesDevis`, `RolesLogistique`, `RolesDirection`, `RolesCommercial`) y sont définies.
- Le champ `VoitMontants` des rôles contrôle la visibilité des montants.
- Tableau de bord Hangfire : sans authentification, activé uniquement en développement.
- Le cookie HttpOnly est posé par l'API ; le front ne stocke pas le jeton.

## 3. Rôles

| Code | Libellé | Accès (menu) |
|---|---|---|
| `directeur` | Directeur | Tout |
| `chef_projet` | Chef de projet | Tout ; contrôle de toutes les étapes |
| `responsable_administratif` | Responsable administratif | Tableau de bord, projets, devis, clients, livraisons, facturation, trésorerie |
| `administrateur` | Administrateur | Idem responsable administratif |
| `agent_commercial` | Agent commercial | Tableau de bord, projets |
| `ingenieur_chef` | Ingénieur en chef | Tableau de bord, projets, devis, répartition des ingénieurs, performance |
| `ingenieur` | Ingénieur | Tableau de bord, projets, devis |
| `dessinateur_chef` | Dessinateur en chef | Tableau de bord, projets, répartition des dessinateurs, performance |
| `dessinateur` | Dessinateur | Tableau de bord, projets |
| `responsable_logistique` | Responsable logistique | Tableau de bord, projets, logistique, paramétrage |
| `responsable_chantier` | Responsable chantier | Tableau de bord, projets |
| `chef_chantier` | Chef de chantier | Tableau de bord, projets |

Source : `web/src/lib/nav.ts` (menus). Les droits d'action réels sont dans les endpoints et `Roles.cs`.

## 4. Règles métier clés

### 4.1 Projet

Statuts, dans l'ordre : `cree` (Créé) → `etude` (En étude) → `realisation` (En réalisation) → `termine` (Terminé). Définis dans `Projet.cs`.

Un projet a un type de construction (`immeuble` par défaut) et une entité juridique (`Entites.Apt` par défaut).

### 4.2 Suivi de chantier d'une dalle (11 étapes, ordre strict)

| N° | Étape | Double validation | Rôles autorisés |
|---|---|---|---|
| 1 | Plans d'exécution | non | dessinateur_chef, dessinateur |
| 2 | Validation BCT | non | ingénieurs, responsable administratif, administrateur |
| 3 | Réception du coffrage | non | responsable chantier, chef de chantier |
| 4 | Commande usine | oui, **après** la réalisation | responsable logistique |
| 5 | Pose de la précontrainte | non | chantier |
| 6 | Réception de dalle | non | chantier |
| 7 | Coulage du béton | non | chantier |
| 8 | Rapport d'écrasement | non | chantier + ingénieurs |
| 9 | Mise en tension | oui, **avant** la saisie + validation chantier | chantier |
| 10 | Injection | oui, **avant** la saisie | chantier |
| 11 | Réception finale | non | chantier + responsable administratif |

Le chef de projet et le directeur contrôlent toutes les étapes. Définition : `Progix.Modules.Projets/Domain/Etape.cs`.

Validation technique puis administrative pour la commande usine, la mise en tension et l'injection.

### 4.3 Devis

Calcul des montants dans `Devis/Domain/Calcul.cs`. Signature électronique via `SignatureDevis` (migration de 2026-10-06). Visibilité des montants restreinte aux rôles `VoitMontants`.

### 4.4 Facturation

Entités : factures, avoirs (avec annulation), encaissements (règlements, reçus de paiement), bons de livraison, contrats, courriers, dépenses (liées à une dalle ou au projet), bordereaux (numérotation, bordereaux communs), signatures de fiches et de documents, acompte exprimé en montant.

### 4.5 Fichiers

Suppression logique puis purge définitive après un mois (tâche Hangfire quotidienne). Stockage MinIO, bucket `progix`.

### 4.6 Hors ligne

PWA (`web/public/sw.js`, `web/src/components/hors-ligne.tsx`). Lecture seule des écrans déjà ouverts. Les saisies, validations et signatures exigent le réseau ; rien n'est mis en file d'attente. Les PDF et fichiers joints ne sont pas gardés hors ligne. Les données locales sont effacées à la déconnexion.

## 5. Frontend

- `web/src/app/login/` : connexion.
- `web/src/app/(app)/layout.tsx` : coque commune (menu selon le rôle, bandeau hors ligne).
- `web/src/app/(app)/<écran>/page.tsx` : un dossier par écran (projets, devis, clients, livraisons, facturation, trésorerie, logistique, performance, paramétrage, répartition des ingénieurs et dessinateurs, tableau de bord).
- `projets/[id]/` : un fichier par onglet du projet (dalles, étapes, commande, tension, injection, versions, rapports d'écrasement, auto-contrôle, dépenses, contrats, courriers, équipe, partenaires, remarques, suivi, fiche de signature).
- `verification/[jeton]/` : page publique de vérification de document (sans connexion).
- `components/ui/` : composants de base (bouton, champ, badge, modale, panneau latéral).
- `lib/api.ts` : client HTTP ; `lib/auth.tsx` : contexte de session ; `lib/nav.ts` : menus par rôle.

Pour les écrans non livrés, `[...slug]/page.tsx` affiche « Écran en cours de développement ».

## 6. Données et environnements

| Environnement | Fichiers | Particularités |
|---|---|---|
| Développement | `docker-compose.yml`, `appsettings.json` | Secrets de développement, Swagger et Hangfire ouverts, données de démonstration disponibles |
| Production | `docker-compose.prod.yml`, `deploiement/`, `.env` (sur le serveur) | Caddy (HTTPS automatique), base et stockage fermés à Internet, pas de comptes de démonstration, sauvegarde nocturne (`deploiement/sauvegarde.sh`) |

Données de démonstration : `scripts/donnees-demo.py` passe uniquement par l'API. Il ne s'exécute qu'une fois (il s'arrête si le premier projet fictif existe).

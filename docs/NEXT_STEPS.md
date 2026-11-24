# Prochaines étapes

**Dernière mise à jour** : 2026-11-22

Par ordre de priorité. Chaque étape doit pouvoir démarrer une session. Retirer une étape terminée lors du `/handoff`.

## 0. Déployer et essayer le lot du 2026-11-22

Migrations à appliquer au démarrage : `CommandesMultiples`, `ForfaitFixeTypeConstruction`, `VarianteDevis`, `RelectureDevis`, `SuiviContrat`. Essayer aussi le lot du soir (relecture des devis, dépôts de fichiers avec MinIO). Migration `CommandesMultiples` (schéma projets) à appliquer au démarrage. À essayer avec les données de démonstration puis un vrai dossier : dépôt des deux DXF par le dessinateur (catégories « Plan de câblage X / Y »), import à l'étape Commande usine, complément, modification d'une commande envoyée et lecture côté usine, rôle gestionnaire de stock (création du compte par l'administrateur), réception par un chef de chantier. Confirmer les hypothèses listées dans DECISIONS.md (2026-11-22).

## 1. Mettre la production à jour avec le dernier commit du 2026-11-12

La production (`/opt/apps/progix`) a reçu le 2026-11-12 vers 1 h 45 les commits jusqu'à `915d3f2`. Restent les commits suivants (étapes 2 et 3 du devis, avancement, avis d'affectation, documents du devis et dossier ZIP, ratios moyens), puis les retours de la séance du 13 novembre (forfait propre, ratio passif saisi, mezzanine, éléments du projet dans les habilitations), **qui contiennent deux migrations** (`ForfaitPropreEtMezzanine`, `RatioPassifSaisi`) : sauvegarde, `git pull --ff-only`, `docker compose -f docker-compose.prod.yml --env-file .env up -d --build`, puis contrôle des services et de `/api/health`.

## 2. Essais par le métier des écrans livrés les 11 et 12 novembre

Aucun retour reçu à ce jour. À essayer avec des données réelles, et noter les écarts dans PROGRESS.md :
- fiche du devis en trois étapes, avec un vrai PTTotal et un vrai RebarTotal (les RebarTotal des essais étaient fabriqués), un projet à plusieurs dalles de surfaces proches, et l'aperçu du PDF à l'étape 3 (non vérifié à l'écran) ;
- liste des devis : filtres, vue de l'ingénieur, devis à créer, avancement ;
- menus Exécution, Chantier, Logistique (« À commander ») et chiffres du menu ;
- évaluation des dessinateurs, après saisie des dates d'arrivée (Paramétrage, Accès) ;
- notifications : activer depuis la cloche sur chaque poste, essayer sur téléphone (passage par le service worker non essayé) ;
- import du fond de plan DXF dans AutoCAD 2021 et ADAPT Builder.

## 3. Décisions à prendre (aucun code avant réponse)

- Devis : un relecteur peut-il valider son propre brouillon ? Un refus laisse-t-il le brouillon modifiable ? Une variante validée sur projet lancé remplace-t-elle le devis validé (état Remplacé) ?

- Boutons de gestion à ajouter aux autres listes liées au projet (catégories de dépense, accessoires) ? Les niveaux et les types ont le leur.
- Commercial créé par le bouton : il n'a pas de mot de passe connu ; faut-il un envoi d'invitation par courriel ?

- Exécution, deux règles codées « à confirmer » : qui ouvre un dossier ; ce qu'il faut pour le terminer (plan, modèle, dessinateur, surface).
- Étude : dépôt des pièces et réouverture, règle « à confirmer ».
- Mot de passe encore demandé pour saisir un paiement, alors qu'il est retiré des signatures : le garder ?
- Évaluation des dessinateurs : le dessinateur en chef doit-il pouvoir régler les seuils et objectifs (aujourd'hui directeur et chef de projet) ?
- Chiffre du menu Devis d'un ingénieur : ses brouillons seulement, ou aussi les devis à créer et sans ingénieur ?
- Projet dont le seul devis est rejeté : le relister dans « À créer » ?
- Ratio actif à zéro : simple signalement avant l'envoi (aujourd'hui), ou blocage ?
- Politique de mot de passe : `deploiement/env.production.exemple` annonce encore majuscule et symbole, que la production n'exige plus ; corriger le texte (fichier de production, à faire sur demande).
- Nouvelle version d'un devis validé : garder l'ancienne comme référence de prix et de « commandé » jusqu'à la validation de la nouvelle ?
- Redis : l'utiliser ou le retirer de la stack ?
- Grille des forfaits : valeurs réelles des tranches.
- Le responsable administratif peut modifier la grille des forfaits mais n'a pas la page Paramétrage dans son menu.

## 4. Tests d'intégration avec base

Compléter `Progix.Tests` par des tests sur PostgreSQL (conteneur de test) : numérotation par entité et par an, plafond des espèces (facture et encaissement), refus de modification de surface, situation financière, réaffectation d'entité.

## 5. Serveur de production

- Sauvegarde de nuit : vérifier par `crontab -l` que la ligne `0 2 * * * /opt/apps/progix/deploiement/sauvegarde.sh >> /var/log/progix-sauvegarde.log 2>&1` est posée ; copie hors serveur.
- `git stash drop` (réglages locaux repris dans le dépôt), dossier `backups/` non suivi à ranger.
- Clés DataProtection à conserver hors du conteneur (avertissement au démarrage de l'API).
- Mises à jour Ubuntu en attente, clé SSH à la place du mot de passe root.

## 6. Application mobile React Native + Expo

Définir d'abord le périmètre (rôles chantier ? saisie hors ligne ?) et l'authentification (le cookie HttpOnly convient mal au mobile). Aucune ligne de code avant décision.

## Plus tard

Passage à Next.js 16 (corrige l'avis PostCSS restant), tests frontend, formule poutre, tableaux de commande, tableaux de mise en tension / injection, PDF et pièces jointes hors ligne, suppression de `TODO.md` et `docs/AVANCEMENT.md` après accord. Notifications push application fermée (clé VAPID, abonnements), sur la règle déjà codée.

# Prochaines étapes

**Dernière mise à jour** : 2026-10-23

Par ordre de priorité. Chaque étape doit pouvoir démarrer une session. Retirer une étape terminée lors du `/handoff`.

## 1. Pousser et activer l'intégration continue (demande votre accord)

Commiter le travail des 22 et 23 octobre, pousser sur `origin` (github.com/YBouamar/progix), vérifier que le workflow `CI` passe dans l'onglet Actions.

## 2. Contrôle visuel des changements du 2026-10-23

Avec `docker compose up` et les données de démonstration, sur http://localhost:3000 (le directeur se connecte avec son nouveau mot de passe, les autres comptes avec celui de démonstration) :
- fiche « Tour Marina Business Center », onglet « Devis et contrats » : bloc Situation financière ;
- même projet, modifier la surface d'une dalle : message sous le champ Surface ;
- Clients, nouveau client avec le nom d'un client existant : avertissement orange ;
- dalle à l'étape 9 ou 10 : bornes de tolérance, boutons de validation selon le rôle (chef.projet, responsable.administratif, responsable.chantier).
Critère de fin : chaque point conforme, ou écart noté dans PROGRESS.md.

## 3. Décisions à prendre (aucun code avant réponse)

- Politique de mot de passe : exiger un symbole (comme l'annonce `deploiement/env.production.exemple`) ou corriger le texte de l'exemple ?
- Nouvelle version d'un devis validé : faut-il garder l'ancienne version comme référence de prix et de « commandé » jusqu'à la validation de la nouvelle ?
- Redis : l'utiliser (cache de quoi ?) ou le retirer de la stack ?
- Rôles dessinateurs, responsable logistique et chef de chantier : tous conservés ?

## 4. Tests d'intégration avec base

Compléter `Progix.Tests` par des tests sur PostgreSQL (conteneur de test) : numérotation par entité et par an, plafond des espèces (facture et encaissement), refus de modification de surface, situation financière, réaffectation d'entité.

## 5. Déploiement sur Contabo (`progix.bacybrains.com`)

Suivre DEPLOIEMENT.md : DNS A vers le VPS, `.env` de production, SMTP avec une adresse du domaine, sauvegarde nocturne et copie hors serveur. Corriger avant : `AllowedHosts`, utilisateur Metabase en lecture seule, version de Metabase figée, clés DataProtection persistées (volume).

## 6. Application mobile React Native + Expo

Définir d'abord le périmètre (rôles chantier ? saisie hors ligne ?) et l'authentification (le cookie HttpOnly convient mal au mobile). Aucune ligne de code avant décision.

## Plus tard

Passage à Next.js 16 (corrige l'avis PostCSS restant), tests frontend, formule poutre, tableaux de commande, tableaux de mise en tension / injection, PDF et pièces jointes hors ligne, suppression de `TODO.md` et `docs/AVANCEMENT.md` après accord.

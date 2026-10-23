# Avancement de PROGIX

**Dernière mise à jour** : 2026-10-23

Remplace `docs/AVANCEMENT.md` et `TODO.md` (2026-10-06), gardés comme historique.

## Base de référence (vérifiée le 2026-10-23)

- Web : `npm ci`, `npm run lint` et `npm run build` sans erreur (Next.js 15.5.28).
- Backend : `dotnet test backend/Progix.sln` dans le conteneur SDK .NET 8 : 0 erreur, 0 avertissement, **70 tests passés**. Aucune vulnérabilité NuGet connue (`dotnet list package --vulnerable --include-transitive`).
- `docker compose up --build` : les 6 modules démarrent, migrations appliquées, `/api/health` OK.
- Données de démonstration chargées sans erreur : le script parcourt par l'API les 11 étapes, les validations, BL, factures, règlements, avoirs et dépenses.

## Terminé

- Six modules backend : Identity (1 migration), Partenaires (2), Projets (25), Devis (5), Fichiers (3), Facturation (19).
- Partie commerciale, trésorerie, suivi de chantier en 11 étapes, logistique et stock, PDF signés, QR de vérification, courriels aux directeurs, PWA.
- **2026-10-23, règles tranchées et appliquées** (vérifiées sur l'API locale) : tolérance EN 13670 ; refus de modifier la surface d'une dalle chiffrée dans un devis envoyé, expiré ou validé ; situation financière et reste dû du projet ; doublons probables par nom ou téléphone ; validations par rôle et validation chantier de l'injection (migration `20261023090000_ValidationChantierInjection`).
- **2026-10-23, qualité** : projet de tests `backend/tests/Progix.Tests` (calcul du devis, étapes et validations, tension EN 13670, commande usine et import des tendons, facture : réglé, effets, avoirs, statuts, montants en lettres) ; workflow `.github/workflows/ci.yml` (pas encore poussé) ; dépendances vulnérables corrigées (MailKit, Newtonsoft.Json, Pkcs, Next.js) ; avertissement CS8604 et commentaire de `EtapeTrace` corrigés.
- 2026-10-23 : mot de passe du compte `directeur` de la base **locale** changé (les comptes de démonstration gardent le leur).

## En cours / à reprendre

- Nouveaux écrans (situation financière, avertissement de doublon, fiche de tension, boutons de validation) : vérifiés par l'API et le build, **pas encore regardés dans le navigateur**.
- Workflow CI écrit mais jamais exécuté (rien n'est commité ni poussé).

## Pas commencé

- Tests d'intégration avec base de données, tests frontend.
- Application mobile React Native + Expo.
- Déploiement réel sur le VPS Contabo `progix.bacybrains.com`.
- Usage de Redis (enregistré, jamais utilisé).

## Bloquants

Aucun. Sur ce poste : pas de SDK .NET 8 ni de Python, le build et les tests backend passent par Docker (voir CLAUDE.md).

## Écarts entre règles décidées et code

Aucun écart ouvert.

## Bugs connus et incohérences mineures

- `deploiement/env.production.exemple` annonce un mot de passe avec symbole, mais Identity a `RequireNonAlphanumeric = false` (décision demandée, NEXT_STEPS 3).
- Créer une nouvelle version d'un devis validé le passe à « Remplacé » : le projet n'a plus de devis validé (facturation refusée, commandé à 0) jusqu'à la validation de la nouvelle version. Comportement existant, décision demandée.
- Le rappel de paiement avant injection reste calculé sur les factures émises.
- Avis npm restant : PostCSS 8.4.31 embarqué par Next.js 15 (correction en Next 16).
- `appsettings.json` : `AllowedHosts: "*"` et secrets de développement. Sauvegardes gardées sur le serveur. Metabase en `latest`. Clés DataProtection non persistées hors du conteneur.

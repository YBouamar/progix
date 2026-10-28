# Avancement de PROGIX

**Dernière mise à jour** : 2026-10-28

Remplace `docs/AVANCEMENT.md` et `TODO.md` (2026-10-06), gardés comme historique.

## Base de référence (vérifiée le 2026-10-23)

- Web : `npm ci`, `npm run lint` et `npm run build` sans erreur (Next.js 15.5.28).
- Backend : `dotnet test backend/Progix.sln` dans le conteneur SDK .NET 8 : 0 erreur, 0 avertissement, **111 tests passés** (2026-10-28, dont l'évaluation des formules de la trame de calcul). Aucune vulnérabilité NuGet connue (`dotnet list package --vulnerable --include-transitive`).
- `docker compose up --build` : les 6 modules démarrent, migrations appliquées, `/api/health` OK.
- Données de démonstration chargées sans erreur : le script parcourt par l'API les 11 étapes, les validations, BL, factures, règlements, avoirs et dépenses.

## Terminé

- Six modules backend : Identity (1 migration), Partenaires (2), Projets (27), Devis (6), Fichiers (3), Facturation (19).
- Partie commerciale, trésorerie, suivi de chantier en 11 étapes, logistique et stock, PDF signés, QR de vérification, courriels aux directeurs, PWA.
- **2026-10-23, règles tranchées et appliquées** (vérifiées sur l'API locale) : tolérance EN 13670 ; refus de modifier la surface d'une dalle chiffrée dans un devis envoyé, expiré ou validé ; situation financière et reste dû du projet ; doublons probables par nom ou téléphone ; validations par rôle et validation chantier de l'injection (migration `20261023090000_ValidationChantierInjection`).
- **2026-10-23, qualité** : projet de tests `backend/tests/Progix.Tests` (calcul du devis, étapes et validations, tension EN 13670, commande usine et import des tendons, facture : réglé, effets, avoirs, statuts, montants en lettres) ; workflow `.github/workflows/ci.yml` (pas encore poussé) ; dépendances vulnérables corrigées (MailKit, Newtonsoft.Json, Pkcs, Next.js) ; avertissement CS8604 et commentaire de `EtapeTrace` corrigés.
- 2026-10-23 : mot de passe du compte `directeur` de la base **locale** changé (les comptes de démonstration gardent le leur).
- **2026-10-28, demandes métier** (vérifiées sur l'API locale et, pour les écrans Paramétrage et Clients, dans le navigateur) :
  - devis : ratios, systèmes et import Excel fermés à l'administrateur (remplacé l'après-midi par la consultation seule, voir ci-dessous) ;
  - niveaux des dalles paramétrables en liste libre (migration `20261028082554_NiveauxDalle`, 46 niveaux d'origine repris) ;
  - page Paramétrage sur toute la largeur, en deux colonnes ;
  - import Excel des clients et partenaires avec trame, aperçu et contrôle des doublons. Le test a créé 3 partenaires d'exemple dans la base **locale** (Immobilière Exemple SARL, M. Youssef Bennani, Bureau d'études exemple).
- **2026-10-28 après-midi, second lot** (vérifié sur l'API locale ; Paramétrage, Niveaux et fiche de devis regardés dans le navigateur) :
  - types de construction paramétrables, en liste déroulante sur le projet ; commercial choisi sur le projet et repris par ses devis (migration `TypesConstructionEtCommercial`, reprise du commercial sur les 10 projets de démonstration) ;
  - grille unique des forfaits par tranche de surface totale du devis (migration `ForfaitsEtIngenieur`) ; grille initiale 0 m² → 100, 10 000 m² → 85, **à ajuster** ;
  - ingénieur du devis ; ingénieur de réalisation unique dans l'équipe ;
  - « Annulé » affiché « Rejeté », rejet réservé au responsable administratif, au chef de projet et au directeur ;
  - administrateur en consultation seule sur les devis (API 403, écran en lecture seule, plus de bouton « Nouveau devis ») ;
  - page « Niveaux des dalles » dans le menu (directeur, chef de projet) ;
  - trame de calcul Excel du devis avec formules, réimportable (2 lignes sur 2 relues sans erreur) ;
  - script de démonstration adapté (commercial sur le projet, ingénieur sur le devis, devis établis sans l'administrateur).
  - Données de vérification créées dans la base **locale** : type « Bâtiment industriel », « Projet de vérification » avec un devis rejeté DEV-00007-2026 et le devis DEV-00008-2026 (v1 remplacée, v2 en brouillon).
- **2026-10-28 soir** : « Type d'ancrage » au lieu de « nombre de torons » ; liste fermée S1, S2, S3, S4, S7, S9, S12, S15 sur le devis, sa trame, son import et la commande usine ; surface modifiable depuis la grille du devis (enregistrée sur la dalle). Vérifié sur l'API (S5 refusé, S12 accepté, surface 60 → 75,5 sur la dalle, refus après envoi du devis) et à l'écran. Un défaut trouvé pendant la vérification a été corrigé : un devis ouvert avant une modification faite sur la fiche des dalles réécrivait l'ancienne surface.

- **2026-10-28 fin de journée** : logos et icône PROGIX intégrés (menu, connexion, vérification publique, icônes PWA et navigateur ; service worker passé en `progix-v2` pour renouveler les icônes en cache) ; vidéo de chantier en fond de la page de connexion (`web/public/medias/chantier-dalle.mp4`, désactivée si l'utilisateur demande moins d'animations) ; bandeau d'accueil du tableau de bord. Vérifié dans le navigateur par lecture de la page (vidéo lue, images chargées, bandeau affiché) ; les captures d'écran de Chrome ne répondaient plus à ce moment-là.
- **2026-10-28** : impression du brouillon de devis (bouton « Imprimer le brouillon », PDF marqué BROUILLON sans signature) ; ratios actif et passif retirés du PDF. Vérifié sur le texte des PDF générés (brouillon et devis validé signé).
- **2026-10-28** : import des exports ADAPT (PTTotal, RebarTotal) sur la fiche de la dalle, création des dalles par niveaux cochés, bouton « Reprendre les données ADAPT » dans un devis brouillon, envoi refusé si une dalle n'a pas de surface. Migration Projets `AdaptDalles`. 116 tests. Vérifié avec les fichiers réels du projet SOHAUS IMM A J1 (PH-2EME) : ratio actif 1,74, ratio passif 12,85, S1 64 câbles / 1 852,07 m, S2 2 / 55,76 m, S3 6 / 191,49 m.
- **Écart à trancher (2026-10-28)** : la gaine reprise est Strand Length (demande métier), déjà multipliée par le nombre de torons, alors que la formule du prix calcule « gaine × 3 × n ». Pour S2 et plus, le toron est donc compté deux fois (S3 : 191,49 m au lieu de 63,83 m de gaine). Soit prendre Tendon/Duct Length (aussi lue et affichée), soit retirer « × n » pour la gaine. Rien n'est changé dans la formule en attendant.
- **2026-10-28** : paiements par dalle (saisie ventilée, taux de paiement par dalle pour les rôles financiers, reçu imprimable avec détail par dalle, remboursement d'un trop-perçu sur la dalle), statut des factures déduit par imputation, factures uniquement sur BL (acompte supprimé), surface saisie sur la commande usine reprise sur BL et facture. Migrations Facturation `PaiementsParDalle` et Projets `SurfaceCommande`. 126 tests. Vérifié par l'API (refus, droits par rôle, imputation, situation, reçu PDF) et à l'écran. Les 17 paiements de démonstration ont été repris au prorata ; un paiement d'essai REC-00011-2026 a été saisi puis annulé.
- **Points à surveiller (2026-10-28)** : la reprise des parts de facture relit toutes les factures à chaque démarrage tant qu'une facture d'acompte n'a pas retrouvé son devis validé ; à alléger si le volume grandit. La saisie des paiements n'est plus possible depuis l'écran Facturation (consultation et lien vers le projet).
- **2026-10-28** : page Performance enrichie de graphes (composants maison `web/src/components/graphes.tsx`, sans bibliothèque) : dalles finies par dessinateur et par mois, surface suivie par ingénieur, projets suivis par mois, production par an, état des paiements par mode et encaissé par mois et par mode (nouvelle route `/api/statistiques/paiements`, rôles qui voient les montants). Palette des modes validée (daltonisme).
- Dossiers non suivis laissés en place : `logos_icons/` et `web/files/` (sources identiques, copiées dans `docs/marque/`) : à supprimer après accord.

## Production

- `progix.bacybrains.com` tourne une version **antérieure au commit `f97dca4`** (constaté le 2026-10-27 : les routes ajoutées le 2026-10-23 y répondent 404). La mise à jour attend les commandes fournies (pas d'accès SSH depuis ce poste).

## En cours / à reprendre

- Écrans du 2026-10-23 (situation financière, avertissement de doublon, fiche de tension, boutons de validation), fiche projet (liste des types, commercial), devis en rôle administrateur et ouverture de la trame dans Excel : vérifiés par l'API, le build et les tests, **pas encore regardés à l'écran**.
- Brouillons de devis existants : leur forfait est recalculé avec la nouvelle grille à leur prochain enregistrement (ex. une villa passe de 120 à 100 DH/m² avec la grille initiale).
- Travail du 2026-10-28 non commité.

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
- En développement, le service worker sert d'anciens fichiers JavaScript après une modification (Ctrl+Maj+R nécessaire). Sans effet en production (fichiers versionnés).
- Avis npm restant : PostCSS 8.4.31 embarqué par Next.js 15 (correction en Next 16).
- `appsettings.json` : `AllowedHosts: "*"` et secrets de développement. Sauvegardes gardées sur le serveur. Metabase en `latest`. Clés DataProtection non persistées hors du conteneur.

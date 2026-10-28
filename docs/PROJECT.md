# PROGIX : le projet

**Dernière mise à jour** : 2026-10-22

## Objectif

Donner à une entreprise marocaine de dalles en béton précontraint par post-tension un système d'information unique, du premier devis à la réception finale de chaque dalle : partie commerciale (clients, devis, contrats, factures, paiements), trésorerie, suivi de chantier en 11 étapes, commandes usine et stock, signatures électroniques et alertes aux directeurs.

Ce dépôt est une refonte complète. L'ancienne version (Next.js, Supabase, Vercel) est abandonnée.

## Entités émettrices

Deux sociétés émettent les documents, chacune avec sa numérotation (`Progix.SharedKernel/Contracts.cs`, classe `Entites`) :

| Code | Libellé |
|---|---|
| `APT` | Africa PT / MK4 (entité par défaut) |
| `BBR` | BECOMAR |

Leur identité légale (ICE, RC, IF, patente, CNSS, RIB, logo) se règle dans Paramétrage, Entités. Les valeurs livrées sont des valeurs de test.

## Utilisateurs et rôles

Rôles définis dans le code (`Progix.SharedKernel/Roles.cs`). « Voit les montants » contrôle l'accès aux devis et aux montants.

| Code | Libellé | Voit les montants | Rôle métier (d'après le code) |
|---|---|---|---|
| `directeur` | Directeur | oui | Contrôle tout. Seul à bloquer ou débloquer un projet, seul à gérer les utilisateurs. Reçoit les courriels. |
| `chef_projet` | Chef de projet | oui | Réalise toutes les étapes, donne la validation technique, supprime et restaure les projets. |
| `responsable_administratif` | Responsable administratif | oui | Chef des administrateurs. Validation administrative des étapes, annulations, attestations. |
| `administrateur` | Administrateur | oui | Partie commerciale (clients, livraisons, factures, trésorerie) ; devis en consultation seule. |
| `agent_commercial` | Agent commercial | non | Commercial (facultatif) d'un projet, choisi à sa création ; voit ses projets. |
| `ingenieur_chef` | Ingénieur en chef | oui | Répartition des ingénieurs, performance. |
| `ingenieur` | Ingénieur | oui | Validation BCT, rapports d'écrasement, devis (ingénieur du devis, trame de calcul) ; ingénieur de réalisation quand il est affecté au projet. |
| `dessinateur_chef` | Dessinateur en chef | non | Répartition des dessinateurs, plans d'exécution. |
| `dessinateur` | Dessinateur | non | Plans d'exécution (étape 1). |
| `responsable_logistique` | Responsable logistique | non | Commande usine (étape 4), suivi logistique, stock, paramétrage des accessoires. |
| `responsable_chantier` | Responsable chantier | non | Étapes de chantier, validation chantier de la mise en tension et de l'injection. |
| `chef_chantier` | Chef de chantier | non | Étapes de chantier sur les projets où il est affecté. |

Validations (décidé le 2026-10-23) : technique par le chef de projet, administrative par le responsable administratif, chantier par le responsable des chantiers ; le directeur peut donner toutes les validations.

La liste de rôles du cadrage du 2026-10-22 ne cite pas les dessinateurs, le responsable logistique ni le chef de chantier, présents dans le code et conservés. **À confirmer** s'ils doivent tous rester.

## Périmètre

- Clients et partenaires (BET, BCT, architecte, laboratoire, entreprise de construction), contrôle des doublons, exonération de TVA.
- Projets et dalles, équipe affectée, intervenants, remarques tracées, blocage, suppression logique, réaffectation à l'autre entité.
- Devis versionnés, calcul du prix au m², signature, relance, expiration, import Excel.
- Contrats, bons de livraison, factures (livraison et acompte), avoirs, encaissements, remboursements, reçus de paiement.
- Trésorerie (journal mensuel, effets à échoir, dépenses générales et de dalle, exports Excel et PDF).
- Suivi de chantier en 11 étapes par dalle, avec validations, photos, fiches (auto-contrôle, mise en tension, injection), rapports d'écrasement, versions de plans.
- Commandes usine (tendons, accessoires, import JSON / Excel / CSV), suivi logistique, stock par entité.
- Tableaux de bord (direction, commercial, logistique, personnel), répartition, performance.
- Documents PDF signés électroniquement, vérification publique par QR code.
- Courriels aux directeurs, PWA tablette en lecture seule hors ligne.
- Déploiement Docker sur un VPS (cible : Contabo, `progix.bacybrains.com`).

## Prévu mais pas commencé

- Application mobile React Native + Expo (imposée dans la stack, aucun code dans le dépôt). Périmètre **À confirmer**.
- Tests automatisés et intégration continue.

## Hors périmètre

- Toute entité ou client hors Maroc, toute autre monnaie que le dirham, tout champ pays.
- Comptabilité générale (le module calcule chiffre d'affaires, encaissements, dépenses et marge, sans plan comptable). **À confirmer**.
- Mis de côté pour l'instant : formule poutre, tableaux de commande, tableaux de mise en tension / injection.

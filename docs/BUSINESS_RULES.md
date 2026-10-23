# Règles métier de PROGIX

**Dernière mise à jour** : 2026-10-23 (écarts du 2026-10-22 tranchés et appliqués)

Sources : règles décidées (cadrage du 2026-10-22) et règles observées dans le code. Chaque règle indique où elle vit dans le code. Les écarts entre une règle décidée et le code sont listés dans [PROGRESS.md](PROGRESS.md), section « Écarts ».

Légende : **[décidé]** règle donnée par le métier ; **[code]** règle trouvée dans le code ; **[écart]** le code diffère ; **À confirmer** non tranché.

## 1. Général

- [décidé] Monnaie unique : dirham (DH). Aucune entité hors Maroc, pas de champ pays. [code] conforme : aucun champ pays ni devise.
- [code] Deux entités émettrices, `APT` et `BBR` ; chaque document porte l'entité du projet. Un projet se réaffecte à l'autre entité (directeur, chef de projet, responsable administratif) : nouveau code et renumérotation de tous ses documents, ancien numéro gardé.
- [code] TVA à 20 %.
- [code] Les documents émis ne se modifient pas et ne se suppriment pas ; ils s'annulent avec un motif obligatoire, tracé (qui, quand).
- [code] Signature électronique par mot de passe de l'utilisateur, obligatoire avant d'imprimer ou d'envoyer devis, contrat, BL, facture, avoir, courrier ; un jeton aléatoire alimente le QR code de vérification publique.

## 2. Numérotation

- [décidé] Factures `FAC-XXXXX-AAAA` ; BL, devis et avoirs (préfixe `AV`) au même format, avec incrémentation indépendante.
- [code] Compteur en base par type, par entité et par an (`NumeroSuivantAsync`). Préfixes : `DEV` (devis, numéro commun à toutes les versions), `FAC`, `AV`, `BL`, `CTR` (contrat), `REC` (reçu de paiement), `RMB` (reçu de remboursement), `BRD` (bordereaux BCT et BET, série commune), `ATT` (attestation de conformité). L'année est celle de la date du document.
- [code] Code projet : `{ENTITE}-{AAAA}-{NNNNN}` (ex. `APT-2026-00018`), année de la date de création, non modifiable ensuite.

## 3. Clients et partenaires

- [code] Types : Client final, BET, BCT, Architecte, Laboratoire, Entreprise de construction. Natures : morale, physique, autre.
- [décidé] Types de client incluant Entreprise de Construction et Autre (sans ICE / RC / CIN exigés). [décidé 2026-10-23] « Autre » reste une **catégorie** (nature), utilisable avec tous les types : un nom suffit, aucun identifiant légal. Pas de type « Autre ».
- [code] Personne morale : ICE de 15 chiffres. Pour contrat et facture, ICE, RC, identifiant fiscal et patente sont exigés, sinon la facturation est refusée. Personne physique : CIN.
- [décidé] Contrôle des doublons clients obligatoire. [décidé 2026-10-23, code] ICE ou CIN identique : **bloquant**. Même nom (sans accents, casse ni ponctuation) ou même téléphone (9 derniers chiffres), toutes catégories : **avertissement** non bloquant pendant la saisie (`/api/partenaires/doublons-probables`).
- [décidé] Exonération de TVA via une case sur la fiche client. [code] conforme ; une attestation d'exonération expirée remet d'office la TVA normale ; l'attestation n'est jamais imprimée.

## 4. Projets

- [décidé] Statuts : Créé (gris, avant tout devis validé ; confirmé le 2026-10-23), En étude (mauve), En réalisation (orange / jaune), Terminé (vert). Le suivi de chantier s'ouvre à la sortie de « Créé ».
- [code] Transitions automatiques : Créé vers En étude à la validation d'un devis ; retour à Créé si plus aucun devis validé ; En réalisation dès qu'une dalle a terminé l'étape 3 ; Terminé quand toutes les dalles ont terminé l'étape 11.
- [décidé] Seul le directeur peut bloquer un projet. [code] conforme (bloquer et débloquer). Un projet bloqué suspend les étapes de chantier et la facturation.
- [code] Suppression logique et restauration : directeur ou chef de projet, motif obligatoire ; un projet supprimé est en lecture seule.
- [code] Doublon de libellé de projet : simple avertissement.
- [code] Types de construction et forfait : immeuble 100 DH/m² (85 DH/m² à partir de 10 000 m² de surface totale du devis), villa 120, radier 120, dallage 50.

## 5. Dalles et suivi de chantier

- [décidé] Une dalle n'a pas de statut propre : on affiche son étape courante ; elle est réalisée à la fin des 11 étapes, dans un ordre strict sans exception. [code] conforme (`EtapesFaites` de 0 à 11).
- [décidé] Référence : numéro + lettre (A par défaut), la lettre s'incrémente à chaque nouvelle version des plans d'exécution. [code] `{CODE PROJET}-{NN}{Lettre}` (ex. `APT-2026-00018-01A`).
- [code] Les 11 étapes et leurs rôles (`Projets/Domain/Etape.cs`) ; directeur et chef de projet peuvent tout réaliser :

| N° | Étape | Rôles | Conditions de réalisation |
|---|---|---|---|
| 1 | Plans d'exécution | dessinateurs | dernière version des plans complète (plan, modèle) |
| 2 | Validation BCT | ingénieurs, responsable administratif, administrateur | plans figés ensuite |
| 3 | Réception du coffrage | chantier | photo |
| 4 | Commande usine | responsable logistique | au moins un tendon ; validations technique et administrative **après** la saisie |
| 5 | Pose de la précontrainte | chantier | photo |
| 6 | Réception de dalle | chantier | photo, fiche d'auto-contrôle complète |
| 7 | Coulage du béton | chantier | photo |
| 8 | Rapport d'écrasement | chantier, ingénieurs | laboratoire rattaché au projet, dernier rapport conforme avec son fichier |
| 9 | Mise en tension | chantier | validations technique, administrative et chantier **avant** la saisie ; fiche complète |
| 10 | Injection | chantier | validations technique, administrative et chantier **avant** ; fiche complète ; signature par mot de passe |
| 11 | Réception finale | chantier, responsable administratif | |

- [décidé 2026-10-23, code] Qui valide : **technique = chef de projet**, **administrative = responsable administratif**, **chantier = responsable des chantiers**. Le **directeur** peut donner toutes les validations. Personne d'autre (ni ingénieur en chef, ni chef de projet pour l'administrative ou le chantier).
- [décidé 2026-10-23] Commande usine : chef de projet + responsable administratif. Mise en tension et injection : chef de projet + responsable administratif + responsable des chantiers.
- [code] Déverrouillage : directeur ou chef de projet, dernière étape réalisée seulement, motif obligatoire, tracé. Déverrouiller l'étape 4 remet à zéro le suivi logistique.
- [décidé] Réception de dalle : photo obligatoire. [code] conforme (et photos aussi aux étapes 3, 5, 7).
- [code] Rapport non conforme : l'étape 8 reste bloquée jusqu'à un nouveau rapport conforme ; un rapport ne se modifie pas.
- [code] Avant l'injection (dalle aux étapes 8 à 9), rappel des factures non soldées du projet ; il informe, il ne bloque pas.
- [code] Avis de conformité d'une dalle réalisée (directeur, chef de projet, responsable administratif) ; seule une dalle conforme figure sur une attestation.

## 6. Mise en tension

- [décidé] Tolérance EN 13670 : ± 5 % pour le groupe, ± 15 % pour un câble seul.
- [code] Allongement mesuré = (lecture 100 % moins lecture 25 %) côté A, plus côté B si tendu des deux côtés. Allongement théorique = force × (1 moins pertes) × longueur / (section × module), paramétrable (221 kN, 150 mm², 195 000 MPa, 5 %).
- [code depuis 2026-10-23] Câble seul entre 0,85 et 1,15 fois Ao ; groupe (somme des mesures) entre 0,95 et 1,05 fois la somme des Ao (`MesureTension`, `TensionEndpoints.Calculer`).
- [code] Hors tolérance : la validation reste possible avec une remarque d'investigation obligatoire.

## 7. Commande usine, logistique et stock

- [décidé] Validation technique et administrative de la commande usine. [code] conforme.
- [décidé] Tendons : numéro simple, orientation X / Y, système, longueur, importés depuis JSON, Excel ou CSV. [code] conforme : numéro en chiffres seulement (affiché sur deux chiffres), système `S{torons}` de 1 à 99, longueur de 0 à 500 m, ancrages de 0 à 99, 3 000 tendons au plus, fichier de 5 Mo au plus ; trames téléchargeables.
- [code] Gaine = longueur ; mètres de toron = longueur × nombre de torons. Accessoires paramétrés proposés d'office.
- [code] États : En attente de validation, Validée, En fabrication, Livrée, Reçue. Modifiables en avant comme en arrière après les deux validations.
- [décidé] Stock impacté quand la commande passe à Livrée (Livrée avant Reçue). [code] conforme : consommé = commandes livrées ; restant = stock initial + mouvements manuels moins consommé, par entité. Mouvements manuels (entrée, sortie, correction) toujours motivés.
- [code] Synthèse logistique : toron T15,7 compté à 1,18 kg par mètre.

## 8. Devis

- [code] Prix au m² = partie entière de ((Σ ancrages × 100 × torons + Σ gaines × 3 × torons) / surface + ratio actif × 12 × 1,05 + forfait) + 1 (`Devis/Domain/Calcul.cs`). Remise au m² avec motif obligatoire.
- [code] États : Brouillon, Envoyé, Expiré, Validé, Annulé, Remplacé. Seul un brouillon se modifie. Envoi : échéances à 100 % exactement. Validation : directeur, chef de projet, responsable administratif, administrateur. Annulation d'un devis validé : directeur, chef de projet, responsable administratif.
- [décidé] Relance à partir de 7 jours. [code] conforme (alerte « à relancer » 7 jours après l'envoi ; alerte d'expiration à 7 jours).
- [décidé] Expiration selon la durée de validité saisie. [code] conforme (1 à 365 jours, 30 par défaut, passage automatique à Expiré).
- [décidé] Toute modification de surface impose une nouvelle version. [décidé 2026-10-23, code] la modification de surface d'une dalle est **refusée** tant qu'un devis envoyé, expiré ou validé la chiffre sur l'ancienne surface : créer d'abord la nouvelle version (brouillon), puis modifier la surface ; le brouillon la reprend à l'enregistrement.
- [décidé] Agent commercial facultatif. [code] conforme.
- [code] Échéances par défaut : 30 % à la signature, 60 % par dalle à la livraison, 10 % de retenue de garantie à 6 mois.

## 9. Facturation et paiements

- [décidé] Factures d'acompte déduites au prorata. [code] l'acompte se saisit en TTC ; sur chaque facture de livraison, déduction = montant de la dalle × pourcentage d'acompte du devis ; total des acomptes d'un devis plafonné à 100 %.
- [code] Prix facturé : prix net au m² du devis validé le plus récent contenant la dalle. Un BL n'est facturé qu'une fois ; une dalle ne figure que sur un BL ; un BL exige que la commande usine de la dalle soit faite.
- [décidé] Paiement à moins de 90 jours. [code] conforme : échéance strictement moins de 90 jours après la date de facture.
- [décidé] Modes : virement, chèque, espèces, effet. [code] conforme ; numéro obligatoire pour chèque et effet ; échéance obligatoire pour un effet, encaissé seulement à cette date.
- [décidé] Espèces plafonnées à 5 000 DH par facture, par client et par mois. [code] contrôlé à l'émission d'une facture en espèces et à chaque encaissement en espèces, par client et mois civil, toutes entités confondues.
- [décidé] Reste dû = commandé moins encaissé. [décidé 2026-10-23, code] **commandé = somme TTC des devis validés du projet** ; reste dû du projet = commandé moins avoirs moins encaissé (`/api/factures/situation`, bloc « Situation financière » de la fiche projet). Le reste par facture (facturé moins avoirs moins encaissé) et le reste à facturer restent affichés à côté.
- [code] Statuts de facture : Impayée, Partielle, En retard, Payée, Annulée (avoir total). Trop-perçu après avoir : remboursement par virement, chèque ou espèces.
- [code] Annulation d'un encaissement, d'une dépense, d'un contrat ou d'un courrier : responsable administratif, chef de projet, directeur.

## 10. Indicateurs

- [décidé] Chiffre d'affaires calculé sur le TTC ; marge = chiffre d'affaires moins dépenses. [code] conforme : CA = TTC des factures de la période moins avoirs ; marge = CA moins dépenses (générales et de dalle) de la période.

## 11. Notifications

- [décidé] Email au directeur après réception de dalle, mise en tension, injection et chaque nouveau paiement, depuis une adresse à son domaine. [code] conforme sur les déclencheurs ; envoi à tous les comptes Directeur actifs ; l'expéditeur est `SMTP_FROM` (le domaine dépend du réglage, **À confirmer**). Un échec d'envoi ne bloque jamais l'action.

## 12. Fichiers

- [code] Suppression logique, purge du stockage un mois après. Droits de dépôt par contexte (plans : dessinateurs ; photos d'étape : images seulement, pendant l'étape ; rapports : dernier rapport seulement). Pièces figées une fois l'étape validée.

## 13. Points mis de côté

Formule poutre, tableaux de commande, tableaux de mise en tension / injection : à reprendre plus tard, rien à coder sans nouvelle décision.

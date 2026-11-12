# Règles métier de PROGIX

**Dernière mise à jour** : 2026-10-28

Sources : règles décidées (cadrage du 2026-10-22) et règles observées dans le code. Chaque règle indique où elle vit dans le code. Les écarts entre une règle décidée et le code sont listés dans [PROGRESS.md](PROGRESS.md), section « Écarts ».

Légende : **[décidé]** règle donnée par le métier ; **[code]** règle trouvée dans le code ; **[écart]** le code diffère ; **À confirmer** non tranché.

## 1. Général

- [décidé] Monnaie unique : dirham (DH). Aucune entité hors Maroc, pas de champ pays. [code] conforme : aucun champ pays ni devise.
- [code] Deux entités émettrices, `APT` et `BBR` ; chaque document porte l'entité du projet. Un projet se réaffecte à l'autre entité (directeur, chef de projet, responsable administratif) : nouveau code et renumérotation de tous ses documents, ancien numéro gardé.
- [code] TVA à 20 %.
- [code] Les documents émis ne se modifient pas et ne se suppriment pas ; ils s'annulent avec un motif obligatoire, tracé (qui, quand).
- [décidé 2026-11-11, code] Mot de passe : 8 caractères au moins, avec une minuscule et un chiffre ; ni majuscule ni symbole exigés (règle déjà en service en production, reprise dans le dépôt).
- [décidé 2026-11-11, code] Signature électronique au nom de l'utilisateur connecté, **sans ressaisie du mot de passe** (elle était exigée jusque-là), obligatoire avant d'imprimer ou d'envoyer devis, contrat, BL, facture, avoir, courrier, fiche de chantier ; un jeton aléatoire alimente le QR code de vérification publique.

### Habilitations par fonction

- [décidé 2026-11-11, code] Les accès se règlent **par fonction et par module**, depuis le paramétrage : aucun accès, lecture, ou écriture. Quinze modules : projets, devis, clients et partenaires, livraisons, facturation, trésorerie, étude, exécution, chantier, logistique, répartition des ingénieurs, performance des dessinateurs, performance des ingénieurs, niveaux des dalles, paramétrage. La trésorerie, les deux répartitions et la performance sont des modules de consultation : ils s'arrêtent à la lecture.
- [décidé 2026-11-11, code] Le réglage revient au **directeur et au chef de projet**. Les droits du directeur ne se règlent pas : il garde l'accès à tout.
- [décidé 2026-11-11, code] Des **fonctions peuvent être créées**, en plus des douze d'origine. Une fonction créée a une **fonction de référence** parmi les douze (sauf directeur), dont elle prend les validations, les signatures, le tableau de bord et la visibilité des montants. Elle se supprime tant qu'aucun utilisateur ne la porte.
- [décidé 2026-11-11, code] Les **validations du chantier restent fixes** (technique, administrative, chantier, blocage par le directeur, suppression d'un projet, signataires) : elles suivent la fonction d'origine ou la fonction de référence, pas le réglage.
- [code] Deux sortes de modules (`Progix.SharedKernel/Habilitations.cs`). **Accordables** à toute fonction : devis, clients et partenaires, livraisons, facturation, trésorerie, logistique. **Liés aux fonctions d'origine** : projets, étude, exécution, chantier, répartitions, performance, niveaux, paramétrage ; le réglage ne peut qu'y restreindre le niveau de la fonction de référence.
- [code] Sans réglage, une fonction a les niveaux d'origine, qui reproduisent les menus et les droits d'avant les habilitations. Le réglage s'applique à la requête suivante, sans reconnexion ; changer la fonction d'un utilisateur le déconnecte.
- [code] Les listes de paramètres (niveaux, types de construction, accessoires...) se lisent quel que soit le niveau : seule leur modification dépend du module. Les fichiers, le tableau de bord et les documents signés par identifiant gardent leurs propres règles.

## 2. Numérotation

- [décidé] Factures `FAC-XXXXX-AAAA` ; BL, devis et avoirs (préfixe `AV`) au même format, avec incrémentation indépendante.
- [code] Compteur en base par type, par entité et par an (`NumeroSuivantAsync`). Préfixes : `DEV` (devis, numéro commun à toutes les versions), `FAC`, `AV`, `BL`, `CTR` (contrat), `REC` (reçu de paiement), `RMB` (reçu de remboursement), `BRD` (bordereaux BCT et BET, série commune), `ATT` (attestation de conformité). L'année est celle de la date du document.
- [code] Code projet : `{ENTITE}-{AAAA}-{NNNNN}` (ex. `APT-2026-00018`), année de la date de création, non modifiable ensuite.

## 3. Clients et partenaires

- [code] Types : Client final, BET, BCT, Architecte, Laboratoire, Entreprise de construction. Natures : morale, physique, autre.
- [décidé] Types de client incluant Entreprise de Construction et Autre (sans ICE / RC / CIN exigés). [décidé 2026-10-23] « Autre » reste une **catégorie** (nature), utilisable avec tous les types : un nom suffit, aucun identifiant légal. Pas de type « Autre ».
- [décidé 2026-10-28, code] Import Excel des clients et partenaires (Clients et partenaires, bouton « Importer », rôles commerciaux) suivant une trame téléchargeable (`/api/partenaires/import/trame.xlsx`, onglet « Aide »). Aperçu ligne par ligne sans écriture, puis création des lignes valides sur confirmation. Mêmes contrôles que la fiche : ligne refusée si type, catégorie ou champs obligatoires invalides ; ICE ou CIN déjà connu (en base ou plus haut dans le fichier) : ligne ignorée ; même nom ou même téléphone : ligne créée avec un avertissement. 2 000 lignes et 5 Mo au plus.
- [code] Personne morale : ICE de 15 chiffres. [décidé 2026-10-28, code] Pour facturer : ICE et registre de commerce exigés (identifiant fiscal et patente facultatifs). Pour établir un contrat : ICE, RC, identifiant fiscal et patente exigés. Personne physique : CIN.
- [décidé] Contrôle des doublons clients obligatoire. [décidé 2026-10-23, code] ICE ou CIN identique : **bloquant**. Même nom (sans accents, casse ni ponctuation) ou même téléphone (9 derniers chiffres), toutes catégories : **avertissement** non bloquant pendant la saisie (`/api/partenaires/doublons-probables`).
- [décidé] Exonération de TVA via une case sur la fiche client. [code] conforme ; une attestation d'exonération expirée remet d'office la TVA normale ; l'attestation n'est jamais imprimée.

## 4. Projets

- [décidé] Statuts : Créé (gris, avant tout devis validé ; confirmé le 2026-10-23), En étude (mauve), En réalisation (orange / jaune), Terminé (vert). Le suivi de chantier s'ouvre à la sortie de « Créé ».
- [code] Transitions automatiques : Créé vers En étude à la validation d'un devis ; retour à Créé si plus aucun devis validé ; En réalisation dès qu'une dalle a terminé l'étape 3 ; Terminé quand toutes les dalles ont terminé l'étape 11.
- [décidé] Seul le directeur peut bloquer un projet. [code] conforme (bloquer et débloquer). Un projet bloqué suspend les étapes de chantier et la facturation.
- [code] Suppression logique et restauration : directeur ou chef de projet, motif obligatoire ; un projet supprimé est en lecture seule.
- [code] Doublon de libellé de projet : simple avertissement.
- [décidé 2026-10-28, code] Types de construction paramétrables (Paramétrage, « Projets : types de construction », directeur ou chef de projet), proposés dans une liste déroulante à la création et à la modification du projet. Code stable porté par le projet ; un type porté par un projet ne se supprime pas, il se désactive. Liste initiale : Immeuble, Villa, Radier, Dallage. Le type ne change plus le forfait (voir Devis).
- [décidé 2026-10-28, code] **Commercial du projet** : agent commercial facultatif, choisi à la création du projet et modifiable sur sa fiche. Il ne se choisit plus sur le devis : chaque devis le reprend (un brouillon suit le commercial à jour du projet ; un devis émis garde celui de son émission). Reprise faite une fois : le commercial du devis le plus récent est devenu celui du projet.

## 5. Dalles et suivi de chantier

- [décidé] Une dalle n'a pas de statut propre : on affiche son étape courante ; elle est réalisée à la fin des 11 étapes, dans un ordre strict sans exception. [code] conforme (`EtapesFaites` de 0 à 11).
- [décidé] Référence : numéro + lettre (A par défaut), la lettre s'incrémente à chaque nouvelle version des plans d'exécution. [code] `{CODE PROJET}-{NN}{Lettre}` (ex. `APT-2026-00018-01A`).
- [décidé 2026-10-28, code] Niveaux des dalles paramétrables (page « Niveaux des dalles » du menu, directeur ou chef de projet) : liste libre et ordonnée de libellés (ex. Mezzanine, Terrasse). Chaque niveau garde une valeur stable portée par les dalles ; un niveau porté par une dalle ne se supprime pas, il se désactive (« Proposé » décoché) et reste affiché sur ses dalles. Un libellé modifié s'applique aux dalles existantes ; les documents déjà émis gardent le leur. Liste initiale : du 5ème sous-sol au 40ème étage.
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
| 10 | Injection | chantier | validations technique, administrative et chantier **avant** ; fiche complète ; la validation signe la fiche au nom de l'exécutant, sans mot de passe (2026-11-11) |
| 11 | Réception finale | chantier, responsable administratif | |

- [décidé 2026-10-23, code] Qui valide : **technique = chef de projet**, **administrative = responsable administratif**, **chantier = responsable des chantiers**. Le **directeur** peut donner toutes les validations. Personne d'autre (ni ingénieur en chef, ni chef de projet pour l'administrative ou le chantier).
- [décidé 2026-10-23] Commande usine : chef de projet + responsable administratif. Mise en tension et injection : chef de projet + responsable administratif + responsable des chantiers.
- [décidé 2026-11-12, code] **Notifications de changement d'étape** (cloche de l'en-tête et notification du navigateur, **tant que PROGIX est ouvert** ; pas de notification push application fermée) :
  - **« C'est mon tour »** : une personne est prévenue quand l'étape qui devient l'étape en cours d'une dalle est réalisée par sa fonction (tableau des étapes ci-dessus), ou quand une étape attend une validation qui lui revient. Mise en tension et injection : les validateurs sont prévenus dès que l'étape est en cours, le chantier une fois les trois validations données. Commande usine : la logistique quand l'étape est en cours, les validateurs quand la commande est soumise. Une étape rouverte (déverrouillage) prévient à nouveau.
  - **Directeur et chef de projet** : seulement les validations qui leur reviennent (technique ; le directeur peut aussi donner les autres), pas chaque changement d'étape.
  - **Projets concernés** : ingénieur, dessinateur et chef de chantier ne sont prévenus que pour les projets ou les dalles où ils sont affectés ; les autres fonctions pour tous les projets. Projets supprimés ou bloqués exclus.
  - Personne n'est prévenu de sa propre action. Les avis couvrent les sept derniers jours et ne sont pas stockés : ils se déduisent de l'état des étapes, relu toutes les minutes. Chacun autorise les notifications dans son navigateur depuis la cloche.
- [décidé 2026-10-28, code] **Ingénieur de réalisation** : l'ingénieur affecté dans l'équipe du projet, un seul à la fois ; en affecter un autre clôt l'affectation du précédent (historique gardé). À distinguer de l'ingénieur du devis.
- [code] Déverrouillage : directeur ou chef de projet, dernière étape réalisée seulement, motif obligatoire, tracé. Déverrouiller l'étape 4 remet à zéro le suivi logistique.
- [décidé] Réception de dalle : photo obligatoire. [code] conforme (et photos aussi aux étapes 3, 5, 7).
- [code] Rapport non conforme : l'étape 8 reste bloquée jusqu'à un nouveau rapport conforme ; un rapport ne se modifie pas.
- [code] Avant l'injection (dalle aux étapes 8 à 9), rappel des factures non soldées du projet ; il informe, il ne bloque pas.
- [code] Avis de conformité d'une dalle réalisée (directeur, chef de projet, responsable administratif) ; seule une dalle conforme figure sur une attestation.

### Étude des dalles (menu Étude)

- [décidé 2026-11-11, code] Chaque dalle d'un projet a son **étude**, dès sa création, visible dans le menu « Étude » (directeur, chef de projet, ingénieur en chef, ingénieur). Les ingénieurs voient toutes les études.
- [décidé 2026-11-11, code] **Affectation** des ingénieurs à l'étude d'une dalle par l'ingénieur en chef, le chef de projet ou le directeur. **Plusieurs ingénieurs** peuvent porter la même étude ; un retrait garde l'historique. Sont affectables : ingénieur et ingénieur en chef. À distinguer de l'ingénieur de réalisation du projet et de l'ingénieur du devis.
- [code] État de l'étude : « À affecter » sans ingénieur, « En cours » dès qu'un ingénieur est affecté, « Terminée » à la clôture.
- [décidé 2026-11-11, code] **Clôture** par un ingénieur affecté à l'étude, sans validation supplémentaire. Les pièces sont figées et les ingénieurs ne changent plus.
- [décidé 2026-11-11, code] **Pièces** de l'étude, dans l'ordre du travail du bureau d'études : plan de coffrage reçu, fond de plan préparé pour ADAPT (calques dalle, poteaux, voiles, réservations), sortie ADAPT, plan de principe.
- [décidé 2026-11-11, code] **Génération du fond de plan** depuis le plan de coffrage déposé (DWG ou DXF), par ceux qui peuvent déposer une pièce. Trois temps, comme le travail manuel : choix du niveau, **affectation des calques aux éléments** (dalle, poteaux, voiles, réservations, ignoré), aperçu, puis dépôt comme pièce « fond de plan ». Le rôle de chaque calque est proposé d'après son nom, l'ingénieur le confirme ou le corrige : les bureaux d'études n'ont pas de convention commune.
- [décidé 2026-11-11, code] Un **calque de hachures** sans autre précision (`HACHURES`, `Hachure`) est proposé « Ignoré » : selon les plans il porte des voiles ou des remplissages non porteurs, l'ingénieur le passe en voiles au besoin. Un rectangle dessiné dans l'épaisseur d'un voile de cage est une **ouverture** qui coupe ce voile. Deux calques différents ne se fondent pas en un seul voile.
- [décidé 2026-11-11, code] Lecture du plan de coffrage : les **blocs** sont lus (poteaux et réservations posés en blocs, éléments visibles seulement) ; l'**unité** déclarée dans le fichier n'est gardée que si elle donne un bâtiment plausible, sinon elle se déduit des dimensions (dessin en mètres déclaré en millimètres) ; un calque « Ouverture voile » est proposé ignoré ; sur un calque de poteaux, une forme est un **voile** si elle est plus de quatre fois plus longue qu'épaisse, sinon un poteau rectangulaire (25 x 70 reste un poteau) ; deux voiles en équerre restent deux rectangles, seuls les morceaux alignés bout à bout se réunissent. Toutes les **réservations** trouvées sont reprises dans le fond de plan pour ADAPT (calques de réservation, fluides compris, et contours du calque de dalle).
- [décidé 2026-11-11, code] ADAPT n'impose **aucun nom de calque**. Le fond de plan produit est un DXF en mètres, le coin bas gauche de la dalle à l'origine, aux calques `ADAPT-DALLE`, `ADAPT-POTEAUX`, `ADAPT-VOILE`, `ADAPT-RESERVATION`.
- [code] Règles de détection (`Projets/Application/FondPlan.cs`) : un niveau est un contour de dalle portant au moins trois poteaux ou voiles, nommé par le titre de plan le plus proche ; sur un calque de poteaux, une forme ramassée est un poteau, une forme allongée un voile, une grande forme une cage dont l'intérieur est une réservation ; deux contours de voiles emboîtés donnent un voile par côté et étendent la dalle ; une réservation est un contour plein entièrement dans la dalle. Le résultat est une proposition : voiles et réservations sont à contrôler avant l'import dans ADAPT. La génération du plan de principe n'est pas faite.
- [code, à confirmer] Dépôt des pièces par les ingénieurs affectés à l'étude et par ceux qui affectent (ingénieur en chef, chef de projet, directeur). Réouverture d'une étude terminée par ces trois mêmes rôles. Aucune pièce n'est exigée pour clôturer.
- [code] L'étude est indépendante des 11 étapes du suivi et du statut du projet : elle peut commencer avant tout devis.

### Exécution des dalles (menu Exécution)

- [décidé 2026-11-11, code] Chaque dalle d'un projet figure dans le menu « Exécution » (directeur, chef de projet, dessinateur en chef, dessinateur). Le menu reprend le fichier de suivi « ETAT EXE » du bureau de dessin.
- [décidé 2026-11-11, code] **Affectation** des dessinateurs à une dalle par le dessinateur en chef, le chef de projet ou le directeur. **Plusieurs dessinateurs** peuvent partager une dalle ; un retrait garde l'historique. La dalle porte toujours le premier affecté et les noms de tous ; l'ancien réglage à un seul dessinateur (équipe du projet) alimente la même affectation. La page « Répartition des dessinateurs » est retirée : le menu Exécution la remplace.
- [décidé 2026-11-11, code] Un **dossier** par version des plans d'exécution : la première version est un dossier **EXE**, chaque version suivante une **MODIF**. Les MODIF s'arrêtent à la validation du BCT, qui fige les plans.
- [code] États d'une dalle : « À affecter » sans dessinateur, « À lancer » avec dessinateur et sans dossier, puis l'état de son dernier dossier : « En cours », « Terminé », « Arrêté ».
- [code, à confirmer] Un dossier s'ouvre par un dessinateur affecté à la dalle ou par un responsable, une fois le précédent terminé ou arrêté. Il se **termine** avec son plan et son modèle de calcul, au moins un dessinateur affecté et une surface de dalle renseignée ; la surface et les dessinateurs sont alors relevés sur le dossier et ses fichiers figés. Valider l'étape « Plans d'exécution » termine le dossier en cours. Arrêt (motif obligatoire), reprise et réouverture du dernier dossier : dessinateur en chef, chef de projet, directeur.
- [décidé 2026-11-11, code] Un dossier partagé compte **à parts égales** pour chacun de ses dessinateurs, dossier et surface (performance).
- [code] **Suivi par jour** : pour chaque jour, les dossiers ouverts ce jour-là (référence, projet, client, BCT, BET, niveau, surface EXE ou MODIF, dessinateurs, ingénieurs de l'étude, état, date d'envoi) et les dalles planifiées ce jour-là qui n'ont pas encore de dossier, signalées en retard une fois le jour passé. Le jour prévu se règle par les responsables.
- [décidé 2026-11-11] Pas de reprise du fichier « ETAT EXE » 2026 : le suivi démarre avec les dossiers saisis dans PROGIX.
- [décidé 2026-11-11, code] La performance se lit sur **deux pages séparées**, chacune avec son module d'habilitation : « Performance des dessinateurs » (directeur, chef de projet, dessinateur en chef) et « Performance des ingénieurs » (directeur, chef de projet, ingénieur en chef). L'état des paiements de l'année, qui figurait sur l'ancienne page unique, est en bas de la Trésorerie.
- [décidé 2026-11-11, code le 2026-11-12] **Évaluation des dessinateurs** (page « Performance des dessinateurs »), reprise de la feuille « Évaluation » du fichier d'évaluation 2026, mêmes colonnes et mêmes formules (vérifiées par des tests sur les valeurs du fichier) :
  - **Données** : dossiers EXE et MODIF **terminés dans l'année** et leurs surfaces relevées à la clôture, hors projets supprimés ; un dossier partagé compte à parts égales (voir plus haut). Pas de reprise des chiffres 2026 du fichier.
  - **Mois travaillés** dans l'année, au dixième : de la date d'arrivée (ou du 1er janvier) à la date de départ (ou à aujourd'hui, ou au 31 décembre), d'après les **dates d'arrivée et de départ de la fiche de la personne** (Paramétrage, Accès). Sans date d'arrivée, ou compte désactivé sans date de départ : la personne est listée mais pas calculée. Jours travaillés = mois × jours par mois (23).
  - **Rendement** : MODIF par jour, EXE par jour, surface MODIF et EXE par jour, dossiers par jour, m² par jour, m² par dossier.
  - **Score** = (0,8 × m² par jour ÷ médiane + 0,2 × dossiers par jour ÷ médiane) × jours de référence ÷ jours par mois ; les médianes sont celles des personnes en cours. **Niveau** : Excellent à partir de 1,25, Bon à partir de 0,90, Moyen à partir de 0,60, À renforcer en dessous. **Rang** : 1 pour le meilleur score. En dessous de 2 mois de présence : « Trop récente », un score mais pas de rang.
  - **Personnes parties** (date de départ passée ou compte désactivé) : listées à part, comparées à la médiane des personnes en cours et classées entre elles.
  - **Objectif normal** : 400 m² et 1 dossier par jour ; un rendement à l'objectif s'affiche en vert.
  - Personnes évaluées : les dessinateurs, plus le dessinateur en chef s'il a terminé un dossier dans l'année.
  - **Réglages** (jours par mois, jours de référence, seuils, mois minimum, poids, objectifs) : modifiables par le directeur et le chef de projet depuis la page ; le dessinateur en chef consulte.

### Suivi de chantier (menu Chantier)

- [décidé 2026-11-11, code] Menu « Chantier » pour le responsable des chantiers, les chefs de chantier, le chef de projet et le directeur : les dalles à suivre sur le terrain, leur étape en cours sur les 11, ce qui la retient, et les chefs de chantier de leur projet. Ouvrir une dalle ouvre son suivi, où les étapes se réalisent comme avant.
- [décidé 2026-11-11, code] Un **chef de chantier ne voit que les dalles des projets où il est affecté**, et n'ouvre le suivi et les dépenses que de ces projets. Le responsable des chantiers, le chef de projet et le directeur voient tout.
- [décidé 2026-11-11, code] L'affectation reste **par projet** : le chef de chantier affecté à un projet suit toutes ses dalles. Elle se fait par le responsable des chantiers, le chef de projet ou le directeur, depuis le menu Chantier comme depuis l'équipe du projet ; plusieurs chefs de chantier par projet, historique gardé.
- [décidé 2026-11-11, code] Le **menu Projets est fermé par défaut** au responsable des chantiers et aux chefs de chantier : ils n'ont que le tableau de bord et le menu Chantier. Il peut leur être rouvert depuis les habilitations (lecture ou écriture).
- [code] États d'une dalle dans ce menu : « À affecter » tant que son projet n'a pas de chef de chantier, « Avant chantier » jusqu'à la validation du BCT, « Commande usine » pendant l'étape 4, « À réaliser » pour les autres étapes, « Terminée » après la onzième.
- [code] Les dépenses de chantier se saisissent depuis le menu Chantier (même écran que l'onglet du projet). Les règles des étapes, des validations et des signatures ne changent pas.

## 6. Mise en tension

- [décidé] Tolérance EN 13670 : ± 5 % pour le groupe, ± 15 % pour un câble seul.
- [code] Allongement mesuré = (lecture 100 % moins lecture 25 %) côté A, plus côté B si tendu des deux côtés. Allongement théorique = force × (1 moins pertes) × longueur / (section × module), paramétrable (221 kN, 150 mm², 195 000 MPa, 5 %).
- [code depuis 2026-10-23] Câble seul entre 0,85 et 1,15 fois Ao ; groupe (somme des mesures) entre 0,95 et 1,05 fois la somme des Ao (`MesureTension`, `TensionEndpoints.Calculer`).
- [code] Hors tolérance : la validation reste possible avec une remarque d'investigation obligatoire.

## 7. Commande usine, logistique et stock

- [décidé] Validation technique et administrative de la commande usine. [code] conforme.
- [décidé 2026-11-11, code] **Commandes à créer depuis le menu Logistique** : l'onglet « Commandes usine » liste, sous l'état « À commander », les dalles où la saisie de la commande est ouverte (étape 4 en cours, commande pas encore soumise à validation, projet ni supprimé ni bloqué). Le bouton « Créer la commande » ouvre le suivi de la dalle sans passer par la fiche du projet. Qui saisit et qui valide ne change pas.
- [décidé 2026-11-11, code] **Chiffres du menu** (ce qui reste à traiter, à côté de Devis, Étude, Exécution, Chantier, Logistique). Devis : devis en brouillon et devis à créer. Étude : études non terminées. Exécution : dalles sans dossier ou au dossier en cours (un dossier arrêté ou terminé ne compte pas). Chantier : dalles dont les 11 étapes ne sont pas toutes faites. Logistique : commandes à créer. **L'ingénieur** (devis et études où il est affecté), **le dessinateur** (dalles où il est affecté) et **le chef de chantier** (dalles de ses projets) voient ce qui est à leur nom ; les autres fonctions voient le total. Les devis sans ingénieur ne comptent pas dans le chiffre d'un ingénieur.
- [décidé 2026-10-28, code] La commande usine porte une **surface totale saisie** (m², obligatoire pour l'enregistrer et pour la soumettre à validation) ; elle est imprimée sur le BL et facturée.
- [décidé] Tendons : numéro simple, orientation X / Y, système, longueur, importés depuis JSON, Excel ou CSV. [code] conforme : numéro en chiffres seulement (affiché sur deux chiffres), type d'ancrage parmi S1, S2, S3, S4, S7, S9, S12, S15 (depuis le 2026-10-28), longueur de 0 à 500 m, ancrages de 0 à 99, 3 000 tendons au plus, fichier de 5 Mo au plus ; trames téléchargeables.
- [code] Gaine = longueur ; mètres de toron = longueur × nombre de torons. Accessoires paramétrés proposés d'office.
- [code] États : En attente de validation, Validée, En fabrication, Livrée, Reçue. Modifiables en avant comme en arrière après les deux validations.
- [décidé] Stock impacté quand la commande passe à Livrée (Livrée avant Reçue). [code] conforme : consommé = commandes livrées ; restant = stock initial + mouvements manuels moins consommé, par entité. Mouvements manuels (entrée, sortie, correction) toujours motivés.
- [code] Synthèse logistique : toron T15,7 compté à 1,18 kg par mètre.

## 8. Devis

- [décidé 2026-10-28, code] **Administrateur en consultation seule** sur les devis (remplace la règle du matin limitée aux données techniques) : il voit les devis et leur PDF, mais ne crée, ne modifie, n'envoie, ne signe, ne valide, ne rejette ni ne versionne aucun devis, et n'utilise ni la trame ni l'import. Refus côté API (`DevisEtatsEndpoints.PeutEcrire`), écran en lecture seule.
- [décidé 2026-10-28, code] **Ingénieur du devis** : choisi sur chaque devis parmi les ingénieurs (ingénieur, ingénieur en chef) ; un ingénieur qui crée le devis en devient l'ingénieur ; repris par la nouvelle version.
- [décidé 2026-10-28, code] **Forfait par superficie** : grille unique (Paramétrage, « Devis : grille des forfaits », directeur, chef de projet, responsable administratif) de tranches « à partir de X m² → Y DH/m² », tous types confondus, la première à 0 m². La tranche est choisie par la **surface totale du devis** ; toutes ses dalles ont le même forfait. Grille initiale : 0 m² → 100, 10 000 m² → 85 (l'ancienne règle de l'immeuble, **à ajuster**). Un brouillon prend la grille en vigueur à chaque enregistrement ; un devis émis garde son forfait.
- [décidé 2026-10-28, code] **Fichiers ADAPT** (exports RTF d'ADAPT-Floor Pro, « PTTotal.rtf.doc » et « RebarTotal.rtf.doc ») : importés **sur la fiche de la dalle** (onglet Dalles, colonne ADAPT ; directeur, chef de projet, responsable administratif, ingénieur chef, ingénieur ; pas l'administrateur). On en déduit, sur le plan « Current plane » : ratio actif = PTTotal, tableau Summary, Rate en kg/m² ; ratio passif = RebarTotal, même colonne ; type d'ancrage = Number Of Strands (S1, S2...) ; ancrages = nombre de lignes de câbles du type ; gaine = cumul de Strand Length du type. Un type absent de la liste des systèmes refuse l'import. Surface : une dalle sans surface prend le Floor Area du fichier ; sinon la surface de la dalle est conservée et l'écart seulement signalé. Un nouveau devis reprend ces données ; un brouillon les reprend par le bouton « Reprendre les données ADAPT ». Un devis émis n'est pas touché.
- [décidé 2026-11-11, code] **Dépôt des fichiers ADAPT depuis la fiche du devis** (étape 1 « Fichiers ADAPT » d'un brouillon, mêmes rôles que l'import sur la dalle ; l'import sur la fiche de la dalle reste en place et alimente les mêmes données). Plusieurs fichiers à la fois, pour toutes les dalles ; chaque fichier est reconnu (PTTotal ou RebarTotal) et lu seul : **les deux exports d'une dalle peuvent arriver séparément**, chacun n'apporte que sa part (PTTotal : ratio actif, câbles, surface du plancher ; RebarTotal : ratio passif). **Rattachement à la dalle par la surface du plancher lue** : automatique seulement sans doute possible (une seule dalle au devis, ou une seule dalle dont la surface, ou celle de ses exports déjà lus, égale celle du fichier) ; sinon le fichier attend et la personne choisit la dalle. Deux fichiers du même genre devinés pour la même dalle dans un seul dépôt : le second attend. **RebarTotal manquant** : signalé, il n'empêche ni de générer le chiffrage ni d'envoyer le devis (le prix ne dépend que du ratio actif). « Générer le chiffrage » reprend les données lues dans le brouillon (ancien bouton « Reprendre les données ADAPT »). **Les fichiers d'origine sont conservés** avec la dalle (contexte de fichiers « adapt ») et se rouvrent depuis le tableau ; un fichier remplacé reste stocké mais n'est plus affiché. Les imports d'avant cette règle n'ont que les valeurs lues.
- [décidé 2026-10-28, code] **Création des dalles par niveaux** : on coche les niveaux présents dans le projet (avec bloc et immeuble facultatifs), une dalle est créée par niveau coché, surface à renseigner (0). La surface d'une dalle peut donc rester vide ; un devis ne peut pas être envoyé tant qu'une de ses dalles n'a pas de surface.
- [décidé 2026-10-28, code] **Trame de calcul Excel** (bouton sur la fiche du devis, rôles qui modifient les devis) : onglet « Dalles » prérempli (référence, niveau, surface, ratios et systèmes déjà saisis, S1 à S5 proposés d'office), prix au m² et montant calculés par les formules de PROGIX ; onglets « Synthèse » (projet, surface totale, forfait de la grille, totaux, TVA) et « Forfaits ». Elle se réimporte par « Importer depuis Excel » dans un brouillon ; PROGIX relit ratios et systèmes et recalcule lui-même.

- [décidé 2026-11-11, code] **Liste des devis filtrable** par ingénieur du devis (ou « Sans ingénieur »), commercial, projet et date du devis, en plus de l'état et de la recherche. **Un ingénieur** (fonction de référence ingénieur) arrive sur ses devis et ceux qui n'ont pas encore d'ingénieur ; il peut retirer le filtre pour voir les autres. C'est un filtre d'affichage, pas une restriction d'accès.
- [décidé 2026-11-12, code] **Devis à créer** : un projet non supprimé qui n'a encore **aucun devis** (quel que soit son état) figure dans la liste des devis, en tête de « Tous » et sous l'état « À créer ». « Créer le devis » ouvre le brouillon (mêmes droits que la création depuis le projet) ; un projet sans dalle renvoie à sa fiche pour en ajouter. La recherche et les filtres Projet et Commercial s'y appliquent, pas l'ingénieur ni la date. Ces devis à créer comptent dans le chiffre du menu Devis, sauf pour un ingénieur, qui ne compte que ses brouillons.
- [code] Prix au m² = partie entière de ((Σ ancrages × 100 × n + Σ gaines × 3 × n) / surface + ratio actif × 12 × 1,05 + forfait) + 1, où n est le nombre de torons du type d'ancrage (`Devis/Domain/Calcul.cs`). Remise au m² avec motif obligatoire.
- [décidé 2026-10-28, code] **Types d'ancrage** (appelés ainsi à l'écran au lieu de « nombre de torons ») : S1, S2, S3, S4, S7, S9, S12, S15 (`SystemesAncrage` du noyau partagé). Seuls ces types se choisissent sur un devis, dans sa trame et son import, et dans la commande usine (saisie et import des tendons). Un type déjà enregistré sur un devis ou une commande reste accepté.
- [code] États : Brouillon, Envoyé, Expiré, Validé, **Rejeté** (anciennement « Annulé », code `annule` inchangé), Remplacé. Seul un brouillon se modifie. Envoi : échéances à 100 % exactement. Signature et validation : directeur, chef de projet, responsable administratif.
- [décidé 2026-10-28, code] **Rejet** (motif obligatoire, quel que soit l'état : brouillon, envoyé, expiré, validé) : seulement le responsable administratif, le chef de projet ou le directeur.
- [décidé 2026-10-28, code] **Impression du brouillon** : un devis en brouillon s'imprime avant d'être marqué envoyé, pour relecture. Le PDF porte « DEVIS (BROUILLON) » et la mention « document de travail non signé, sans valeur contractuelle », sans bloc de signature ni QR code ; il reprend la version enregistrée. Un devis envoyé doit toujours être signé avant d'être imprimé.
- [décidé 2026-10-28, code] Le PDF du devis n'affiche **ni ratio actif ni ratio passif** (ni en colonne, ni dans les conditions d'offre) : ils servent au chiffrage interne.
- [décidé] Relance à partir de 7 jours. [code] conforme (alerte « à relancer » 7 jours après l'envoi ; alerte d'expiration à 7 jours).
- [décidé] Expiration selon la durée de validité saisie. [code] conforme (1 à 365 jours, 30 par défaut, passage automatique à Expiré).
- [décidé] Toute modification de surface impose une nouvelle version. [décidé 2026-10-23, code] la modification de surface d'une dalle est **refusée** tant qu'un devis envoyé, expiré ou validé la chiffre sur l'ancienne surface : créer d'abord la nouvelle version (brouillon), puis modifier la surface ; le brouillon la reprend à l'enregistrement.
- [décidé 2026-10-28, code] La surface se modifie **depuis la fiche des dalles ou depuis la grille d'un devis en brouillon**. Dans les deux cas elle s'enregistre sur la dalle, seule source de la surface, avec le même refus si un autre devis émis la chiffre. Un devis n'écrit sur la dalle que la surface qu'on y a changée : une surface laissée telle quelle reprend celle de la dalle.
- [décidé] Agent commercial facultatif. [code] conforme, porté par le projet depuis le 2026-10-28.
- [code] Échéances par défaut : 30 % à la signature, 60 % par dalle à la livraison, 10 % de retenue de garantie à 6 mois.

## 9. Facturation et paiements

- [décidé 2026-10-28, code] **Une facture se crée uniquement à partir d'un bon de livraison.** Plus de facture d'acompte : le paiement à la signature se saisit comme un paiement sur les dalles, sans facture. Les factures d'acompte déjà émises restent consultables et leur acompte reste déduit au prorata des factures de livraison qui suivent (montant de la dalle × pourcentage d'acompte du devis).
- [décidé 2026-10-28, code] **Les paiements suivent les dalles, pas les factures.** Un paiement se saisit sur le projet (onglet « Devis et contrats », Paiements par dalle, ou trésorerie après choix du projet) et se **ventile à la main** entre ses dalles ; un seul reçu REC par paiement, avec le détail par dalle. Le montant affecté à une dalle ne dépasse pas son reste à payer (effets en attente déduits). Saisie : rôles commerciaux (directeur, chef de projet, responsable administratif, administrateur).
- [décidé 2026-10-28, code] **Taux de paiement d'une dalle** = payé ÷ montant TTC de la dalle au devis validé le plus récent qui la contient (prix net au m² × surface de la dalle, TVA du devis). Affiché à côté de chaque dalle (onglet Dalles, colonne Paiement, et Paiements par dalle) **uniquement pour les rôles qui voient les chiffres financiers** : directeur, chef de projet, responsable administratif, administrateur, ingénieur en chef, ingénieur. Une dalle hors de tout devis validé n'a pas de taux. Les effets non échus ne comptent pas dans le payé.
- [décidé 2026-10-28, code] **Statut des factures déduit des dalles** : les paiements d'une dalle sont imputés aux parts de cette dalle sur ses factures (part TTC au prorata du montant de chaque ligne), les factures les plus anciennes d'abord ; le surplus reste au crédit de la dalle (avance) et s'impute à la facture suivante. Impayée, Partielle, En retard, Payée se calculent sur ce réglé imputé.
- [décidé 2026-10-28, code] **Trop-perçu remboursé sur la dalle** : possible quand le payé d'une dalle dépasse son montant TTC moins les avoirs partiels qui la concernent ; reçu RMB comme avant. Un avoir total ne réduit pas le dû de la dalle : il annule la facture et rend son BL facturable à nouveau.
- [décidé 2026-10-28, code] **Reçu de paiement imprimable** (bouton « Imprimer le reçu » sur chaque règlement, ouvert d'office après la saisie), par les rôles qui voient les chiffres financiers. Il reprend le client et le projet figés à l'encaissement et la répartition par dalle.
- [décidé 2026-10-28, code] Reprise des paiements saisis avant ce changement : chacun est réparti entre les dalles de sa facture au prorata de leurs montants (facture d'acompte : au prorata des dalles de son devis) ; les reçus gardent leur numéro.
- [code] Prix facturé : prix net au m² du devis validé le plus récent contenant la dalle. Un BL n'est facturé qu'une fois ; une dalle ne figure que sur un BL ; un BL exige que la commande usine de la dalle soit faite. [décidé 2026-10-28, code] **Surface du BL et de la facture = surface totale saisie sur la commande usine** (une commande d'avant cette règle garde la surface de la dalle).
- [décidé] Paiement à moins de 90 jours. [code] conforme : échéance strictement moins de 90 jours après la date de facture.
- [décidé] Modes : virement, chèque, espèces, effet. [code] conforme ; numéro obligatoire pour chèque et effet ; échéance obligatoire pour un effet, encaissé seulement à cette date.
- [décidé] Espèces plafonnées à 5 000 DH par facture, par client et par mois. [code] contrôlé à l'émission d'une facture en espèces et à chaque encaissement en espèces, par client et mois civil, toutes entités confondues.
- [décidé] Reste dû = commandé moins encaissé. [décidé 2026-10-23, code] **commandé = somme TTC des devis validés du projet** ; reste dû du projet = commandé moins avoirs moins encaissé (`/api/factures/situation`, bloc « Situation financière » de la fiche projet). L'encaissé est celui des paiements du projet (toutes dalles). Le reste par facture (facturé moins avoirs moins réglé imputé) et le reste à facturer restent affichés à côté.
- [code] Statuts de facture : Impayée, Partielle, En retard, Payée, Annulée (avoir total), calculés sur le réglé imputé depuis les dalles. Trop-perçu : remboursement sur la dalle par virement, chèque ou espèces.
- [code] Annulation d'un encaissement, d'une dépense, d'un contrat ou d'un courrier : responsable administratif, chef de projet, directeur.

## 10. Indicateurs

- [décidé] Chiffre d'affaires calculé sur le TTC ; marge = chiffre d'affaires moins dépenses. [code] conforme : CA = TTC des factures de la période moins avoirs ; marge = CA moins dépenses (générales et de dalle) de la période.

## 11. Notifications

- [décidé] Email au directeur après réception de dalle, mise en tension, injection et chaque nouveau paiement, depuis une adresse à son domaine. [code] conforme sur les déclencheurs ; envoi à tous les comptes Directeur actifs ; l'expéditeur est `SMTP_FROM` (le domaine dépend du réglage, **À confirmer**). Un échec d'envoi ne bloque jamais l'action.

## 12. Fichiers

- [code] Suppression logique, purge du stockage un mois après. Droits de dépôt par contexte (plans : dessinateurs ; photos d'étape : images seulement, pendant l'étape ; rapports : dernier rapport seulement). Pièces figées une fois l'étape validée.

## 13. Points mis de côté

Formule poutre, tableaux de commande, tableaux de mise en tension / injection : à reprendre plus tard, rien à coder sans nouvelle décision.

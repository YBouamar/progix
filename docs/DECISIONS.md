# Décisions

Format : date / décision / raison. Les dates sont celles où la décision devient visible (cadrage, première migration, commit). Une date inconnue est notée « À confirmer ».

| Date | Décision | Raison |
|---|---|---|
| À confirmer | Abandon de l'ancienne version Next.js / Supabase / Vercel ; refonte complète dans ce dépôt. | Donnée du cadrage du 2026-10-22. Raison détaillée **À confirmer**. |
| 2026-10-04 (première migration) | Backend ASP.NET Core 8 en monolithe modulaire : un processus, six modules, un DbContext et un schéma PostgreSQL par module. | Stack imposée ; isolement des domaines sans coût de microservices. |
| 2026-10-04 | Communication entre modules par interfaces du SharedKernel (`Lookup`, contrats), jamais par les tables d'un autre module. | Garder les modules découplés. |
| 2026-10-04 | Migrations EF Core appliquées au démarrage de l'API, avec reprise si la base n'est pas prête. | Déploiement simple en Docker, sans étape manuelle. |
| 2026-10-04 | Authentification ASP.NET Identity, JWT dans un cookie HttpOnly posé par l'API. | Le front ne manipule pas le jeton. |
| 2026-10-04 (au plus tard) | Dirham uniquement, aucune entité hors Maroc, aucun champ pays. | Activité exclusivement marocaine (cadrage). |
| 2026-10-05 | Deux entités émettrices (APT, BBR), numérotation par entité et par an, renumérotation à la réaffectation. | Deux sociétés juridiques émettent les documents. |
| 2026-10-05 | Documents émis immuables ; correction par avoir ou annulation motivée. | Exigence comptable et traçabilité. |
| 2026-10-06 | Signature électronique par mot de passe + QR code de vérification publique. | Authentifier les documents sans prestataire externe. |
| 2026-10-06 | Les 11 étapes de chantier en ordre strict, double validation pour commande usine, mise en tension et injection ; validation du responsable chantier en plus pour la mise en tension (migration `ValidationChantier`). | Règle métier. |
| 2026-10-06 | PWA en lecture seule hors ligne, aucune saisie mise en file d'attente, données locales effacées à la déconnexion. | Usage tablette sur chantier, sans risque de conflit de saisie. |
| 2026-10-06 | Dépôt git initialisé (commit initial `fba84bb`), remote GitHub `YBouamar/progix`. | Historique et revue. |
| 2026-10-12 | Courriels aux directeurs via MailKit, mis en file Hangfire ; sans SMTP réglé, rien n'est envoyé ; un échec ne bloque pas l'action. | Informer la direction sans fragiliser les saisies. |
| 2026-10-12 | Stock consommé au passage de la commande à Livrée ; articles de câbles visibles dès la commande. | Règle métier (Livrée avant Reçue). |
| 2026-10-12 | `docker-compose.override.yml` pour le développement à chaud (dotnet watch, next dev), sans effet en production. | Confort de développement. |
| 2026-10-22 | Mémoire de projet : CLAUDE.md + docs/PROJECT, ARCHITECTURE, BUSINESS_RULES, DECISIONS, PROGRESS, NEXT_STEPS et commande `/handoff`. TODO.md et docs/AVANCEMENT.md deviennent historiques. | Reprendre le contexte à chaque session sans réexplication. |
| 2026-10-23 | Mise en tension : tolérance EN 13670, ± 15 % par câble seul, ± 5 % pour le groupe (remplace 0,95 à 1,10 pour les deux). | Norme applicable (choix métier). |
| 2026-10-23 | Reste dû du projet = TTC des devis validés moins avoirs moins encaissé. | « Commandé » = devis validé TTC (choix métier). |
| 2026-10-23 | Surface d'une dalle chiffrée dans un devis envoyé, expiré ou validé : modification refusée tant qu'une nouvelle version n'est pas créée. | Règle « toute modification de surface impose une nouvelle version » (choix : bloquer plutôt qu'avertir). |
| 2026-10-23 | Doublons clients : ICE / CIN bloquants ; même nom ou même téléphone en avertissement, toutes catégories. | Contrôle obligatoire sans empêcher les homonymes légitimes. |
| 2026-10-23 | « Autre » reste une catégorie (nature) et non un type ; le statut « Créé » est conservé. | Confirmé par le métier, pas de changement de code. |
| 2026-10-23 | Validations : technique = chef de projet, administrative = responsable administratif, chantier = responsable des chantiers, le directeur peut toutes les donner. L'injection exige aussi la validation chantier (migration de reprise `ValidationChantierInjection`). | Règle métier donnée par l'utilisateur. |
| 2026-10-23 | Tests xUnit des règles de domaine (`backend/tests/Progix.Tests`, sans base de données) et intégration continue GitHub Actions. | Filet de sécurité avant les évolutions et le déploiement. |
| 2026-10-23 | Dépendances : MailKit 4.18.1, Newtonsoft.Json 13.0.3 et System.Security.Cryptography.Pkcs (10.0.13 dans l'API, 8.0.2 dans Facturation) épinglés ; Next.js 15.5.28. | Vulnérabilités connues, dont une exécution de code à distance dans Next.js 15.5.27. PostCSS embarqué par Next laissé en l'état (correction seulement en Next 16, risque limité à du CSS non maîtrisé). |
| 2026-10-23 | Build backend sur ce poste via le conteneur `mcr.microsoft.com/dotnet/sdk:8.0`, sans installer le SDK. | Pas de SDK .NET 8 sur le poste Windows. |
| 2026-10-22 | Cibles imposées non encore réalisées : application mobile React Native + Expo, déploiement sur VPS Contabo (`progix.bacybrains.com`). | Cadrage. |

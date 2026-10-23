---
description: Fin de session PROGIX : met à jour la mémoire de projet (PROGRESS, NEXT_STEPS, DECISIONS) sans toucher au code
---

Tu clôtures une session de travail sur PROGIX. **Ne modifie aucun fichier de code** (backend, web, scripts, Docker, migrations). Tu ne touches qu'aux fichiers de `docs/` listés ci-dessous. Tout en français, sans tiret long.

## 1. Recueillir ce qui s'est passé

- Relis la conversation de la session : tâches faites, décisions prises par l'utilisateur, problèmes rencontrés.
- Lance `git status` et `git log --oneline -15` (et `git diff --stat` si des changements ne sont pas commités) pour voir ce qui a réellement changé.
- Ne compte comme « vérifié » que ce qui a été exécuté pendant la session (build, lint, test, usage à l'écran), avec le résultat obtenu.

## 2. Réécrire `docs/PROGRESS.md`

Réécris le fichier entier (pas d'ajout en bas), en gardant ses sections : Terminé, En cours / à reprendre, Pas commencé, Bloquants, Écarts entre règles décidées et code, Bugs connus. Mets la date du jour. Retire ce qui est résolu, ajoute les nouveaux écarts et bugs constatés. Si le code contredit une règle de `docs/BUSINESS_RULES.md`, signale l'écart ici sans trancher.

## 3. Mettre à jour `docs/NEXT_STEPS.md`

Retire les étapes terminées, ajuste les priorités, ajoute les nouvelles étapes. Chaque étape doit être assez précise pour démarrer une session : fichiers concernés, critère de fin.

## 4. Mettre à jour `docs/DECISIONS.md`

Ajoute une ligne date / décision / raison pour chaque décision prise pendant la session (choix technique, règle métier tranchée par l'utilisateur). N'invente pas de raison : écris « À confirmer » si elle n'a pas été donnée. Si une règle métier a été tranchée, mets aussi à jour `docs/BUSINESS_RULES.md`.

## 5. Proposer, sans appliquer, des ajouts à `CLAUDE.md`

Si la session a révélé une commande, une convention ou un piège utile à toutes les sessions, propose le texte exact à ajouter. N'écris pas dans `CLAUDE.md` sans accord explicite. Garde-le sous 150 lignes.

## 6. Résumé final

Donne en quelques lignes : fichiers mis à jour, ce qui a avancé, ce qui reste bloquant, la prochaine étape recommandée.

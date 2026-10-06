#!/bin/bash
# Sauvegarde quotidienne de PROGIX : la base de données et les fichiers déposés.
# Usage : ./deploiement/sauvegarde.sh            (depuis la racine du projet, sur le serveur)
# Les archives vont dans ~/sauvegardes-progix ; celles de plus de 14 jours sont effacées.
# Copiez-les aussi hors du serveur : une sauvegarde restée sur la même machine ne protège pas d'une panne du serveur.
set -euo pipefail

cd "$(dirname "$0")/.."
DEST="${SAUVEGARDES:-$HOME/sauvegardes-progix}"
JOUR="$(date +%Y-%m-%d_%H%M)"
COMPOSE="docker compose -f docker-compose.prod.yml --env-file .env"
mkdir -p "$DEST"

# Base de données : export complet, compressé.
$COMPOSE exec -T postgres pg_dump -U progix -d progix --format=custom > "$DEST/progix-base-$JOUR.dump"

# Fichiers déposés (plans, photos, rapports) : copie du volume de stockage.
docker run --rm -v progix_miniodata:/data:ro -v "$DEST":/sauvegarde alpine \
  tar czf "/sauvegarde/progix-fichiers-$JOUR.tar.gz" -C /data .

find "$DEST" -name 'progix-*' -mtime +14 -delete
echo "Sauvegarde terminée : $DEST/progix-base-$JOUR.dump et progix-fichiers-$JOUR.tar.gz"

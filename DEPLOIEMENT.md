# Déployer PROGIX sur un serveur (VPS)

Ce guide installe PROGIX sur un VPS sous Ubuntu ou Debian, avec HTTPS automatique.
Compter 30 à 45 minutes. Les commandes se tapent dans un terminal.

## Ce qu'il faut avant de commencer

- Le VPS : son adresse IP et le mot de passe `root` (dans l'espace client de l'hébergeur). 8 Go de mémoire conseillés, 4 Go au minimum.
- Un nom de domaine ou un sous-domaine, par exemple `progix.votre-domaine.ma`. Sans domaine, pas de HTTPS : à éviter pour un usage réel.

## 1. Faire pointer le domaine vers le serveur

Chez le gestionnaire de votre domaine, créer un enregistrement DNS :

| Type | Nom | Valeur |
|---|---|---|
| A | progix | l'adresse IP du VPS |

La prise en compte demande de quelques minutes à quelques heures. Vérification : `ping progix.votre-domaine.ma` doit répondre avec l'IP du VPS.

## 2. Préparer le serveur

Se connecter :

```
ssh root@ADRESSE_IP
```

Mettre à jour, installer Docker et le pare-feu :

```
apt update && apt upgrade -y
curl -fsSL https://get.docker.com | sh
apt install -y ufw rsync
ufw allow OpenSSH
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable
```

Seuls SSH (22), HTTP (80) et HTTPS (443) restent ouverts.

## 3. Copier PROGIX sur le serveur

Depuis votre ordinateur (pas depuis le serveur), dans un nouveau terminal :

```
cd ~/projects/progix
rsync -az --delete \
  --exclude node_modules --exclude .next --exclude bin --exclude obj \
  --exclude _to_delete --exclude .env \
  ./ root@ADRESSE_IP:/opt/progix/
```

La même commande sert ensuite à envoyer chaque nouvelle version. Le fichier `.env` du serveur n'est jamais écrasé.

## 4. Régler les secrets

Sur le serveur :

```
cd /opt/progix
cp deploiement/env.production.exemple .env
nano .env
```

À remplir :

- `SITE_ADDRESS` et `PUBLIC_URL` : votre domaine.
- `POSTGRES_PASSWORD`, `MINIO_ROOT_PASSWORD`, `JWT_KEY` : trois valeurs différentes, générées chacune avec `openssl rand -hex 32`.
- `ADMIN_EMAIL` et `ADMIN_PASSWORD` : le premier compte directeur.

Puis protéger le fichier : `chmod 600 .env`.

Attention : `POSTGRES_PASSWORD` et `MINIO_ROOT_PASSWORD` sont fixés au premier démarrage. Les changer ensuite dans `.env` ne change pas ceux de la base et du stockage.

## 5. Démarrer

```
cd /opt/progix
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

La première construction prend 5 à 10 minutes. Suivre le démarrage :

```
docker compose -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml logs -f api
```

Ouvrir ensuite `https://progix.votre-domaine.ma` et se connecter avec l'identifiant `directeur` et le mot de passe choisi.

## 6. Premiers réglages dans PROGIX

1. Paramétrage, Entités : identité légale et logo de chaque société.
2. Paramétrage, Utilisateurs : créer les comptes de l'équipe. Aucun compte de démonstration n'existe en production.
3. Vérifier un PDF (un devis signé, par exemple) et le dépôt d'un fichier.

## 7. Sauvegardes

Tester une sauvegarde à la main :

```
cd /opt/progix && ./deploiement/sauvegarde.sh
```

La programmer chaque nuit à 2 h :

```
(crontab -l 2>/dev/null; echo "0 2 * * * /opt/progix/deploiement/sauvegarde.sh >> /var/log/progix-sauvegarde.log 2>&1") | crontab -
```

Les archives sont gardées 14 jours dans `/root/sauvegardes-progix`. Elles sont sur le même serveur : rapatriez-les régulièrement ailleurs, par exemple depuis votre ordinateur :

```
rsync -az root@ADRESSE_IP:/root/sauvegardes-progix/ ~/sauvegardes-progix/
```

### Restaurer

```
cd /opt/progix
C="docker compose -f docker-compose.prod.yml --env-file .env"
$C stop api web
$C exec -T postgres pg_restore -U progix -d progix --clean --if-exists < /root/sauvegardes-progix/progix-base-DATE.dump
docker run --rm -v progix_miniodata:/data -v /root/sauvegardes-progix:/sauvegarde alpine \
  sh -c "rm -rf /data/* && tar xzf /sauvegarde/progix-fichiers-DATE.tar.gz -C /data"
$C up -d
```

## 8. Mettre à jour PROGIX

1. Sur le serveur : `./deploiement/sauvegarde.sh`.
2. Depuis votre ordinateur : la commande `rsync` de l'étape 3.
3. Sur le serveur : `docker compose -f docker-compose.prod.yml --env-file .env up -d --build`.

Les évolutions de la base s'appliquent toutes seules au démarrage.

## Ce qui change par rapport à votre poste

- HTTPS partout, certificat obtenu et renouvelé automatiquement.
- Base de données, stockage et API fermés à Internet : seul le site est exposé.
- Pas de comptes de démonstration, pas de documentation d'API publique.
- Cinq mots de passe faux de suite bloquent un compte quelques minutes.

## En cas de souci

| Symptôme | À regarder |
|---|---|
| Le site ne répond pas | `docker compose -f docker-compose.prod.yml ps` puis `logs caddy` |
| « Certificat » en erreur | Le DNS ne pointe pas encore vers le serveur, ou les ports 80 et 443 sont fermés |
| Connexion refusée après identifiant correct | `PUBLIC_URL` doit commencer par `https://` et correspondre au domaine |
| Erreur à l'ouverture d'un PDF ou d'un fichier | `logs api` |

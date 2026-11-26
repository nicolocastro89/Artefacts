# Installing Artefacts on TrueNAS SCALE

TrueNAS SCALE 24.10 (Electric Eel) or later runs apps with Docker, and installs a Compose file as a *Custom App*. [`compose.yml`](compose.yml) is that file. It runs three containers:

| Container | What it does | Port |
|---|---|---|
| `app` | The catalogue: web pages, REST API and GraphQL, from the image `artefacts:<version>`, built on your computer and loaded into TrueNAS | 30080 |
| `imgproxy` | Resizes pictures on request, reading the originals from the media folder. Browsers load pictures from it directly | 30081 |
| `postgres` | The database | (internal) |

Pictures are stored as plain files in a dataset (no object storage), so TrueNAS snapshots and replication cover them like any other files.

> **No sign-in yet.** Anyone who can reach ports 30080 and 30081 can read and change the whole catalogue, purchase prices included. Keep it on the local network or a VPN. Do not forward these ports to the internet.

## 1. Build the image and load it into TrueNAS

The image never goes through a registry. Build it on any computer with Docker, from the repository root, and save it as a file:

```sh
docker build -t artefacts:0.1.0 .
docker save artefacts:0.1.0 -o artefacts-0.1.0.tar
```

Copy the file to the NAS: through an SMB share, or with `scp artefacts-0.1.0.tar admin@truenas.local:/mnt/tank/apps/artefacts/`. Then load it from the TrueNAS shell (**System > Shell**, or SSH):

```sh
sudo docker load -i /mnt/tank/apps/artefacts/artefacts-0.1.0.tar
```

`compose.yml` uses `pull_policy: never`, so TrueNAS takes the loaded image and never looks for it online. If the tag in the file doesn't match a loaded image, the install fails with "No such image". TrueNAS runs on x86-64, which is what a normal PC or Intel Mac builds. On an Apple Silicon Mac, add `--platform linux/amd64` to the build.

## 2. Create the datasets

Create these under a pool (the file assumes `tank`), with the **Apps** dataset preset:

- `tank/apps/artefacts/postgres`: the database
- `tank/apps/artefacts/media`: original pictures and documents

You don't need to set owners by hand. Postgres sets up its own folder on first start, and the short-lived `media-permissions` container gives the media folder to the app's user (UID 1654) every time the app starts.

If your pool or paths differ, change the four host paths in `compose.yml` (one for postgres, three for media).

## 3. Fill in the settings

At the top of `compose.yml`, under `x-settings`:

| Setting | Value |
|---|---|
| `db-password` | Any long random string |
| `imgproxy-key`, `imgproxy-salt` | Two different 64-character hex strings. Run `openssl rand -hex 32` twice, from the TrueNAS shell or any computer |
| `image-url` | The address browsers use to reach imgproxy, for example `http://truenas.local:30081` or `http://192.168.1.20:30081` |

Each value is written once and reused through YAML anchors. The app refuses to start while the key or salt still reads `CHANGE-ME`.

Keep a copy of the filled-in file somewhere safe. The database password protects existing data, so changing it later means changing it inside Postgres too.

## 4. Install

**Apps > Discover Apps > ⋮ > Install via YAML**. Name the app `artefacts`, paste the edited file and save. The first start pulls the images and creates the database, which takes a minute or two. Once all containers are healthy, open `http://<nas>:30080`.

The database schema is migrated automatically when the app starts.

## Updating

Build and load the new version under a new tag (for example `artefacts:0.2.0`, as in step 1). Then **Apps > artefacts > Edit**, change the `image:` line to the new tag and save. The app restarts on the new image and migrates the database as it starts. The **Update** button doesn't apply, because it looks for newer images in a registry.

Keep the previous image until the new version has run for a while. Going back means changing the tag back, but only if the update didn't migrate the database: restore the dump taken before the update in that case. Remove old images with `sudo docker image rm artefacts:<old version>`.

## Backups

Back up two things:

- **Media**: snapshot and replicate `tank/apps/artefacts/media` like any dataset.
- **Database**: a dataset snapshot of `postgres` is crash-consistent and usually restorable, but a dump is the dependable copy. Save this as a script, for example `/mnt/tank/apps/artefacts/backup.sh`, and run it from **System > Advanced > Cron Jobs** (as root, daily):

  ```sh
  #!/bin/sh
  set -e
  dir=/mnt/tank/apps/artefacts/backups
  mkdir -p "$dir"
  container=$(docker ps --format '{{.Names}}' | grep artefacts | grep postgres | head -n1)
  docker exec "$container" pg_dump -U artefacts -Fc artefacts > "$dir/artefacts-$(date +%F).dump"
  find "$dir" -name 'artefacts-*.dump' -mtime +30 -delete
  ```

  To restore into an empty database: `docker exec -i <postgres container> pg_restore -U artefacts -d artefacts --clean --if-exists < artefacts-YYYY-MM-DD.dump`.

## Behind a reverse proxy

If you put a proxy such as Nginx Proxy Manager, Caddy or Traefik in front, route both ports, set `image-url` to the proxied picture address, and raise the request body limit: one upload can be up to 2 GB (TIFF masters), and each file up to 200 MB.

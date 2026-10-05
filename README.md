# Personal Finance

A self-hosted personal finance tracker: bank and cash accounts, credit cards with billing cycles, installment purchases, creditors, shared expenses with other people, subscriptions and incomes, with reports on top. It runs on your own machine, and your data never leaves it.

## Requirements

Git and Docker (with Compose v2).

## Quick start

```sh
git clone https://github.com/InakiPoch/personal-finance-app
cd personal-finance-app
docker compose up -d
docker compose logs -f
```

The first run builds the app from source. When the log shows `Personal Finance is ready at http://localhost:8080`, press `Ctrl+C` to leave the log view and open that URL.

## Your data

- Everything lives in the Docker volume `personal-finance_pf-data`.
- The port is bound to `127.0.0.1` only. The app has no authentication, so anyone who can reach the port can read and change your finances. Do not expose it to a network.

## Stop / start

```sh
docker compose down
docker compose up -d
```

## Backup / restore

Stop the app first. The database runs in WAL mode, and copying it while it is running can produce a corrupt backup.

```sh
docker compose stop
docker run --rm -v personal-finance_pf-data:/data -v "$PWD":/backup alpine tar czf /backup/pf-backup-$(date +%F).tgz -C /data .
docker compose start
```

Restore into an empty volume (replace the date with your backup's):

```sh
docker compose down -v
docker compose up --no-start
docker run --rm -v personal-finance_pf-data:/data -v "$PWD":/backup alpine \
  sh -c 'tar xzf /backup/pf-backup-YYYY-MM-DD.tgz -C /data && chown -R 1654:1654 /data'
docker compose up -d
```

`1654` is the non-root `app` user inside the .NET image.

## Reset

```sh
docker compose down -v
```

> **Warning:** `-v` deletes the data volume. All your data is gone unless you made a backup first.

## Upgrade

```sh
git pull
docker compose up -d --build
```

Without `--build` the old image keeps running. Database migrations run automatically on startup. **Back up first** (see above): a migration that is interrupted halfway cannot always be recovered.

## HTTPS (optional)

The app generates a self-signed certificate on first start and keeps it in the data volume, so browsers warn until you trust it.

1. In `compose.yaml`, uncomment the three HTTPS lines: the `8443` port mapping, `Https__Enabled` and the `https://localhost:8443` `App__PublicUrl`. Comment out the `http://localhost:8080` `App__PublicUrl` line above it so only one is active.
2. `docker compose up -d`. Port 8080 now redirects to `https://localhost:8443`.
3. Copy the certificate out and trust it:

   ```sh
   docker compose cp app:/data/https/personal-finance.crt .
   ```

   - **Chrome / Brave / Chromium on Linux** (verified): needs `certutil` (package `nss-tools` or `libnss3-tools`):
     ```sh
     certutil -d sql:$HOME/.pki/nssdb -A -t "P,," -n personal-finance -i personal-finance.crt
     ```
     Restart the browser. Use `-t "P,,"` (trusted peer); `-t "C,,"` does not work for this certificate.
   - **Firefox, macOS, Windows, system-wide trust on Linux:** not tested. Import `personal-finance.crt` into your OS or browser certificate store as a trusted certificate.

   The certificate is regenerated if you reset the volume (`down -v`), so trust it again after a reset.

## Troubleshooting

- **Logs:** `docker compose logs app`.
- **`500 Internal Server Error` / "check if the server supports the requested API version" on Windows or macOS:** Docker Desktop is outdated or its engine is not running yet. Update Docker Desktop, restart it, wait until the engine is running, and retry. On Windows, `wsl --update` can also help.
- **Port 8080 is in use:** change the left side of the port mapping in `compose.yaml` (for example `127.0.0.1:9090:8080`) and set `App__PublicUrl` to the same port.
- **The container is "Up" but the page never loads and the log stops during migrations:** a stale migration lock from an interrupted start (only on images built before this was fixed; the current image clears it automatically). Back up, then:
  ```sh
  docker compose stop
  docker run --rm -v personal-finance_pf-data:/data alpine sh -c 'apk add -q sqlite && sqlite3 /data/personalfinance.db "delete from __EFMigrationsLock"'
  docker compose up -d
  ```

## Contributing

`main` is generated for each release. Development happens on `dev`, and pull requests target `dev`.

## License

[MIT](LICENSE)

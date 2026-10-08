# 0014 — Localhost-only HTTP by default, opt-in self-signed HTTPS, migrate on startup

**Status**: accepted (2026-10-02/04, API Phases 57, 59)

**Decision**:
- The compose port binds to `127.0.0.1` only; finance data must not reach the LAN.
- HTTP is the default; HTTPS is opt-in with a .NET-generated self-signed ECDSA P-256 leaf stored in `/data/https/` (no HSTS, no PFX password) — a trusted cert cannot be installed from a container.
- Migrations run on startup, sequentially, before hosted services, gated by `Database:MigrateOnStartup` (off in `appsettings.json`, on in the image). Forward-only; no downgrades.

**Rejected**: HTTPS by default; a local CA; a wrapper start script (would break plain `docker compose up`); manual `dotnet ef database update` for users.

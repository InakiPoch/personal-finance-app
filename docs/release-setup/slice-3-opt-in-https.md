# Slice 3 — Opt-in HTTPS

Read [00-overview.md](00-overview.md) first. Requires [Slice 1](slice-1-runnable-container.md) and [Slice 2](slice-2-smoke-and-ci.md).

## Goal

A user can switch the app to HTTPS by uncommenting two lines in `compose.yaml`, with **no certificate work on their side**: the app generates a self-signed certificate on first boot and stores it in the data volume. Default stays plain HTTP.

## Why opt-in, and why self-signed (decision Q2b/Q8)

- The owner asked whether certificates could be managed "blindly". Answer recorded here so nobody re-litigates it: **not fully**. A browser trusts a cert only if its issuer is in the *host's* trust store; a container cannot write there. Shipping a publicly-trusted cert would mean publishing its private key (insecure, gets revoked). So the options are a self-signed cert (one browser warning) or a user-performed trust step.
- `http://localhost` is already a "potentially trustworthy" origin (W3C Secure Contexts), and the port is loopback-only, so HTTPS buys almost no security here. Hence: HTTP default, HTTPS groundwork available.

## Design

Config (env vars via compose):
- `Https__Enabled=true` turns it on. Default `false`.
- Cert files live in the volume: `/data/https/personal-finance.pfx` (key + cert, used by Kestrel) and `/data/https/personal-finance.crt` (public cert only, for the user to trust).

Startup behaviour when enabled (host code, `Program.cs` + a `Helpers/SelfSignedCertificateHelper.cs`):
1. If the PFX exists and is valid for > 30 more days → load it.
2. Else generate one with .NET's built-in `System.Security.Cryptography.X509Certificates.CertificateRequest` (no openssl — the chiseled image has no shell or tools): RSA 2048 or ECDSA P-256, subject `CN=localhost`, SAN `DNS:localhost` + `IP:127.0.0.1`, EKU server auth, validity 397 days. Export PFX + CRT to `/data/https/`. Log `Generated self-signed HTTPS certificate (expires …)`.
3. Configure Kestrel endpoints explicitly: HTTP `8080` **and** HTTPS `8443` with that cert. (When endpoints are configured in code, `ASPNETCORE_HTTP_PORTS` is overridden — expected; only do this when HTTPS is enabled.)
4. `UseHttpsRedirection()` only when enabled, with `HttpsPort = 8443`. When disabled, remove the call (fixes the Slice 1 warning).
5. Ready log line uses the HTTPS URL when enabled.

Deliberately **not** done:
- **No HSTS.** HSTS on `localhost` would force HTTPS on *every* app the user runs on localhost, for months. Never enable it here.
- No password-protected PFX (it sits in the same volume as the unencrypted DB; a password adds nothing).
- No automatic trust-store installation (impossible from the container).

`compose.yaml` additions (commented by default):
```yaml
    ports:
      - "127.0.0.1:8080:8080"
      # - "127.0.0.1:8443:8443"   # HTTPS (also set Https__Enabled below)
    environment:
      App__PublicUrl: "http://localhost:8080"
      # Https__Enabled: "true"
      # App__PublicUrl: "https://localhost:8443"
```

Trust step (documented in README in Slice 4, written here so it can be tested now):
```sh
docker compose cp app:/data/https/personal-finance.crt .
# Linux (system): sudo trust anchor personal-finance.crt   (Arch/Fedora)  |  Debian/Ubuntu: copy to /usr/local/share/ca-certificates/ + update-ca-certificates
# Chrome/Brave on Linux use NSS: certutil -d sql:$HOME/.pki/nssdb -A -t "P,," -n personal-finance -i personal-finance.crt   # "P" (trusted peer), NOT "C": NSS rejects our CA:FALSE leaf as a trusted CA
# Firefox: Settings → Certificates → Import
# macOS: Keychain Access → import → Always Trust
# Windows: certmgr.msc → Trusted Root Certification Authorities → Import
```
Verify which of these actually make a *self-signed leaf* trusted in Brave on this machine; if browsers reject a leaf-as-root, switch the helper to generate a tiny local CA + leaf (still pure .NET) — record the decision in Findings.

## Steps

1. Implement the helper + conditional Kestrel/redirect wiring.
2. Unit test the helper (generates a cert with the right SAN/EKU/validity; reuses an existing valid one; regenerates when < 30 days left) in `PersonalFinance.Api.Tests`, using a temp directory.
3. Extend `scripts/smoke.sh` only via env: `BASE_URL=https://localhost:8443 CURL_OPTS=-k` must pass against an HTTPS-enabled compose (use a throwaway copy of `compose.yaml` with the lines uncommented, or `docker compose -f compose.yaml -f <tmp-override>` inside the test — do not commit an override file).

## Done when

- [x] Default compose: behaviour identical to Slice 1; no HTTPS redirect warning in logs.
- [x] HTTPS enabled: `smoke.sh` passes with `BASE_URL=https://localhost:8443 CURL_OPTS=-k`; `http://localhost:8080` redirects to 8443.
- [x] Restart keeps the same cert (fingerprint unchanged across `down`/`up`); `down -v` generates a new one.
- [x] After the trust step, Brave opens `https://localhost:8443` with no warning.
- [x] Helper unit tests green; full API suite green.
- [x] TASK.md ledger line.

## Findings

- **Decision: keep the self-signed leaf; no local CA needed.**
- **Trust gotcha:** NSS `-t "C,,"` (trusted CA) does NOT work for our `CA:FALSE` leaf (`certutil -V` says "issuer is not recognized", Brave still warns). `-t "P,,"` (trusted peer) works: `certutil -V` is valid and Brave opens `https://localhost:8443` with no warning (confirmed on this CachyOS machine). curl/openssl accept the leaf via `--cacert`.
- **Untested:** only the Linux Chrome/Brave NSS path was tried; system trust (`trust anchor`), Firefox, macOS and Windows commands are unverified.
- Cert `notBefore` is backdated ~5 min for clock skew. The PFX loads with default key-storage flags (no fix needed).
- **Verification (docker, project `pf-https-test`):** default compose smoke OK with no https-port warning; HTTPS smoke OK via `BASE_URL=https://localhost:8443 CURL_OPTS=-k bash scripts/smoke.sh <compose-dir>` against a throwaway uncommented compose copy (`smoke.sh` unchanged); 8080 returns 307 to `https://localhost:8443`; fingerprint stable across `down`/`up`, new after `down -v`; `docker compose cp app:/data/https/personal-finance.crt .` works on the chiseled image.
- **Tests:** 5 new helper tests (SAN/EKU/validity, public-only PEM crt, reuse, regenerate <30d, reuse at 31d); API 570 -> 575 green. No migration, no client change.

## Out of scope

README wording (Slice 4). Making HTTPS the default (rejected, Q8).

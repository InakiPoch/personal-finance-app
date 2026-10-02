# Slice 0 — Prerequisites

Read [00-overview.md](00-overview.md) first.

## Goal

Remove the blockers that would otherwise surface mid-slice: no Docker on the dev machine, and an unknown history that becomes public in Slice 6. No application code changes.

## Why this is first

- Every later slice is tested by running `docker compose up` locally. Docker is not installed on this machine (CachyOS/Arch).
- The owner chose to make the **same repo** public (Q12), which publishes `dev` and all 374+ commits of history. If a secret or real personal financial data is in history, the fix is a history rewrite — far cheaper to discover now than after `main` has been generated from that history.

## Steps

### 1. Install Docker (Arch / CachyOS)

```fish
sudo pacman -S --needed docker docker-compose docker-buildx
sudo systemctl enable --now docker.service
sudo usermod -aG docker $USER   # then log out/in (or reboot) for group membership
```

Notes:
- `docker-compose` on Arch provides the **v2 plugin** (`docker compose …`), which is what the README will document. Do not use the legacy `docker-compose` binary syntax in any doc.
- The owner's "install via brew" rule is for CLI utilities; Docker Engine needs the system package + systemd service.

### 2. Secrets + personal-data scan of the full history

```fish
sudo pacman -S --needed gitleaks     # or: brew install gitleaks
cd ~/Documents/PersonalFinanceApp
gitleaks git --log-opts="--all" --report-path /tmp/gitleaks.json -v
```

Also check for things gitleaks does not catch:
- Any DB ever committed: `git log --all --diff-filter=A --name-only --format= | rg -i '\.db($|-)|\.bak$'`
- Real personal data in docs/fixtures (real bank/creditor names, real amounts, real people in Parties examples): skim `docs/`, `app/*/docs/`, `*.http`, and test fixtures with `rg -i` for names the owner recognises as real.
- Emails in commit metadata: `git log --all --format='%ae%n%ce' | sort -u` — the owner should be fine with every address listed being public (GitHub noreply addresses are ideal).

Triage outcome, recorded in this doc under "Findings":
- **Nothing found** → proceed.
- **Real secret** → rotate it first, then rewrite history (`git filter-repo`) *before* Slice 5 ever runs. Coordinate: rewriting `dev` rewrites every open branch.
- **Personal data in docs** → either rewrite history or accept; owner decides.

### 3. Confirm the work branch

`chore/deploy-groundwork` is checked out, branched off `dev`. Confirm it is up to date with `origin/dev` (`git fetch && git log --oneline origin/dev..HEAD`) before Slice 1.

## Done when

- [ ] `docker run --rm hello-world` works **without sudo**.
- [ ] `docker compose version` and `docker buildx version` print versions.
- [ ] gitleaks report reviewed; findings (or "none") written below.
- [ ] Email/personal-data review done; owner signed off on public history.

## Findings

2026-10-02:
- Docker 29.8.2, Compose 5.6.0, Buildx 0.37.2; `hello-world` runs without sudo.
- gitleaks over `--all` history: no findings (`[]`).
- No `*.db*` / `*.bak` ever committed.
- Commit emails: GitHub noreply addresses + `inakipoch106@gmail.com` (341/376 commits). Owner accepted it being public; no history rewrite. (A rewrite would not remove it anyway: GitHub keeps `refs/pull/*/head` pointing at the old commits.)

## Out of scope

Anything in the app. GitHub settings (Slice 5/6).

# Main Branch Release Strategy — Review and Recommendation

## Scope

This memo reviews the proposed `03-implementation-plan.md` for turning `main` into the public release branch of the PersonalFinance project.

The review is based primarily on:

- API `PRD.md`
- API `DESIGN.md`
- Client `PRD.md`
- Client `DESIGN.md`
- Client `SYSTEM.md`
- `03-implementation-plan.md`
- `council-transcript-2026-10-02.md`

The clarified deployment requirement is:

> `main` is the release branch. A user should be able to clone the repository from `main`, start from an empty database, run a single command such as `docker compose up`, and open the application. The branch should contain only what is needed to consume the release, not development documentation, test projects, agent files, or other development-only material.

This clarification materially strengthens the case for the proposed generated `main` model.

---

## Executive assessment

The core direction is sound:

- `dev` is the engineering/source branch.
- releases originate from a tagged commit on `dev`.
- a release image is built and published.
- `main` is generated from that release and acts as a user-facing distribution branch.
- users consume the already-built image through Docker Compose.
- persistent state lives outside the container in a Docker volume.
- a fresh installation starts with an empty database and migrations create the schema.

The runtime/container side of the plan is well aligned with the application's actual architecture.

The release automation side still needs revision before implementation.

The most important conclusion is:

> The generated `main` branch is justified by the actual product requirement. The problem is not the branch model itself; the remaining weaknesses are in release sequencing, GitHub default-branch behavior, bootstrap of GHCR visibility, provenance, and acceptance testing.

---

# 1. Reasoning summary

This is a summary of the decision rationale rather than a private step-by-step chain of thought.

The evaluation was performed in three layers.

### 1.1 Does the deployment model fit the application architecture?

Yes.

The API is explicitly designed as a modular monolith running in a single process over one SQLite database. Background work such as Outbox processing and scheduled accrual runs in that same application process.

That makes a single deployable container a natural fit.

The client is an Angular application whose production API URL is already relative (`/v1`), so serving the Angular output from the ASP.NET host gives a simple same-origin deployment.

The API also already exposes `/health`, which is appropriate for Docker and CI smoke testing.

### 1.2 Does the plan satisfy the desired user experience?

Mostly yes.

The intended user contract is:

```text
git clone <repo>
cd personal-finance
docker compose up
```

That is compatible with the current plan, provided that `main` references an already-built public image instead of depending on local compilation.

The persistent Docker volume gives the correct first-run and restart semantics:

```text
first run
    -> no DB exists
    -> migrations create schema
    -> app starts empty

restart
    -> same volume reused
    -> existing data preserved

docker compose down -v
    -> volume removed
    -> next run starts empty again
```

### 1.3 Is the release automation internally coherent?

Not yet.

The main issues are:

1. the original plan publishes `main` before the release image has passed the anonymous pull check;
2. first-time GHCR visibility creates a bootstrap problem;
3. the role of the GitHub default branch is not explicitly defined;
4. scheduled workflows and Dependabot behavior may conflict with the rule that `.github` never exists on `main`;
5. Release Please ownership of tag/release creation is not fully specified;
6. provenance between a generated `main` commit and its source tag/SHA is underspecified;
7. the smoke test is too weak if it checks only `/health`.

These are repairable without abandoning the overall model.

---

# 2. What is strongly backed by the project sources

## 2.1 Single-container deployment

The API design describes:

- one process;
- one SQLite file;
- one real writer;
- Outbox processing;
- schedulers/background services;
- modular isolation inside the process.

That strongly supports a single application container.

This is a better fit than splitting API, workers, and client into several runtime services for this project.

## 2.2 Same-origin Angular + API

The client design already has a production environment seam and the repository verification confirmed that production uses:

```text
/v1
```

The API design already includes:

- CORS support for development;
- OpenAPI;
- consistent error responses;
- `/health`.

Therefore the deployment plan's proposal to serve the Angular production build from ASP.NET is coherent.

Development can continue using:

```text
Angular dev server -> http://localhost:5000/v1
```

while production becomes:

```text
browser -> same host
        -> /
        -> /v1/*
```

## 2.3 SQLite persistence

The application is explicitly designed around SQLite.

Putting the database under a Docker volume such as:

```text
/data/personalfinance.db
```

is consistent with the architecture.

The container image should contain schema/migrations, not user data.

## 2.4 Startup ordering

The sources establish that the API contains background components that interact with the database.

Therefore the deployment plan is right to require database migrations to complete before the Outbox worker and scheduler begin normal operation.

The exact implementation mechanism is a deployment decision rather than something explicitly prescribed by the PRD, but the need itself is technically justified.

## 2.5 `/health`

The API design explicitly exposes `/health` and ties health reporting to the Outbox health check.

Using it in Docker and CI smoke tests is directly supported by the design.

---

# 3. What is not directly dictated by the PRDs/DESIGNs

Several parts of the implementation plan are reasonable, but they are release-policy decisions rather than requirements derived from the application architecture.

These include:

- GHCR as the registry;
- Release Please;
- GitHub branch rulesets;
- a generated projection from `dev` to `main`;
- SHA-pinned GitHub Actions;
- attestations/SBOM/provenance;
- removal of tests and development documentation from `main`;
- automatic version stamping in `compose.yaml`;
- use of a special release identity to write to `main`.

These decisions should be documented as:

> repository and release architecture

rather than presented as if the product PRD itself required them.

That distinction matters because these choices should be judged against the public-release requirement and GitHub platform behavior, not against the accounting/domain design.

---

# 4. The clarified requirement changes the evaluation of generated `main`

Before the deployment requirement was clarified, a generated `main` branch appeared potentially over-engineered.

With the clarified requirement, it is justified.

The intended branch responsibilities should be made explicit.

## `dev`

`dev` is the complete engineering repository.

It contains:

```text
application source
tests
specs
architecture documentation
implementation documentation
agent/tooling files
development CI
release workflows
compose development override
solution/project development structure
```

## `main`

`main` is the public release projection.

It should contain only material useful for understanding or running the released application.

Conceptually:

```text
main
├── app/
│   ├── api/src/...
│   └── client/src/...
├── compose.yaml
├── Dockerfile                # optional for consumers, useful for transparency
├── README.md
├── LICENSE
├── .env.example             # only if genuinely needed
└── minimal GitHub metadata  # only where default-branch behavior requires it
```

It should not contain:

```text
tests/
*.spec.ts
internal implementation docs
.claude/
CLAUDE.md
TASK.md
development compose overrides
development CI
temporary databases
```

The important conceptual point is:

> `main` is not the development source of truth. The tagged commit on `dev` is the source of truth. `main` is a generated release artifact represented as Git.

---

# 5. One design point I would keep challenging

The project should deliberately choose between two forms of release branch.

## Option A — Release branch with production source

```text
main
├── production source
├── Dockerfile
├── compose.yaml
├── README.md
└── LICENSE
```

This keeps the project easy to inspect from the default GitHub landing page.

This is my preferred option for this project.

## Option B — Pure distribution branch

```text
main
├── compose.yaml
├── README.md
├── LICENSE
└── .env.example
```

The container image contains the application and users never build from the repository.

This is simpler operationally, but weakens the repository as a portfolio/open-source source tree because users landing on `main` would no longer see the implementation.

Given the nature of this project, Option A is the better compromise.

---

# 6. Recommended Docker Compose contract

The release branch should consume a released image.

It should not silently build the current checkout when the intended release image is unavailable.

Recommended `main` behavior:

```yaml
services:
  app:
    image: ghcr.io/inakipoch/personal-finance:X.Y.Z
    ports:
      - "127.0.0.1:8080:8080"
    restart: unless-stopped
    volumes:
      - pf-data:/data

volumes:
  pf-data:
```

Development can add an override:

```yaml
services:
  app:
    build:
      context: .
    pull_policy: build
```

This gives a clean invariant:

```text
main -> consume a release
dev  -> build current source
```

If the public release image does not exist, `main` should fail rather than quietly compile something else.

That failure is useful because it exposes a broken release instead of masking it.

---

# 7. Where I agree with the council

## 7.1 The five major phases are sensible

The broad progression is reasonable:

```text
container
-> repository hygiene
-> CI
-> release pipeline
-> ongoing maintenance
```

The council is correct that the overall decomposition does not need to be discarded.

## 7.2 GHCR visibility is a real first-release problem

The original sequence places `publish-main` before `smoke-main`.

That is backwards.

If the image is:

- private;
- missing;
- incorrectly tagged;
- broken for one architecture;

then the release branch may already have been updated to reference it.

The image must be validated before `main` is published.

## 7.3 The plan contains unresolved executable placeholders

The council is right that these should be resolved before implementing the release pipeline:

- Angular production output path;
- exact API host `.csproj`;
- .NET 10 chiseled runtime tag;
- solution-file policy;
- release version output wiring;
- release committer identity;
- branch-ruleset bypass identity.

These are not theoretical concerns; they directly affect whether the workflow can execute.

## 7.4 Deleting the `.sln` from `main` is cleaner than partially rewriting it

If the release branch removes development/test projects, keeping a solution that references missing projects is misleading.

If `main` is a release projection, no solution file is preferable to a broken one.

---

# 8. Where I would modify the council recommendation

## 8.1 Slice 0 should be broader than only a bypass spike

The council proposes starting with a protected-branch bypass experiment.

That is useful, but the first step should be a short **deployment contract**.

The project should first record:

```text
main = default public release branch
dev = engineering/source branch
releases originate from tagged commits on dev
main is generated automatically
humans never author commits directly on main
compose on main pulls the released image
a new installation starts from an empty persistent volume
```

After those invariants are fixed, test the bypass identity.

## 8.2 First GHCR publication should be bootstrap work

I would avoid making "the first release behaves differently" part of the normal release design.

Instead:

```text
bootstrap once:
    publish disposable/bootstrap image
    make GHCR package public
    verify anonymous pull
```

After that, every real release should follow the same automated path.

## 8.3 The council underemphasized default-branch behavior

If `main` is the user-facing branch, it should probably also be the GitHub default branch.

That improves the public experience:

```text
git clone <repo>
```

naturally checks out the release branch.

But this creates an important constraint:

some GitHub features operate from the default branch.

Therefore the rule:

```text
main contains no .github/workflows
```

should not be absolute.

The correct rule is closer to:

> `main` contains no development workflows, but may contain the minimal GitHub configuration needed for release maintenance or default-branch platform behavior.

The branch should be clean for users, not artificially empty of operational metadata.

A hidden `.github` directory does not harm the `git clone && docker compose up` experience.

---

# 9. Recommended release pipeline

The release process should have two distinct validation gates.

## Gate A — Is the release artifact valid?

```text
release-please / release decision
        ↓
tag vX.Y.Z on dev
        ↓
full build + tests
        ↓
build multi-architecture image
        ↓
push immutable image
        ↓
verify anonymous pull
        ↓
run image smoke test
```

Only after this passes should `main` be updated.

## Gate B — Is the distribution branch valid?

```text
take exact tagged dev commit
        ↓
create clean projection
        ↓
remove development-only files
        ↓
delete .sln if appropriate
        ↓
stamp compose.yaml with X.Y.Z
        ↓
record source tag + SHA
        ↓
run leak guard
        ↓
publish generated main
        ↓
fresh checkout / clone of main
        ↓
docker compose up
        ↓
distribution smoke test
```

This separates two questions:

```text
Does the image work?

Can a stranger consume the release exactly as documented?
```

Both should be tested.

---

# 10. Provenance should be explicit

A generated release branch changes the source tree.

Therefore the relationship between:

```text
dev tag
container image
generated main commit
```

should be recorded explicitly.

At minimum:

```text
release version: vX.Y.Z
source tag: vX.Y.Z
source SHA: abcdef...
container digest: sha256:...
```

The generated `main` commit message could contain:

```text
Release vX.Y.Z

Source: <dev SHA>
Image: ghcr.io/...:X.Y.Z
Digest: sha256:...
```

The image should also carry OCI labels for:

```text
version
revision
source
```

This makes the generated branch auditable rather than merely procedural.

---

# 11. The smoke test should imitate a real user

Checking only `/health` is insufficient.

The deployment changes several things simultaneously:

- ASP.NET hosting;
- Angular static files;
- SPA fallback routing;
- API routing;
- SQLite creation;
- migrations;
- Docker volume persistence;
- public registry access.

The release smoke should verify all of them.

Recommended clean-run acceptance sequence:

```bash
git clone <repo>
cd personal-finance
docker compose up -d
```

No:

- registry login;
- .NET SDK;
- Node.js;
- pnpm;
- local database;
- development configuration.

Then verify:

```text
GET /health
    -> service healthy

GET /
    -> Angular application

GET /<known-angular-deep-route>
    -> Angular index/fallback works

GET /v1/instruments
    -> successful empty response
```

Then create a small piece of test data and run:

```bash
docker compose down
docker compose up -d
```

Verify the data still exists.

Finally:

```bash
docker compose down -v
docker compose up -d
```

Verify the application is empty again.

That directly proves the user-facing release promise.

---

# 12. Database and migration contract

The release image should never contain a user database.

Expected behavior:

```text
/data/personalfinance.db missing
        ↓
application creates/opens DB
        ↓
migrations run
        ↓
background services start
        ↓
application becomes available
```

For an upgrade:

```text
existing volume from release N
        ↓
release N+1 container starts
        ↓
pending migrations run
        ↓
existing data remains valid
        ↓
application starts
```

The acceptance suite should test at least one real N -> N+1 upgrade with a populated database.

---

# 13. Backup and restore needs stronger treatment

The plan correctly mentions backup documentation, but "document backup" is weaker than necessary for SQLite in WAL mode.

The release contract should specify a safe backup procedure.

It should also test restore.

A backup that has never been restored is not a verified backup strategy.

The README should document:

```text
backup
restore
reset
upgrade
volume location/semantics
```

and CI or a release acceptance procedure should verify the backup/restore path at least periodically.

---

# 14. Recommended revised slices

## Slice 0 — Deployment contract and platform spike

Record:

- `main` is the default release branch.
- `dev` is the engineering branch.
- releases originate from tags on `dev`.
- humans cannot directly write `main`.
- `main` is generated.
- Compose on `main` pulls a release image.
- new installs use an empty volume.
- decide exact GitHub default-branch automation requirements.
- prove the ruleset bypass mechanism.
- confirm release identity strategy.
- bootstrap GHCR public visibility.

## Slice 1 — Runtime container

Implement:

- Angular production build;
- ASP.NET static hosting;
- SPA fallback;
- startup migrations;
- persistent `/data`;
- root Dockerfile;
- production Compose;
- dev Compose override.

Verify:

- application root;
- deep Angular route;
- API endpoint;
- `/health`;
- persistence;
- reset;
- migration interruption/recovery.

## Slice 2 — Release-branch hygiene

Implement:

- production README;
- LICENSE;
- `.gitattributes` / projection rules;
- branch rulesets;
- release-only file allow/deny policy;
- minimal default-branch GitHub metadata where required.

## Slice 3 — CI

Implement:

- SHA-pinned actions;
- existing build/test checks;
- Docker smoke;
- migration/model consistency checks;
- upgrade test.

## Slice 4 — Release pipeline

Order:

```text
release decision
-> verify
-> build image
-> publish image
-> anonymous pull
-> image smoke
-> generate main
-> leak guard
-> push main
-> fresh-main smoke
-> release finalization
```

## Slice 5 — Maintenance

Add:

- dependency maintenance;
- base-image rebuilds;
- vulnerability scanning;
- scheduled checks.

But ensure the implementation respects whichever branch is configured as GitHub's default branch.

---

# 15. Recommended acceptance criteria

A release should not be considered valid until all of the following are true.

### Clone and run

```text
git clone <repo>
cd <repo>
docker compose up -d
```

works on a machine with Docker and no development toolchain.

### Empty first run

The first installation contains no user/application data.

### Web application

`/` serves the Angular client.

### SPA routing

A direct request to an Angular route loads correctly.

### API

At least one stable list endpoint returns successfully.

### Health

`/health` reports acceptable health.

### Persistence

`docker compose down` followed by `up` preserves data.

### Reset

`docker compose down -v` followed by `up` produces a clean installation.

### Upgrade

A populated release N volume upgrades to N+1 without data loss.

### Public artifact

The pinned GHCR image can be pulled without authentication.

### Architecture

The release image is available for the intended CPU architectures.

### Provenance

The generated `main` commit, release tag, source SHA, and image digest are traceable to each other.

### Hygiene

`main` contains no development-only tests, specs, internal docs, temporary databases, or agent files.

### Protection

Normal human pushes to `main` are rejected.

---

# 16. Final recommendation

Keep the generated `main` architecture.

The clarified product requirement makes it appropriate.

The branch model should be formalized as:

```text
dev
    complete engineering repository

tag on dev
    immutable release source

container image
    executable release artifact

main
    generated user-facing release projection
```

The main change I recommend is not architectural replacement, but stronger release invariants.

In particular:

1. define `main` explicitly as the default release/distribution branch;
2. make `dev` the only human-authored integration branch;
3. make `main` consume a released image rather than building implicitly;
4. validate the image before publishing `main`;
5. validate `main` afterward from a fresh unauthenticated clone;
6. resolve GitHub default-branch automation requirements instead of banning `.github` categorically;
7. establish explicit provenance between tag, source SHA, image digest, and generated commit;
8. treat backup/restore and N -> N+1 migration as part of the release contract.

---

# Conclusion

The proposed implementation plan is directionally correct and substantially supported by the application architecture.

The council was right to preserve the five-stage structure and right to identify the release-pipeline gaps.

The key clarification is that `main` is intentionally **not** a normal development branch. It is the release.

Under that requirement, removing tests, implementation documentation, agent files, and other development-only artifacts is coherent rather than cosmetic.

The release experience should be treated as a product feature with one hard invariant:

> A user with only Git and Docker can clone the default branch, run `docker compose up`, and receive a clean, working, persistent PersonalFinance installation corresponding to one known release.

Once the release pipeline enforces that invariant before every update to `main`, the branch strategy becomes both technically defensible and operationally clear.

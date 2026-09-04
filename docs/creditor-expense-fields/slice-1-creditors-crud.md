# Slice 1 — Creditors CRUD (new table + management view)

> **Read this whole file before touching code.** It is written so a fresh session with zero prior context can execute this slice end to end. It is one of two slices; this one is **independent** and must be shipped and tested green **before** Slice 2 (`slice-2-load-expense-integration.md`).

---

## 1. Why this exists (context & intent)

We are expanding expense creation so a purchase can optionally record **who the user is going to pay** (a *Creditor*) and **which of that creditor's accounts to send money to** (*Account to Pay*).

The real-world driver: the user sometimes buys things on **another person's** credit card. Later that person says *"don't pay me at that bank — pay me at this other account."* Today the app cannot record who fronted a purchase or where to settle it.

**This slice builds only the Creditor entity and its management view** — a self-contained CRUD vertical modeled directly on the existing **Instruments** feature. It does **not** touch the Load Expense form; that is Slice 2. Shipping this first means Slice 2 has real creditors to pick from.

### Locked design decisions (do not re-litigate — these were settled with the user)

| # | Decision | Answer |
|---|----------|--------|
| Q2 | What "account to pay" refers to | The **creditor's own accounts** — NOT the user's instruments |
| Q3 | Creditor lifecycle | **Add + list only** (mirror Instruments; no edit, no delete) |
| C-1 | Creditor account fields | `label` (free text, e.g. "Galicia") + `identifier` (free text: CBU / CVU / alias) |
| C-2 | Creation flow | A creditor is created **together with its accounts** in one submit (dynamic rows). No adding accounts later. |

**Mental model:** a `Creditor` is a person you pay; a `CreditorAccount` is one of *their* destination accounts. A creditor may have 0..N accounts. Instruments (the user's own wallet) are a completely separate concept — do not conflate them.

---

## 2. Domain model

Everything lives in the existing **Financing** module (co-located with `CreditCard` and `PaymentPlan`), so there are no cross-module calls.

- **`Creditor`** aggregate root (`AggregateRoot<Guid>`): `Id`, `Name`, owns a collection of `CreditorAccount`.
- **`CreditorAccount`** child entity: `Id`, `CreditorId` (FK), `Label` (string), `Identifier` (string).

Tables (SQLite, one shared file, Financing has its own EF migrations history table):
- `financing_creditors` — `Id` (TEXT/Guid PK, not generated), `Name` (TEXT, required)
- `financing_creditor_accounts` — `Id` (TEXT/Guid PK), `CreditorId` (TEXT/Guid FK → cascade delete), `Label` (TEXT), `Identifier` (TEXT)

---

## 3. API — copy the CreditCard vertical

The **CreditCard** feature is the exact template. Read each template file, then create the Creditor equivalent alongside it. All paths are relative to `app/api/`.

### 3.1 Domain
- **Template:** `src/Modules/Financing/PersonalFinance.Financing/Domain/CreditCard.cs`
- **Create:** `Domain/Creditor.cs` and `Domain/CreditorAccount.cs`.
  - `Creditor`: private ctor + static `Create(Guid id, string name, IReadOnlyList<(string Label, string Identifier)> accounts)` returning `Result<Creditor>`. Validate: name non-empty/trimmed. Accounts are optional (0+); each account gets a `Guid.CreateVersion7()` id. Expose `IReadOnlyList<CreditorAccount> Accounts`.
  - Follow the CreditCard style exactly: `internal sealed class`, `Result<T>` factory, `builder.Ignore(DomainEvents)` handled in config.

### 3.2 Contracts (`PersonalFinance.Financing.Contracts`)
- **Command template:** `Commands/CreateCreditCardCommand.cs`
  - `Commands/CreateCreditorCommand.cs`:
    ```csharp
    public sealed record CreateCreditorCommand(string Name, IReadOnlyList<CreditorAccountPayload> Accounts) : ICommand<Guid>;
    public sealed record CreditorAccountPayload(string Label, string Identifier);
    ```
- **Query template:** `Queries/ListCreditCardsQuery.cs`
  - `Queries/ListCreditorsQuery.cs`:
    ```csharp
    public sealed record CreditorAccountRow(Guid Id, string Label, string Identifier);
    public sealed record CreditorRow(Guid Id, string Name, IReadOnlyList<CreditorAccountRow> Accounts);
    public sealed record ListCreditorsResponse(IReadOnlyList<CreditorRow> Rows);
    public sealed record ListCreditorsQuery() : IQuery<ListCreditorsResponse>;
    ```
- **Facade:** add to `IFinancingApi.cs`:
  `Task<Result<Guid>> CreateCreditorAsync(CreateCreditorCommand command, CancellationToken ct = default);`
  `Task<ListCreditorsResponse> ListCreditorsAsync(ListCreditorsQuery query, CancellationToken ct = default);`

### 3.3 Application handlers
- **Templates:** `Application/Commands/CreateCreditCard/CreateCreditCardHandler.cs`, `Application/Queries/ListCreditCards/ListCreditCardsHandler.cs`
- `Application/Commands/CreateCreditor/CreateCreditorHandler.cs` — validate, build the aggregate via `Creditor.Create(...)`, `context.Creditors.Add(creditor)`, `SaveChangesAsync`. **No Ledger calls** (unlike CreditCard, which creates ledger accounts — Creditor does NOT).
- `Application/Queries/ListCreditors/ListCreditorsHandler.cs` — `context.Creditors.Include(c => c.Accounts)` (or the equivalent owned/nav load), materialize, then order by `Name` client-side (`StringComparer.OrdinalIgnoreCase`) exactly like `ListCreditCardsHandler` (SQLite ordering caveat).

### 3.4 Persistence
- **Config template:** `Infrastructure/Persistence/Configurations/CreditCardConfiguration.cs`
- `Configurations/CreditorConfiguration.cs` — `ToTable("financing_creditors")`, key not generated, `Name` required, `Ignore(DomainEvents)`, `HasMany(c => c.Accounts).WithOne().HasForeignKey("CreditorId").OnDelete(DeleteBehavior.Cascade)` (mirror how `PaymentPlanConfiguration` maps `Installments`).
- `Configurations/CreditorAccountConfiguration.cs` — `ToTable("financing_creditor_accounts")`, `Label` + `Identifier` required.
- **DbContext:** `Infrastructure/Persistence/FinancingDbContext.cs` — add `public DbSet<Creditor> Creditors => Set<Creditor>();`. Configurations auto-discover via `ApplyConfigurationsFromAssembly`.

### 3.5 Migration
Run from `app/api/`:
```bash
dotnet ef migrations add AddCreditors \
  --project src/Modules/Financing/PersonalFinance.Financing \
  --startup-project src/Modules/Financing/PersonalFinance.Financing \
  --context FinancingDbContext
dotnet ef database update \
  --project src/Modules/Financing/PersonalFinance.Financing \
  --startup-project src/Modules/Financing/PersonalFinance.Financing \
  --context FinancingDbContext
```
Connection string resolves via `SqliteConnectionStringHelper` (`ConnectionStrings__PersonalFinanceDb` env override; otherwise `personalfinance.db` at solution root).

### 3.6 Host endpoints (`src/Bootstrap/PersonalFinance.Api`)
Model on the Instruments endpoints: `Endpoints/InstrumentsEndpoints.cs` (POST), `Endpoints/GetInstruments.cs` (GET), `Endpoints/EndpointExtensions.cs` (`MapInstrumentsEndpoints`), `Endpoints/ApiRoutes.cs`.
- DTOs (`Endpoints/DTOs/`): `CreateCreditorDTO` = `(string Name, IReadOnlyList<CreateCreditorAccountDto> Accounts)`, `CreateCreditorAccountDto = (string Label, string Identifier)`, `CreditorResultDTO = (Guid CreditorId)`, `CreditorListDTO = (IReadOnlyList<CreditorRowDto> Rows)` with nested account rows.
- `Endpoints/Creditors/PostCreditor.cs` + `Endpoints/Creditors/GetCreditors.cs` — static `Handle(...)`, dispatch through `IFinancingApi` (or the command/query bus, matching how PostPaymentPlan does it). Return `Results<Created<CreditorResultDTO>, ProblemHttpResult>` / `Ok<CreditorListDTO>`.
- Mapping: `Endpoints/Mapping/CreditorMappingExtensions.cs` — DTO → command, response → list DTO.
- Routes: add `Creditors = "/creditors"` under a suitable group in `ApiRoutes.cs`; add `MapCreditorEndpoints()` in `EndpointExtensions.cs` (`POST/GET /v1/creditors`) and call it from where the other groups are mapped.
- Register the two handlers in the Financing module registration and add the facade methods to `FinancingApi`.

**Response envelope:** the client expects `{ "rows": [...] }` (see `RowsEnvelope` on the client). Ensure `GET /v1/creditors` returns `{ rows: [...] }`.

---

## 4. Client — copy the Instruments view

The **Instruments** feature is the template. All paths relative to `app/client/src/app/`.

### 4.1 New feature folder `features/creditors/`
- **Template:** `features/instruments/` (whole folder).
- `creditors-service.ts` — copy `instruments-service.ts`:
  ```ts
  list(): Observable<Creditor[]> {
    return this.http.get<RowsEnvelope<Creditor>>('creditors').pipe(map(e => e.rows));
  }
  create(body: CreateCreditor): Observable<CreditorCreated> {
    return this.http.post<CreditorCreated>('creditors', body);
  }
  ```
- `types/creditor.ts` — `{ id: string; name: string; accounts: CreditorAccount[] }`; `CreditorAccount = { id: string; label: string; identifier: string }`.
- `types/create-creditor.ts` — `{ name: string; accounts: { label: string; identifier: string }[] }`.
- `creditors.routes.ts` — `[{ path: '', component: CreditorsPage }]`.

### 4.2 Page component `pages/creditors-page/`
- **Template:** `features/instruments/pages/instruments-page/` (.ts/.html/.css) for the add-form + list + loading/error/empty/ready status signals.
- **Accounts sub-form:** the accounts are a **`FormArray`** of `{ label, identifier }` rows with add/remove buttons. Copy the dynamic-row mechanics from the **Split FormArray** in `features/financing/pages/load-expense-page/load-expense-page.ts` (+ its `.html` for the add/remove row UI).
- Form model:
  ```ts
  type CreditorForm = FormGroup<{
    name: FormControl<string>;
    accounts: FormArray<FormGroup<{ label: FormControl<string>; identifier: FormControl<string> }>>;
  }>;
  ```
  `name` required (trim on submit, like `parties-page` does). Start with one empty account row; allow add/remove.
- Submit: transform to `CreateCreditor`, call `creditorsService.create(body)`, set a `createdCreditorId` signal on success, then reload the list (`loadCreditors()`), mirroring the Instruments submit → reload pattern.
- List section: `@for (creditor of creditors(); track creditor.id)` showing name + its accounts (label — identifier).

### 4.3 Register route + nav
- `app.routes.ts` — add a lazy route: `{ path: 'creditors', loadChildren: () => import('./features/creditors/creditors.routes').then(m => m.routes) }`.
- `app.ts` — append `{ label: 'Creditors', path: '/creditors' }` to `navItems`.
- `app.html` renders `navItems` already — no change needed there beyond the array.

### 4.4 Design
Follow `app/client/docs/SYSTEM.md` ("warm homebanking"), identical to every other view: `mx-auto max-w-184` container, serif "Personal ledger" kicker, tokens (`text-ink`, `border-rule`, `focus-visible:border-stamp`), the lifted `bg-paper-raised` form block. Reuse the native-`<select>`/input utility classes already used by Instruments. No hardcoded hex.

---

## 5. Testing

### 5.1 API (`app/api/tests/PersonalFinance.Financing.Tests`, xUnit v3)
Run from `app/api/`: `dotnet test --project tests/PersonalFinance.Financing.Tests`.
- **Domain:** `Creditor.Create` — succeeds with a valid name (+ accounts pass through with generated ids); fails on empty/whitespace name; a creditor with zero accounts is allowed.
- **Handler:** `CreateCreditorHandler` persists the creditor + its accounts (in-memory SQLite fixture, per `OnPaymentPlanCreatedTests` style — `new SqliteConnection("Filename=:memory:")`). `ListCreditorsHandler` returns creditors with their accounts, ordered by name.

### 5.2 Client (Karma/Jasmine)
Run from `app/client/`: `CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless`.
- **Template:** `features/parties/pages/parties-page/parties-page.spec.ts`.
- `creditors-page.spec.ts` — `provideZonelessChangeDetection`, `provideRouter([])`, a `create` jasmine spy on `CreditorsService`. Assert: name is trimmed; account rows can be added/removed; the submitted body has the right `{ name, accounts }` shape; `createdCreditorId` is set; the list is re-fetched after create.

---

## 6. Verification — "Slice 1 done when"

- [ ] `dotnet build` clean (from `app/api/`).
- [ ] `AddCreditors` migration created and applied; `financing_creditors` + `financing_creditor_accounts` exist.
- [ ] `dotnet test --project tests/PersonalFinance.Financing.Tests` green.
- [ ] `pnpm ng lint` + `pnpm ng build` clean; `CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless` green (new spec + no regressions).
- [ ] **Manual:** run the API (`dotnet run --project src/Bootstrap/PersonalFinance.Api`) + client (`pnpm ng serve`), open `/creditors`, create a creditor named "Juan" with two accounts (Galicia / CBU…, Mercado Pago / alias…), confirm it appears in the list. `GET /v1/creditors` returns `{ rows: [...] }` with nested accounts.

Only when every box is checked do you start Slice 2.

---

## 7. Out of scope for this slice
- The Load Expense form (Slice 2).
- Editing/deleting creditors or accounts (Q3).
- Any ledger/payable balance (metadata-only decision, Q6).
- CBU/alias validation or a bank catalog (free text, C-1).

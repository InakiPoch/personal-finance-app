# Slice 3 — Remove the stale shared-expense page; prefill the party on Load an Expense

> Read `00-overview.md` first. API Phase 52 / client Phase 49.

## Goal

`/parties/shared-expense?party={id}` predates the three payment modes. It asks the user to **paste an
expense account UUID**, lets them pick a credit card as the funding source (which the debit path
rejects elsewhere), and shows a raw `splitReferenceId` on success. Everything it does, Load an Expense
already does better (D8):

- multi-party split rows (`+ add party`, weight per party, holder = weight 1),
- all three modes (card / creditor / debit-cash) — the old page only does debit,
- category picked by **name** (auto-provisioned), bank/cash-only source,
- a reconcile panel showing each party's new balance.

So: delete the page **and** its HTTP endpoint. The party detail button now opens Load an Expense
with that party already added as a split row.

## Why delete the endpoint too

Nothing else calls `POST /v1/parties/shared-expenses`. The **command** stays: Ledger's
`RecordDebitExpenseHandler` sends `RegisterSharedExpenseCommand` through `IPartiesApi` whenever a debit
expense has split rows. Deleting only the HTTP surface removes a second, weaker way to create the same
ledger shape. Less surface, one behaviour.

## Current state (verified 2026-09-29)

API (`H/` = `app/api/src/Bootstrap/PersonalFinance.Api/Endpoints`):

- `H/Parties/PostSharedExpense.cs` — the endpoint handler.
- `H/EndpointExtensions.cs:238-241` — `group.MapPost(ApiRoutes.Parties.SharedExpenses, …)`.
- `H/ApiRoutes.cs:65` — `SharedExpenses = "/shared-expenses"`.
- `H/Mapping/PartyMappingExtensions.cs:13-17` (`ToRegisterSharedExpenseCommand`) and `:87-88`
  (`ToSharedExpenseResultDto`).
- `H/DTOs/RegisterSharedExpenseDTO.cs` (+ `SharedExpenseResultDto`, wherever it's declared).
- `app/api/src/Bootstrap/PersonalFinance.Api/PersonalFinance.Api.http` — sample requests near `:308`.
- **Keep:** `Parties.Contracts/Commands/RegisterSharedExpenseCommand.cs`, `IPartiesApi.RegisterSharedExpenseAsync`,
  `PartiesApi.cs`, `RegisterSharedExpenseHandler/Validator`, `PartiesModule.cs` registration — all used by
  `L/Application/Commands/RecordDebitExpense/RecordDebitExpenseHandler.cs:39-52` and by
  `tests/PersonalFinance.Reporting.Tests/ReportingIntegrationFixture.cs:214-226` (module-level, fine).
- **Tests hitting the HTTP route:** `tests/PersonalFinance.Api.Tests/PartiesCurrencyTests.cs:142`
  (`RegisterSharedExpenseAsync(client, …)` helper used at :19-20, :36-37, :83-84, :111-112). These must be
  rewritten to seed through the **debit-expense endpoint with a split** (the real user path) — find the
  route in `H/ApiRoutes.cs` (Ledger debit expense) and its DTO. Keep each test's assertions unchanged;
  only the seeding helper changes. Note the debit path requires a Bank/Cash source and a category name
  instead of an expense account id.
- `app/api/docs/DESIGN.md` mentions the endpoint — update.

Client (`C/` = `app/client/src/app`):

- `C/features/parties/pages/shared-expense-page/` (ts, html, spec) — delete.
- `C/features/parties/parties.routes.ts:9` — the `shared-expense` route — delete.
- `C/features/parties/parties-service.ts:54-56` `registerSharedExpense` + its spec cases in
  `parties-service.spec.ts` — delete.
- `C/features/parties/types/register-shared-expense.ts`, `shared-expense-result.ts`,
  `shared-expense-participant.ts` — delete if nothing else imports them (grep first).
- `C/features/parties/pages/party-detail-page/party-detail-page.html:63-70` — the link
  "Register a shared expense with this party" (`routerLink` + `queryParams { party: id }`).
- `C/features/financing/pages/load-expense-page/load-expense-page.ts`:
  - `split: FormArray<SplitRow>`; `addSplitRow()` at `:147` pushes `createSplitRow()` (`:387-391`,
    `partyId` control required, `weight` default 1).
  - Parties list comes from `this.parties()` (signal, used at `:288`).
  - The page does not read query params today.

## API changes

1. Delete the endpoint file, route constant, `MapPost`, mapping extension methods, and DTO(s).
2. Remove the `.http` samples.
3. Rewrite `PartiesCurrencyTests` seeding to go through the debit-expense endpoint with split rows.
4. Update `app/api/docs/DESIGN.md` endpoint list.

## Client changes

1. Delete the page, route, service method, spec cases, and unused types.
2. Party detail: the button becomes **"Split an expense with <name>"** →
   `routerLink="/financing/load-expense"` `[queryParams]="{ party: id }"`.
3. Load an Expense: inject `ActivatedRoute`; on init, if `party` query param is present **and** matches a
   known party once the parties list has loaded, push one split row with that `partyId` (weight 1). Unknown
   id → ignore silently (no row). Don't add a second row if the user navigates back and forth (only on init).
   The default mode stays `card`; the user picks the mode.

## Test plan

API:
- `PartiesCurrencyTests` — same assertions, green, via the new seeding helper.
- A test that `POST /v1/parties/shared-expenses` now returns 404 (optional; one line — proves the route is gone).
- Existing `RecordDebitExpenseHandlerTests` (split path) stay green unchanged.

Client:
- `load-expense-page.spec.ts`: with `?party=p1` and parties `[p1]` → one split row with `partyId 'p1'`, weight 1;
  with `?party=unknown` → zero rows; without the param → zero rows.
- `party-detail-page.spec.ts`: the link points to `/financing/load-expense` with `party` query param.
- `parties.routes` no longer contains `shared-expense` (only if an existing spec enumerates routes).

## Steps

- [x] 1. API prod — deletions + DESIGN.md. Build clean.
- [x] 2. API tests — reseed `PartiesCurrencyTests`; all projects green.
- [x] 3. Client prod — deletions, party link, query-param prefill. Lint + prod build clean.
- [x] 4. Client specs — as above. Green.
- [x] 5. Doc-sync — API `TASK.md` Phase 52 / client `TASK.md` Phase 49; both `CLAUDE.md` files if they list the
      endpoint/page (remove it; note "splits are created only from Load an Expense").

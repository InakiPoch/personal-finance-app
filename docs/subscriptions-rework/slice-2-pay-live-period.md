# Slice 2 — Pay a live period

> Read `00-overview.md` and `slice-1-explicit-pay-foundation.md` first. This slice needs the status
> model + `MarkCurrentPeriodPaid(...)` + `SubscriptionStatus` type introduced in Slice 1.

## Goal / Why

Let the user settle a **due** or **overdue** subscription with one click. Paying:

- posts one real `X` charge (`Dr Expense / Cr Funding`) **dated today** (decision 5) → it enters this
  month's out-of-pocket;
- marks the current period paid and advances the due date by **exactly one month** (decision 4);
- flips the row's status **overdue/upcoming → paid**.

For a subscription several months behind, one Pay settles one period; if it is still in the past
afterwards it stays **overdue** and can be paid again (one at a time — no silent forgiveness, no bulk
`N·X`).

## Context & current state

After Slice 1, the aggregate has `LastPaidPeriod`, `NextDueDate` (next unpaid period's due date), and
`MarkCurrentPeriodPaid(DateOnly)`. `SubscriptionChargeCalculator.Build(...)` already produces the correct
`Dr Expense / Cr Funding` `PostTransactionCommand` with a `SubscriptionReferenceId`. There is no pay
command/endpoint yet.

Reuse blueprint: `PayCreditorInstallmentHandler`
(`.../Financing/Application/Commands/PayCreditorInstallment/PayCreditorInstallmentHandler.cs`) for the
command/handler/endpoint/error shape — but note the key difference: **subscriptions DO post to the
ledger** at pay-time (creditor pay is display-only), so this handler also injects `ILedgerApi`.

## API changes

### 1. Command + handler

- Contract command: `PaySubscriptionCommand(Guid SubscriptionId) : ICommand<Guid>` — id only; the charge
  date is the server clock (decision 5), no date field, no bank picker.
- Handler `PaySubscriptionHandler` — primary ctor `(SubscriptionsDbContext context, ILedgerApi ledger,
  TimeProvider timeProvider)`. Flow:
  1. Load the template; not found → `Subscriptions.SubscriptionNotFound`.
  2. Not active (`IsActive == false`) → `Subscriptions.SubscriptionNotActive`.
  3. Already paid for the current calendar month → `Subscriptions.SubscriptionAlreadyPaid`
     (idempotency guard — mirrors `InstallmentAlreadyPaid`).
  4. `var now = timeProvider.GetUtcNow();` Post `SubscriptionChargeCalculator.Build(ExpenseAccountId,
     FundingAccountId, Amount, Id, now)` via `ledger.PostTransactionAsync(...)`. If the posting fails,
     return its error (do not mutate the aggregate).
  5. Determine the period being paid = the **first unpaid period's anchor** (= `NextDueDate` before the
     call, i.e. `LastPaidPeriod` + 1 month, or the seeded first due if never paid). Call
     `MarkCurrentPeriodPaid(paidPeriodAnchor)` → sets `LastPaidPeriod`, advances `NextDueDate` one month.
  6. Save; return `template.Id` (`Result<Guid>`).

  **Catch-up semantics (decision 4):** step (5) advances `LastPaidPeriod`/`NextDueDate` by exactly one
  month. It does **not** jump to the current calendar month. So paying a 3-months-behind sub once leaves
  it 2 months behind (still overdue).

- Register the handler in `SubscriptionsModule.cs`.

### 2. Endpoint

- `POST /v1/subscriptions/{id:guid}/pay` — add the route constant to `ApiRoutes.cs`
  (`Subscriptions` group) and map it in `EndpointExtensions.cs` (`MapSubscriptionsEndpoints`), mirroring
  `PayCreditorInstallment.cs`. Return `TypedResults.Ok(PaySubscriptionResultDto)` (not `Created`), unwrap
  failures via `ProblemResultsHelper.From(result.Error)`. `.Produces<PaySubscriptionResultDto>(200)
  .ProducesProblem(404).ProducesProblem(409)`. Add the result DTO under `Endpoints/DTOs/`.

### 3. Error mapping (`ErrorHttpStatusHelper.cs`)

- `Subscriptions.SubscriptionNotFound` → 404 (matches the `*NotFound` suffix fallback — no explicit line
  needed, but confirm).
- `Subscriptions.SubscriptionAlreadyPaid` → 409 (matches the `*AlreadyPaid` suffix fallback — confirm).
- `Subscriptions.SubscriptionNotActive` → 409 (matches the `*NotActive` suffix fallback — confirm).
- If any chosen code does **not** match a suffix rule, add an **explicit** entry (the suffix table only
  covers `NotFound`/`AlreadyPaid`/`AlreadyAccrued`/`AlreadyReversed`/`NotActive`/`Invalid`/`NonPositive`).

## Client changes

- `subscriptions-service.ts`: add `pay(id: string): Observable<PaySubscriptionResult>` → `POST
  subscriptions/{id}/pay`.
- `types/`: add `pay-subscription-result.ts` (`{ id: string }`).
- `pages/subscriptions-page/subscriptions-page.html`: per-row **Pay** button, shown when
  `status === 'overdue' || status === 'upcoming'` (hidden when `paid`). Reuse the creditor per-row action
  styling (`creditor-purchases-table.html` Pay button, `text-stamp`). On click →
  `SubscriptionsService.pay(id)` → on success reload `listActive()`; surface errors via the page's
  existing error/toast state (`role="alert"`, `text-negative`).
- Keep the button disabled while a request is in flight (mirror the existing form-submit guard).

## Testing

**API** (`PersonalFinance.Subscriptions.Tests`, in-memory SQLite + `FixedTimeProvider` + `FakeLedgerApi`):

1. **Pay an overdue period** posts **exactly one** `X`, dated `fixedNow` (assert
   `FakeLedgerApi.PostedTransactions` count == 1 and its `PostedOnUtc == fixedNow`); status → **paid**;
   `NextDueDate` advanced one month.
2. **Multi-overdue stays overdue**: a sub 3 months behind, paid once → one charge, still **overdue**
   (`NextDueDate` still `<= today`); paying three times settles it (three separate `X`, then **paid**).
3. **Already paid this month → 409** (`SubscriptionAlreadyPaid`); **no** new posting captured.
4. **Unknown id → 404** (`SubscriptionNotFound`).
5. **Cancelled/inactive sub → 409** (`SubscriptionNotActive`); no posting.
6. **Ledger-post failure**: if `ILedgerApi` returns failure, the aggregate is **not** mutated
   (`LastPaidPeriod`/`NextDueDate` unchanged) and the error propagates.

**API contract** (`PersonalFinance.Api.Tests`, `ApiWebApplicationFactory`): `POST
/v1/subscriptions/{id}/pay` is present in OpenAPI; 404 problem shape for a missing id. (Happy-path
ledger effects are covered by the module tests + the `.http` walk, since the WAF strips hosted services.)

**Client**:
- `subscriptions-service.spec.ts`: `pay()` POSTs the right URL and maps the result.
- `subscriptions-page.spec.ts`: Pay button visible only for overdue/upcoming; clicking calls the service
  and reloads; error path renders the alert.

## Out of scope

- Undo (Slice 3). A mis-click is not reversible until Slice 3 ships.
- "Pay all overdue at once" (explicitly deferred per decision 4 — could be a later add-on).
- The Dashboard block (Slice 4).

## Verification

1. `cd app/api && dotnet build`; run `PersonalFinance.Subscriptions.Tests` + `PersonalFinance.Api.Tests`
   via their compiled binaries.
2. `cd app/client && pnpm ng lint && pnpm ng build --configuration production`; run client tests.
3. End-to-end: with an **overdue** sub, press Pay → it becomes **Paid**, one `X` appears in this month's
   out-of-pocket, `NextDueDate` moves forward one month. With a sub several months behind, one Pay leaves
   it overdue; repeat to catch up. Paying an already-paid sub is rejected (409).
4. Slice is green before starting Slice 3.

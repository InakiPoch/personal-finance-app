# Slice 4 — Pay full creditor debt

> Read `00-overview.md` and Slices 1–3 first. This closes the initiative. Same display-only model
> as Slice 3 — a stamp, no ledger. This slice is small; it builds directly on Slice 3's command
> shape.

## Goal

One button on the creditor detail page that settles the creditor's **entire remaining** debt:
stamp `PaidOnUtc = now` on **every** unpaid, non-reversed installment across all of that creditor's
purchases. After it runs, the list's `TotalOwedMinorUnits` (and `DueNowMinorUnits`) for that
creditor is $0.

## Why "all remaining" and why a bulk command (intent)

The user chose "pay full debt = clear it all, future cuotas included" (`00-overview.md` Q3). The
per-installment Pay buttons from Slice 3 already cover the surgical "just this one" case, so the
bulk button earns its place only by doing the *whole* thing. A single `PayCreditorFullDebt` command
is idempotent-friendly (already-paid cuotas are skipped via `MarkPaid`'s double-pay guard) and does
in one transaction what would otherwise be N round-trips.

Undo at the bulk level is intentionally **not** added — Slice 3's per-cuota Undo is the recovery
path; a "unpay everything" is unlikely and can be a later addition if asked. Note this in the doc
so a fresh session doesn't assume it's missing by mistake.

## API changes

No schema change, no EF migration, no new module edge. RNF-9 unaffected.

**Command** — `…/Contracts/Commands/PayCreditorFullDebtCommand.cs`:

```csharp
public sealed record PayCreditorFullDebtCommand(Guid CreditorId) : ICommand<int>; // returns count settled
```

Returning the count of installments settled gives the client something to confirm ("Settled 7
cuotas"); a `Guid`/void is fine too — pick and note it.

**Handler** — `Application/Commands/PayCreditorFullDebt/PayCreditorFullDebtHandler.cs`, ctor
`(FinancingDbContext context, TimeProvider timeProvider)`:

1. Optionally verify the creditor exists (`context.Creditors.AnyAsync(...)`) → else
   `CreditorNotFound` (404). (Cheap and gives a clean error for a bad id.)
2. Load this creditor's installments (tracked):
   `from installment in context.Set<Installment>() join plan in context.PaymentPlans on
   installment.PaymentPlanId equals plan.Id where plan.CreditorId == query.CreditorId select
   installment` → `ToListAsync`.
3. For each where `!IsReversed && !IsPaid`: `installment.MarkPaid(now)`. Count them.
4. `SaveChangesAsync`; return the count. Zero settleable → return 0 (idempotent — not an error;
   the debt was already clear).

Register in `FinancingModule.Register`.

**Host** — `POST /v1/financing/creditor-payables/{creditorId}/pay-full`:
- `ApiRoutes.Financing.CreditorPayableFullPayment = "/creditor-payables/{creditorId:guid}/pay-full"`.
- `Endpoints/Financing/PayCreditorFullDebt.cs` — map id → command, `commandBus.SendAsync<int>`,
  on failure `ProblemResultsHelper.From`, else `TypedResults.Ok(new PayCreditorFullDebtResultDto(count))`.
- `Endpoints/DTOs/PayCreditorFullDebtDTO.cs` (result only, empty request body) +
  `FinancingMappingExtensions`. Wire with `.Produces<…ResultDto>(200)` + `.ProducesProblem(404)`,
  tag `"Financing"`.

## Client changes

All under `app/client/src/app/features/financing/`.

1. **Types**: `pay-creditor-full-debt-result.ts` (`{ settledCount: number }`).
2. **Service** `financing-service.ts`:
   `payCreditorFullDebt(creditorId): Observable<PayCreditorFullDebtResult>` →
   `POST financing/creditor-payables/${creditorId}/pay-full` (empty body).
3. **Detail page** (`creditor-detail-page`): a **"Pay full debt"** button in the page header,
   disabled when `TotalOwed` is already $0 or a request is in flight. On success, re-fetch
   `creditorDetail(id)` → every group shows fully paid; optionally a toast/confirmation using the
   returned count. Consider a lightweight confirm step (this settles everything) — a simple
   "Are you sure?" inline confirm, not a heavy modal; note the choice.

## Testing

**API** — `tests/PersonalFinance.Financing.Tests/PayCreditorFullDebtHandlerTests.cs` (in-memory
SQLite + `FixedTimeProvider`, `SeedCreditorAsync`):

- Stamps **all** unpaid, non-reversed installments across multiple purchases; returns the count;
  `GetCreditorPayables` for that creditor then reports `TotalOwed == 0` and `DueNow == 0`.
- **Skips reversed** (stays unpaid, not stamped) and **skips already-paid** (untouched, not
  double-counted) — the count reflects only newly-settled.
- **Idempotent**: running it twice → second run settles 0.
- Unknown creditor → `CreditorNotFound` (if the existence check is added).

**Client** — `financing-service.spec.ts` (+1: URL/verb/empty body); detail-page spec: button
disabled at $0, calls the service, refetches on success.

## Verification (closes the initiative)

- API: build Release, run the Financing binary (`-class "*PayCreditorFullDebt*"`) + full suite
  green; RNF-9 green; `AccrualBoundaryTests` untouched.
- Client: lint, test, prod build clean.
- **End-to-end (handed to user), full initiative walk:** load a back-dated creditor purchase →
  "Owed to creditors" shows `Due now` (arrears + this cycle) and `Total owed` (adds future) with
  paid excluded → open the creditor → purchases grouped with per-cuota status → Pay one cuota →
  figures drop → Undo → figures restore → **Pay full debt** → `Total owed` = $0, every cuota Paid.
  No bank balance moves anywhere (display-only, by design).

## Doc-sync (after Slice 4 is green — closes the initiative)

Mirror how prior initiatives recorded their phases:
- `app/api/.claude/CLAUDE.md` — new Phase entries (one per slice, or one summarizing) + header
  phase-count bump.
- `app/api/docs/DESIGN.md` (D2 read/command bullets) and `app/api/docs/PRD.md` (new decision entry).
- `app/client/.claude/CLAUDE.md` + both `TASK.md` files.
- State clearly that the initiative `docs/owed-to-creditors/` is complete.

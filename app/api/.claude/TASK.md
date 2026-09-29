# TASK.md — Phase Ledger

Compact phase-by-phase build history for the PersonalFinance API, sequenced by real code dependency order, not module-list order. Each entry: what shipped, key API/domain surface touched, test count after the phase. Full step-by-step task lists, Definition-of-done checklists, and narrative completion notes are **not** kept here once a phase is green — that process detail served its purpose during execution. For phases built from a vertical-slice doc, the doc under repo-root `docs/<feature-slice>/` is the source of truth for design rationale; check there before re-deriving it.

See `.claude/CLAUDE.md` for the architecture summary, commands, and current status.

Source docs: `docs/PRD.md` (product), `docs/DESIGN.md` (technical decisions log D1–D18 + folder scaffold in §6).

## Phase 0 — Repository & solution scaffolding (2026-08-28)

Replaced the flat `dotnet new webapi` scaffold with `PersonalFinance.sln` + `global.json` + `Directory.Build.props` at `app/api/` (the solution root); host moved to `src/Bootstrap/PersonalFinance.Api/`.

## Phase 1 — Shared substrate (2026-08-29)

`PersonalFinance.SharedKernel` (`Money`/`Currency`/`Result`/`Error`/`Entity`/`AggregateRoot`/`PhantomPennyAllocator`), `PersonalFinance.Abstractions` (CQRS + `IModule`), `PersonalFinance.Infrastructure` (command/query buses, Outbox/Inbox, `ModuleDbContextBase`, `SchedulerBase`, `SqliteConnectionFactory`, `AddSharedInfrastructure`). WAL + `busy_timeout` verified live.

## Phase 2 — Ledger module (2026-08-29)

Sole source of accounting truth: double-entry append-only `Transaction`/`Entry`/`Account`, plain storno reversal (D12 cascade deferred to Phase 4), `ILedgerApi`. `POST /v1/ledger/transactions`, `.../transactions/{id}/reversal`, `GET .../accounts/{id}/balance`. `Money` maps to SQLite as a scalar via `ValueConverter<Money,long>` — currency not stored, reconstructed as `Currency.Reference`. 7 tests.

## Phase 3 — Financing module (2026-08-30)

`CreditCard`/`PaymentPlan`/`Installment`/`MonthlyStatement`, `BillingCycleCalculator` (cutoff-day cycles, not calendar month), `PhantomPennyAllocator` installment split, `AccrueInstallments` scheduler, `PayStatement`. First Outbox producer (`PaymentPlanCreatedIntegrationEvent`). Unified `POST /v1/instruments` router (D13). `PersonalFinance.Architecture.Tests` created (module isolation, RNF-9). 20 tests.

## Phase 4 — Reversal semantics: D12 (2026-08-30)

Reversal always succeeds: plain storno always, plus a compensating `Dr CardCredit / Cr CardLiability` when the reversed installment was already paid (netted against the *next* statement, never cash directly). Parties cascade left as a seam for Phase 6. 40 tests.

## Phase 5 — Subscriptions module (2026-08-30)

Recurring charges; charged-on-subscribe + a `RenewDueSubscriptions` scheduler for later periods (this auto-charge model was killed and replaced in Phases 38–40). `POST /v1/subscriptions`, `DELETE .../{id}`, `GET .../active`. 75 tests.

## Phase 6 — Parties module (2026-08-31)

Third-party shared-expense tracking as a management view over Ledger receivable accounts (D1), never a second ledger. Synchronous debit/cash split posts immediately; async card-split records intent only via Outbox/Inbox (`OnPaymentPlanCreated`, Option C — no ledger post until accrual, D11). Phase-4 reversal-cascade seam closed (`CorrectExpenseSplitAsync`, metadata-only, no ledger post). 110 tests.

## Phase 7 — Reporting module (2026-08-31)

Read-only leaf over `vw_*` views only (D5/RNF-6) — no `.Contracts`, no `DbContext`, raw ADO. Four queries: monthly-expenses, card-due-by-month, party timeline, debt-by-party. First multi-context integration test harness. 115 tests. Flagged two view-leak gaps (RF-1 card-purchase leak into monthly expenses, RF-2 no shared card-identity key) — both fixed in Phase 10.

## Phase 8 — Host polish for the Angular client: D13 (2026-09-01)

RFC-9457 `ProblemDetails` envelope for every failure, configurable CORS, complete OpenAPI at `/openapi/v1.json`, `/health` wired to `OutboxHealthCheck` (RNF-7). 126 tests.

## Phase 9 — CI (2026-09-01)

`.github/workflows/ci.yml` at the git root: restore/build/test the whole `.sln`, SDK pinned via `global.json`, 20 `packages.lock.json` committed for `--locked-mode`. No Docker in the per-push loop (LLM Council verdict, `app/api/council/`).

## Phase 10 — Fase-1 PRD acceptance pass (2026-09-02)

Walked every Fase-1 acceptance criterion live; fixed both Phase-7 view leaks in code (`AccountKind.CardPurchases`, `OwnerReferenceId` shared card-identity seam); reconciled `docs/DESIGN.md` D12's worked example. 127 tests.

## Phase 11 — US-4 AC1 statement detail (2026-09-02)

`GET /v1/financing/statements/{id}` itemizes the accrued installments behind a statement's `AmountDue`. Closed the one open Fase-1 acceptance gap. 129 tests.

## Phase 12 — Client-driven read endpoints (2026-09-03)

`GET /v1/instruments`, `GET /v1/financing/cards/{id}/statements`, `GET /v1/ledger/transactions`, plus a `TransactionId`/`ReversalTransactionId` D20 enabler threaded into the Parties timeline and statement-installment rows. 140 tests.

## Phase 13 — Creditors CRUD (2026-09-04)

New `Creditor`/`CreditorAccount` reference entities in Financing (distinct from Parties' `Party` — tracks *payees*, not debt). `POST /v1/creditors`, `GET /v1/creditors`. `docs/creditor-expense-fields/slice-1-creditors-crud.md`. 149 tests.

## Phase 14 — Load-expense creditor metadata (2026-09-04)

`CreatePaymentPlanCommand` gains optional `CreditorId`/`CreditorAccountId` — pure metadata, no ledger impact, no cross-validation. `docs/creditor-expense-fields/slice-2-load-expense-integration.md`. 153 tests.

## Phase 15 — Expense description field (2026-09-04)

Required `PaymentPlan.Description`, threaded through `CreatePaymentPlan` and echoed on the client confirmation panel. `docs/expense-description/slice-1-description-field.md`.

## Phase 16 — Card-debt drill-down (2026-09-04)

`GET /v1/financing/cards/{id}/purchases` — outstanding purchases behind a card's total, each with its description. `docs/expense-description/slice-2-card-debt-drilldown.md`. 170 tests.

## Phase 17 — Recent purchases view (2026-09-05)

`GET /v1/financing/purchases/recent` — newest-first purchase list across every card. `docs/expense-description/slice-3-recent-purchases-view.md`. Closes the expense-description initiative. 175 tests.

## Phase 18 — Creditor-financed expenses (2026-09-05)

`PaymentPlan.CardId` made nullable (card XOR creditor); a card-less plan gets no billing cycle/statement/card ledger posting. Split co-borrower routes through a new `CreditorPayable` liability account via `AccrueCreditorSplitInstallments` (later retired, Phase 23/26). `docs/expense-payment-modes/slice-1-creditor-financed.md`. 192 tests.

## Phase 19 — Owed to creditors list (2026-09-05)

`GET /v1/financing/creditor-payables` — outstanding grouped by creditor with per-account breakdown. Known limitation at ship time (no paid/settled flag) — closed by Phase 34. `docs/expense-payment-modes/slice-2-owed-to-creditors-list.md`. 198 tests.

## Phase 20 — Debit/cash expenses with categories (2026-09-05/06)

`POST /v1/ledger/expenses` — one balanced transaction (unsplit) or a `RegisterSharedExpense` split; category get-or-created case-insensitively as an `Expense`-`Expense` account. `docs/expense-payment-modes/slice-3-debit-cash-categories.md`. Closes the payment-modes initiative. 222 tests.

## Phase 21 — Dashboard: card name on future rows (2026-09-06, API half)

`vw_card_future_schedule` + `card_due_by_month.sql` label future rows with the card **name**, not its GUID (client-side expand-grouping fix is `app/client/.claude/TASK.md`). `docs/dashboard-fixes/slice-2-card-name-and-expand.md`. 223 tests.

## Phase 22 — Dashboard: Parties list endpoint (2026-09-06)

`GET /v1/parties` — every registered party by name, including ones with zero ledger movements (fixes the debt-summary INNER JOIN silently dropping them). `docs/dashboard-fixes/slice-3-parties-list-endpoint.md`. Closes the dashboard-fixes initiative. 227 tests.

## Phase 23 — Creditor-financed split posts up front (2026-09-06, bugfix)

A creditor-financed split's co-borrower receivable now posts in full at link time (mirrors the debit/cash split) instead of trickling in via `AccrueCreditorSplitInstallments` (retired) — fixed a "Settled up" bug for same-month purchases. 227 tests (net-flat).

## Phase 24 — Parties card-split future shares (2026-09-06)

`GET /v1/parties/{id}/future-shares` — projected per-installment shares for not-yet-accrued card splits, byte-exact with real accrual. `docs/parties-card-split/slice-2b-party-future-shares.md`. Known limitation at ship time: dated by statement-close cycle, not payment cycle — resolved by Phase 25.

## Phase 25 — Card due-month reframe (2026-09-06)

Payment-facing card surfaces (future-shares, future-schedule, card-due-by-month) now show `DueCycle` (= close cycle + 1, "when the money moves"); statement-facing surfaces keep the raw close cycle. `AccrueInstallments` gains a second gate posting the split co-borrower receivable at the due month. `docs/cycle-due-month/slice-1-card-due-month.md`. Financing 89 tests.

## Phase 26 — Creditor-split parity (2026-09-06/07)

Creditor-financed splits now behave exactly like card splits: $0 owed at link time, receivable accrues at `DueCycle` via a third `AccrueInstallments` gate. Undoes Phase 23's up-front booking (safe now that Phase 25 made the schedule visible). `docs/cycle-due-month/slice-2-creditor-split-parity.md`. 251 tests.

## Phase 27 — Schedule-aware Parties summary (2026-09-07)

`GET /v1/parties/pending-shares` — bulk per-party scheduled-count/total so the Parties **list** stops reading "Settled up" for a $0-now-but-scheduled party. `docs/cycle-due-month/slice-3-schedule-aware-summary.md`. Closes the cycle-due-month initiative. Financing 99 / 259 total.

## Phase 28 — Foundation: pay from installments (2026-09-07)

Fixed a latent overpay bug: `PayStatement` now charges Σ(accrued, non-reversed, unpaid) installments instead of the stored, increment-only `MonthlyStatement.AmountDue` (never decremented on reversal). Added `Installment.PaidOnUtc`/`MarkPaid` — no new user-facing payment path yet. `docs/individual-installment-payments/slice-1-foundation-payable-from-installments.md`. 263 tests.

## Phase 29 — Pay a single installment (2026-09-07)

`POST /v1/financing/installments/{id}/pay` settles one cuota (`Dr CardLiability / Cr Bank`, no carried-credit netting); "Pay full statement" relabeled. `docs/individual-installment-payments/slice-2-pay-single-installment.md`. 270 tests.

## Phase 30 — Next-payment visibility (2026-09-07)

Recent Purchases rows derive "N/M paid · next: <month>" / "Fully paid" from `PaidOnUtc` + `DueCycle`. `docs/individual-installment-payments/slice-3-next-payment-visibility.md`. Closes the individual-installment-payments initiative. 273 tests.

## Phase 31 — Back-dated card expenses (2026-09-07)

Loading a past-dated card purchase synchronously accrues every already-closed cycle and pays past-due installments from a new `BankAccountId`, historically dated; future-dated purchases rejected for every mode. `docs/backdated-expenses/slice-1-card-backdating.md`. Financing 122 / 282 total.

## Phase 32 — Back-dated creditor expenses (2026-09-07)

Creditor purchases now honor a uniform 26th cutoff (`ResolveCycle`); a back-dated creditor purchase stamps already-elapsed installments `PaidOnUtc` — display-only, no ledger movement. `docs/backdated-expenses/slice-2-creditor-cutoff.md`. Financing 126 / 286 total.

## Phase 33 — Pending $ on Recent Purchases (2026-09-07)

Rows carry a server-computed `PendingAmountMinorUnits` = Σ unpaid, non-reversed installment amounts. `docs/backdated-expenses/slice-3-pending-amount.md`. Closes the backdated-expenses initiative. Financing 128 / 288 total.

## Phase 34 — Owed to creditors: current-cycle outstanding (2026-09-08)

`GetCreditorPayablesQuery` replaces the single outstanding total with `DueNowMinorUnits` (unpaid ≤ current due cycle, arrears folded in) + `TotalOwedMinorUnits` (all unpaid, future included); paid excluded from both. `docs/owed-to-creditors/slice-1-current-cycle-outstanding.md`. Financing 131 / 291 total.

## Phase 35 — Creditor detail view (2026-09-08)

`GET /v1/financing/creditor-payables/{id}` drills into one creditor's debt grouped by purchase, per-cuota status (overdue/due/future/paid/reversed); unknown creditor → 404. `docs/owed-to-creditors/slice-2-creditor-detail-view.md`. Financing 135 / Api 45 / 297 total.

## Phase 36 — Pay a creditor cuota + undo (2026-09-08)

`POST /v1/financing/creditor-installments/{id}/pay` + `/unpay` — display-only `PaidOnUtc` stamp/clear, no bank, no ledger; undo of an already-unpaid cuota is a silent no-op success. `docs/owed-to-creditors/slice-3-pay-installment-and-undo.md`. Financing 145 / 307 total.

## Phase 37 — Pay a creditor's full debt (2026-09-08)

`POST /v1/financing/creditor-payables/{id}/pay-full` stamps every unpaid, non-reversed installment across all of a creditor's purchases in one transaction; returns the settled count; idempotent (0 settleable → 0, not an error); no bulk undo (Phase 36's per-cuota undo is the recovery path). `docs/owed-to-creditors/slice-4-pay-full-debt.md`. Closes the owed-to-creditors initiative. Financing 151 / 313 total.

## Phase 38 — Subscriptions rework: explicit-pay foundation (2026-09-16)

Killed the auto-charge `RenewDueSubscriptions` scheduler outright (fixed the "N missed cycles = N·X" out-of-pocket bug). `LastRenewalOnUtc` → `LastPaidPeriod`; a subscription is charged at registration only if its current period's anchor has already passed, else starts `Upcoming`; `GET /v1/subscriptions/active` derives `paid`/`overdue`/`upcoming`. One-time data wipe (incompatible model change). `docs/subscriptions-rework/slice-1-explicit-pay-foundation.md`. 323 tests.

## Phase 39 — Subscriptions: pay a live period (2026-09-17)

`POST /v1/subscriptions/{id}/pay` settles the next unpaid period — one real ledger charge dated today, one period per call (never a silent batch). `docs/subscriptions-rework/slice-2-pay-live-period.md`. 331 tests.

## Phase 40 — Subscriptions: undo a payment (2026-09-17)

`POST /v1/subscriptions/{id}/unpay` reverses the pay-time transaction via the dedicated Ledger reversal API and steps `LastPaidPeriod`/`NextDueDate` back one month; new `LastPaidTransactionId` field. `docs/subscriptions-rework/slice-3-undo-payment.md`. 341 tests. (Slice 4, a Dashboard subscriptions block, is client-only.)

## Phase 45 — Incomes: record + Dashboard toggle (2026-09-24)

`Transaction.Description` persists for the first time (repairs a real gap — debit/split/settlement postings were silently dropping it). `POST /v1/ledger/incomes` posts `Dr Bank-or-Cash / Cr Income`; `GET /v1/reports/monthly-incomes` feeds a Dashboard `Out of pocket | Income` toggle. `docs/incomes-support/slice-1-record-income-and-dashboard-toggle.md`. 390 tests.

## Phase 46 — Incomes: the Money Flow table (2026-09-25)

`vw_ledger_money_flow` + `GET /v1/reports/money-flow?month=` render an accounting-style monthly table reusing the Out-of-pocket/Income filters bit-for-bit; reversed pairs hidden entirely. `docs/incomes-support/slice-2-money-flow-table.md`. 402 tests.

## Phase 47 — Incomes: undo + double-reversal guard (2026-09-25)

A global double-reversal guard in `ReverseTransactionHandler` (new `Ledger.TransactionAlreadyReversed`, 409) closes the hole a Money-Flow Undo button would otherwise have exposed — no new endpoint, client reuses the existing reversal call. `docs/incomes-support/slice-3-undo-income.md`. Closes the incomes-support initiative. 407 tests.

## Phase 48 — Partial creditor payments: foundation (2026-09-28)

New `CreditorInstallmentPayment` entity (payment-per-row, not a single stamp) backs `Installment.ApplyPayment`/`UndoLastPayment`; `PaidOnUtc` narrows to mean "fully paid"; every creditor figure (detail groups, payables Due now/Total/per-account) now computed from remaining amounts; `POST /v1/financing/creditor-installments/{id}/pay` accepts an optional partial amount; undoing an empty installment is a 409 `NoPaymentToUndo`. `docs/partial-creditor-payments/slice-1-foundation-partial-installment.md`. Financing 170 / client 363.

## Phase 49 — Partial creditor payments: pay-from-expense (2026-09-28)

`PayCreditorExpenseCommand` + `POST /v1/financing/creditor-purchases/{id}/pay` pays a whole purchase (full or partial) via a new pure `CreditorPaymentWaterfall.Allocate` (fills installments in `Sequence` order, last one partial). `docs/partial-creditor-payments/slice-2-pay-expense.md`. Financing 182 / client 363. Slices 3 (partial full-debt) and 4 (party share) of `docs/partial-creditor-payments/` remain.

## Phase 50 — Partial creditor payments: partial full-debt (2026-09-28)

`PayCreditorFullDebtCommand` gains optional `AmountMinorUnits`/`CurrencyCode`: `null` keeps settling everything in every currency (unchanged); a set amount requires a currency (`InvalidCurrencyCode` otherwise) and fills that currency's remaining installments across every purchase, oldest due-month first (tie-break: purchase date, then `Sequence`), reusing `CreditorPaymentWaterfall.Allocate` from Phase 49. `docs/partial-creditor-payments/slice-3-partial-full-debt.md`. Financing 187. Slice 4 (party share) of `docs/partial-creditor-payments/` remains.

## Phase 51 — Partial creditor payments: party share (2026-09-28)

The one ledger-touching creditor payment in the initiative: `PayCreditorInstallmentPartyShareCommand` settles a split participant's fixed PhantomPenny share (`CreditorSplitReceivableCalculator.PartyShares`, shared with the read side so the offered and settled amounts can't disagree) via `IPartiesApi.SettleCurrentAccountAsync` (`Dr Bank / Cr Receivable_party`), then records it as a payment on the installment (`Installment.ApplyPayment`, Phase 48); best-effort `ILedgerApi.ReverseTransactionAsync` compensation if the installment save fails after settlement succeeds (no outbox saga yet — `// ponytail:` marked). `POST /v1/financing/creditor-installments/{id}/pay-party`. `GetCreditorDetailHandler` calls `IPartiesApi.ListPartiesAsync` once per request to expose per-installment `PartyShares` (name/share/paid), gated to split-accrued non-reversed installments. `UnpayCreditorInstallmentHandler` reverses the settlement transaction when undoing a party-paid row. `docs/partial-creditor-payments/slice-4-party-part.md`. Closes the partial-creditor-payments initiative. Financing 200 / 452 total.

# Client DESIGN — Personal Finance (Angular 20)

> Technical design for wiring the Angular 20 client to the PersonalFinance `.NET 10` API.
> Counterpart of `app/api/docs/DESIGN.md`. Governed by `.claude/CLAUDE.md` and
> `.claude/rules/typescript-frontend-style.md` — every symbol below obeys that style guide.
> **UI/visual design is out of scope** (separate task); this document defines structure, types,
> services, HTTP wiring, and configuration only.

## 1. Principles

- **Feature-based / screaming structure.** Folders name business capabilities (instruments,
  financing, subscriptions, parties, reports), mirroring the API's bounded contexts — not technical
  layers.
- **Signals-first, zoneless.** State lives in signals; derived state in `computed()`. No `effect()`
  for data flow. Templates read signals by invocation; **no async pipe**.
- **`inject()` DI, standalone, `OnPush`** on every component. `standalone: true` is never written
  (it is the default).
- **Container / presentational split.** This task defines container **pages** and the signal state
  they hold. Presentational leaves are placeholders for the UI task.
- **Hand-written types.** One `type` per file in `types/` folders, mirroring the API DTOs by hand.
  `/openapi/v1.json` is the reference to mirror — **no codegen**. Money is a branded type.
- **No business logic in the client.** Cycles, splits, rounding, and invariants are the API's.

## 2. Folder structure

```
src/app/
  core/
    http/
      base-url.interceptor.ts        -> baseUrlInterceptor
      problem-details.interceptor.ts -> problemDetailsInterceptor
    types/
      money.ts             -> Money            (branded minor-units)
      currency-code.ts     -> CurrencyCode     ('ARS' | 'USD')
      iso-instant.ts       -> IsoInstant       (ISO-8601 UTC string)
      iso-date.ts          -> IsoDate          (date-only 'YYYY-MM-DD' string)
      problem-details.ts   -> ProblemDetails   (wire error envelope)
      app-error.ts         -> AppError         (domain error the app handles)
    money/
      money.ts             -> fromMinorUnits / toMinorUnits / formatMoney / formatArs (pure fns;
                                formatArs is a thin ARS-only shim over formatMoney)
    health/
      health-service.ts    -> HealthService
  features/
    instruments/
      instruments-service.ts
      types/  (instrument.ts, instrument-type.ts, create-instrument.ts, instrument-created.ts)
      pages/  instruments-page/
      instruments.routes.ts
    ledger/
      ledger-service.ts
      types/  (post-transaction.ts, transaction-line.ts, direction.ts,
               post-transaction-result.ts, reverse-transaction-result.ts, account-balance.ts,
               transaction-row.ts)
      pages/  transactions-page/, reverse-movement-page/
      ledger.routes.ts
    financing/
      financing-service.ts
      types/  (create-payment-plan.ts, split-participant.ts, create-payment-plan-result.ts,
               monthly-statement.ts, monthly-statement-installment.ts, monthly-statement-summary.ts,
               pay-statement.ts, pay-statement-result.ts, pay-installment.ts, pay-installment-result.ts,
               card-future-schedule.ts, card-future-schedule-row.ts)
      pages/  statements-page/, statement-page/, load-expense-page/
      financing.routes.ts
    subscriptions/
      subscriptions-service.ts
      types/  (create-subscription.ts, frequency.ts, subscription-result.ts,
               active-subscription.ts, subscription-status.ts)
      pages/  subscriptions-page/
      subscriptions.routes.ts
    parties/
      parties-service.ts
      types/  (create-party.ts, party-result.ts, current-account-balance.ts,
               current-account-timeline-row.ts, register-shared-expense.ts,
               shared-expense-participant.ts, shared-expense-result.ts,
               settle-current-account.ts, settlement-result.ts)
      pages/  parties-page/, party-detail-page/, shared-expense-page/
      parties.routes.ts
    reports/
      reports-service.ts
      types/  (monthly-expense-row.ts, card-due-row.ts, party-debt-row.ts, party-timeline-row.ts)
      pages/  dashboard-page/
      reports.routes.ts
  environments/
    environment.ts             (prod placeholder)
    environment.development.ts  (apiUrl: 'http://localhost:5000/v1')
  app.config.ts
  app.routes.ts
```

Naming per the style guide: files kebab-case, exports PascalCase with **no** `Component`/`Service`
suffix in the class name where the guide omits it (services keep the `Service` suffix per the guide's
own example, e.g. `FinancingService`; components drop the suffix, e.g. `DashboardPage`). Types carry
no `I-` prefix.

## 3. API type model (hand-written, exact field mapping)

.NET serializes with **camelCase**. Amounts are `long` minor units → TypeScript `Money` (a branded
`number`). C# `DateTimeOffset` → `IsoInstant`; C# `DateOnly` / date strings → `IsoDate`. GUIDs →
`string`. Below, each client type mirrors one DTO exactly (source of truth in parentheses).

**Core value types** (`core/types/`)
- `Money = number & { readonly __brand: 'Money' }` — minor units; never floated.
- `CurrencyCode = 'ARS' | 'USD'` — a closed set, mirroring the API's `Currency.FromCode`. No FX
  conversion exists anywhere in the client (or the API): currency lives on the record, never the
  account, and any total over rows spanning currencies must group by `currencyCode` before summing
  — never blend two currencies into one figure (`docs/dollar-support/00-overview.md`, `app/api/docs/DESIGN.md`
  D14).
- `IsoInstant = string` (ISO-8601 UTC), `IsoDate = string` (`YYYY-MM-DD`).

**Instruments** (`PostInstrumentDto` / `InstrumentCreatedDto`)
- `InstrumentType = 'debit' | 'credit' | 'cash'`
- `CreateInstrument = { type: InstrumentType; name: string; cutoffDate?: number }` (cutoff required
  when `type === 'credit'`, 1–31)
- `InstrumentCreated = { id: string; type: InstrumentType }`

**Ledger** (`PostTransactionDto`, `ReverseTransactionResultDto`, `AccountBalanceDto`)
- `Direction = 'Debit' | 'Credit'`
- `TransactionLine = { accountId: string; direction: Direction; amountMinorUnits: Money }`
- `PostTransaction = { lines: TransactionLine[]; postedOnUtc: IsoInstant; splitReferenceId?: string;
  installmentReferenceId?: string; description?: string }`
- `PostTransactionResult = { transactionId: string }`
- `ReverseTransactionResult = { reversalTransactionId: string; originalTransactionId: string;
  compensatingEntryPosted: boolean }`
- `AccountBalance = { accountId: string; balanceMinorUnits: Money; currencyCode: CurrencyCode;
  formatted: string }`
- `DebitExpenseParticipant = { partyId: string; weight: number }`
- `RecordDebitExpense = { amountMinorUnits: Money; sourceInstrumentId: string; categoryName: string;
  purchaseDate: IsoDate; description: string; currencyCode: CurrencyCode;
  split?: DebitExpenseParticipant[] }` (`currencyCode` added in `docs/dollar-support/slice-1-…` —
  the user picks ARS or USD on Load Expense's debit mode)
- `RecordDebitExpenseResult = { id: string }`
- `listExpenseCategories` returns a bare `string[]` — the service `map`-unwraps the
  `{ rows: [{ name }] }` envelope to category names

**Financing** (`CreatePaymentPlanDto`, `MonthlyStatementDetailDto`, `PayStatementDto`,
`CardFutureScheduleDto`)
- `SplitParticipant = { partyId: string; weight: number }`
- `CreatePaymentPlan = { amountMinorUnits: Money; cardId?: string; installmentCount: number;
  purchaseDate: IsoDate; description: string; split?: SplitParticipant[]; creditorId?: string;
  creditorAccountId?: string; bankAccountId?: string }` — `cardId` xor `creditorId`+`creditorAccountId`
  by payment mode; `bankAccountId` only when a card purchase is back-dated (funds the API's
  retroactive settlement of the elapsed installments — `docs/backdated-expenses/slice-1-card-backdating.md`)
- `CreatePaymentPlanResult = { paymentPlanId: string }`
- `MonthlyStatementInstallment = { planId: string; installmentId: string; sequence: number;
  installmentCount: number; purchaseDate: IsoDate; cycleYear: number; cycleMonth: number;
  amountMinorUnits: Money; isReversed: boolean; reversalTransactionId: string | null;
  isPaid: boolean; paidOnUtc: IsoInstant | null }`
- `MonthlyStatement = { statementId: string; cardId: string; cardName: string; cycleYear: number;
  cycleMonth: number; amountDueMinorUnits: Money; isPaid: boolean; paidOnUtc: IsoInstant | null;
  installments: MonthlyStatementInstallment[] }`
- `PayStatement = { bankAccountId: string; paidOnUtc: IsoInstant }`
- `PayStatementResult = { statementId: string }`
- `PayInstallment = { bankAccountId: string; paidOnUtc: IsoInstant }`
- `PayInstallmentResult = { installmentId: string }`
- `CardFutureScheduleRow = { planId: string; installmentId: string; sequence: number;
  cycleYear: number; cycleMonth: number; amountMinorUnits: Money }`
- `CardFutureSchedule = { cardId: string; rows: CardFutureScheduleRow[] }`
- `CreditorPayableAccount = { accountId: string; label: string; outstandingMinorUnits: Money }`
- `CreditorPayableRow = { creditorId: string; creditorName: string; dueNowMinorUnits: Money;
  totalOwedMinorUnits: Money; nextDueDate: IsoDate | null; accounts: CreditorPayableAccount[] }` —
  `dueNowMinorUnits` = Σ unpaid, non-reversed cuotas due by the current creditor cycle (arrears
  folded in); `totalOwedMinorUnits` = Σ all unpaid, non-reversed (future included); paid excluded
  from both (`docs/owed-to-creditors/slice-1-current-cycle-outstanding.md`). `nextDueDate` and the
  per-account `outstandingMinorUnits` are still over all non-reversed cuotas.
- `creditorPayables` returns `CreditorPayableRow[]` — the service casts + unwraps the `{ rows }` envelope
- `CreditorInstallmentRow = { installmentId: string; sequence: number; installmentCount: number;
  amountMinorUnits: Money; dueYear: number; dueMonth: number; isPaid: boolean; isReversed: boolean;
  status: 'overdue' | 'due' | 'future' | 'paid' | 'reversed'; paidMinorUnits: Money;
  remainingMinorUnits: Money; hasPayments: boolean }` — `dueYear`/`dueMonth` are the payment
  month (stored cycle + 1); `status` is server-computed; `paidMinorUnits`/`remainingMinorUnits`/
  `hasPayments` are derived from the installment's payment rows (`docs/partial-creditor-payments/
  slice-1-foundation-partial-installment.md`) — Pay shows on `remainingMinorUnits > 0`, Undo on
  `hasPayments`, independently (a partial row shows both)
- `CreditorPurchaseGroup = { planId: string; description: string; purchaseDate: IsoDate;
  totalMinorUnits: Money; outstandingMinorUnits: Money; currencyCode: CurrencyCode;
  installments: CreditorInstallmentRow[] }` — `totalMinorUnits` = Σ non-reversed cuotas of the plan,
  `outstandingMinorUnits` = Σ unpaid non-reversed (now = Σ remaining); `currencyCode` (from the
  underlying `PaymentPlan.Currency`) feeds `formatMoney` on the detail page and the pay dialog —
  creditor plans can be USD since dollar-support
- `CreditorDetail = { creditorId: string; creditorName: string; purchases: CreditorPurchaseGroup[] }` —
  one creditor's debt grouped by purchase, groups newest-first
  (`docs/owed-to-creditors/slice-2-creditor-detail-view.md`)
- `creditorDetail(creditorId)` returns `CreditorDetail` — the service casts the bare object (no `{ rows }`
  envelope); an unknown creditor is a `404 Financing.CreditorNotFound` the detail page renders as a
  friendly not-found state
- `PayCreditorInstallment = { amountMinorUnits: Money | null }` — the pay-dialog's output/POST body,
  `null` = pay whatever remains (`docs/partial-creditor-payments/slice-1-foundation-partial-installment.md`)
- `PayCreditorExpense = { amountMinorUnits: Money | null }` — same shape as `PayCreditorInstallment`,
  the pay-dialog's output/POST body in `expense` mode: `null` pays everything remaining on the whole
  purchase, a custom amount fills its cuotas in `Sequence` order, the last one taking the leftover as a
  partial (`docs/partial-creditor-payments/slice-2-pay-expense.md`)
- `PayCreditorExpenseResult = { settledCount: number }` — the count of cuotas newly fully settled,
  same meaning as `PayCreditorFullDebtResult.settledCount`; `creditor-detail-page` reuses its
  "N cuota(s) settled." line for both
- `PayCreditorFullDebt = { amountMinorUnits: Money | null; currencyCode: CurrencyCode | null }` —
  the pay-dialog's output/POST body for `pay-full`; `{ null, null }` pays everything remaining in
  every currency (unchanged). This same shape is what `creditor-pay-dialog`'s `confirm` output
  emits for **every** mode now, not just `full-debt` — `currencyCode` is simply always `null` for
  `installment`/`expense`, and `creditor-detail-page` strips it back down to
  `{ amountMinorUnits }` before calling those two services, so their request bodies stay exactly
  what the API expects (`docs/partial-creditor-payments/slice-3-partial-full-debt.md`)
- `CreditorOutstandingByCurrency = { currencyCode: CurrencyCode; outstandingMinorUnits: Money }` —
  one entry per currency the creditor currently owes in, summed across purchase groups; the page's
  `outstandingByCurrency` computed feeds the dialog's new `currencies` input
- **`creditor-pay-dialog` gains a third mode, `full-debt`.** The creditor-detail-page's old inline
  "Settle every remaining cuota…" confirm (`confirmingFullDebt` signal + its own template block)
  is gone — "Pay full debt" now sets `payTarget = { kind: 'full-debt' }` like the other two modes.
  A currency `<select>` (a real `formControlName`, mirrored into a signal like `amount`/`choice`
  already were, since the app is zoneless) appears **only** when `currencies().length > 1`; picking
  a currency changes the "$X remaining" text, the custom-amount max, and the currency sent on
  confirm. With a single currency the select stays hidden and the dialog falls back to the
  existing `remainingMinorUnits`/`currency` inputs directly. The "Pay in full" radio lists every
  currency's total (`$X + US$Y`) when there's more than one.

**Subscriptions** (`CreateSubscriptionDto`, `SubscriptionResultDto`, `ActiveSubscriptionsDto`)
- `Frequency = 'monthly' | 'weekly' | 'daily' | 'annually'`
- `CreateSubscription = { name: string; amountMinorUnits: Money; category: string;
  fundingAccountId: string; frequency: Frequency; anchorDay: number }`
- `SubscriptionResult = { id: string }`
- `ActiveSubscription = { subscriptionId: string; name: string; amountMinorUnits: Money;
  category: string; frequency: Frequency; anchorDay: number; nextDueDate: IsoDate;
  status: SubscriptionStatus }`
- `SubscriptionStatus = 'paid' | 'overdue' | 'upcoming'` (`docs/subscriptions-rework/` Slice 1,
  `app/api` Phase 38) — a server-derived read-model field, already lowercase (no enum
  serialization involved, unlike `Frequency` below), rendered as a badge on the Subscriptions
  page.
- **Reconciliation (client D12, Phase 2):** the API's `RecurrenceFrequency` enum currently
  implements `Monthly` only, and `GET /v1/subscriptions/active` serialises it PascalCase as
  `"Monthly"`. The wider `Frequency` union above is aspirational. As shipped, the client types
  `Frequency = 'monthly'`, `SubscriptionsService.listActive` normalises the `"Monthly"`
  response to `'monthly'`, and the create form renders `frequency` as a fixed, disabled
  control. The seam reopens here if the API adds `Weekly` / `Annually`.

**Parties** (`CreatePartyDto`, `RegisterSharedExpenseDto`, `SettleCurrentAccountDto`,
`CurrentAccountDTO`)
- `CreateParty = { name: string }`, `PartyResult = { id: string }`
- `SharedExpenseParticipant = { partyId: string; weight: number }`
- `RegisterSharedExpense = { description: string; totalMinorUnits: Money; expenseAccountId: string;
  fundingAccountId: string; incurredOnUtc: IsoInstant; participants: SharedExpenseParticipant[];
  currencyCode: CurrencyCode }` (`currencyCode` added in `docs/dollar-support/slice-4-parties.md`)
- `SharedExpenseResult = { splitReferenceId: string }`
- `SettleCurrentAccount = { amountMinorUnits: Money; bankAccountId: string; settledOnUtc: IsoInstant;
  currencyCode: CurrencyCode }` — the settle form constrains the offered `currencyCode` to whatever
  the party's balances actually carry a positive amount in
- `SettlementResult = { ledgerTransactionId: string }`
- `PartyCurrencyBalance = { currencyCode: CurrencyCode; balanceMinorUnits: Money }`
- `CurrentAccountBalance = { partyId: string; name: string; balances: PartyCurrencyBalance[] }` —
  reshaped from a single scalar to one row per currency the party has ever moved money in
  (`docs/dollar-support/slice-4-parties.md`); a party with no movements in a currency has no row
  for it, so an all-settled party has an empty `balances` array, never a synthesized `$0` row
- `CurrentAccountTimelineRow = { movementOnUtc: IsoInstant; description: string;
  deltaMinorUnits: Money; runningBalanceMinorUnits: Money; currencyCode: CurrencyCode }` — the
  running balance is windowed per `(accountId, currencyCode)` server-side, so it never crosses
  currencies
- `FuturePartyShare = { cycleYear: number; cycleMonth: number; shareMinorUnits: Money;
  currencyCode: CurrencyCode; sourceLabel: string }` (`GET /v1/parties/{id}/future-shares`) and
  `PendingSharesByPartyRow = { partyId: string; scheduledCount: number;
  scheduledTotalMinorUnits: Money; currencyCode: CurrencyCode }` (`GET /v1/parties/pending-shares`)
  — `currencyCode` was a bare `string` placeholder on both and is now the same closed
  `CurrencyCode` union, carrying real per-currency values

**Reporting** (`ReportingDTOs`)
- `MonthlyExpenseRow = { month: string; category: string; amountMinorUnits: Money;
  currencyCode: CurrencyCode }` (`month` is `'YYYY-MM'`)
- `CardDueRow = { bucket: 'Accrued' | 'Future'; card: string; cycleYear: number | null;
  cycleMonth: number | null; amountMinorUnits: Money; currencyCode: CurrencyCode;
  cardId: string | null }`
- `PartyDebtRow = { partyId: string; partyName: string; netBalanceMinorUnits: Money;
  currencyCode: CurrencyCode }`
- `PartyTimelineRow = { movementOnUtc: IsoInstant; description: string; deltaMinorUnits: Money;
  runningBalanceMinorUnits: Money; currencyCode: CurrencyCode }`
- `MonthlyIncomeRow = { month: string; amountMinorUnits: Money; currencyCode: CurrencyCode }`
  (`GET /v1/reports/monthly-incomes`, `docs/incomes-support/slice-1-…` — missing from this list
  since that slice, added here alongside `MoneyFlowRow` below)
- `MoneyFlowRow = { transactionId: string; date: string; description: string; accountName: string;
  kind: 'Income' | 'Outcome'; amountMinorUnits: Money; currencyCode: CurrencyCode }`
  (`GET /v1/reports/money-flow?month=`, `docs/incomes-support/slice-2-money-flow-table.md`) —
  `transactionId` rode on the row unused by Slice 2's own view, exactly so Slice 3's Undo button
  (`slice-3-undo-income.md`, now built) could target `LedgerService.reverse(transactionId)`
  without a second read; `kind`/`amountMinorUnits` are already resolved server-side (never both
  a non-zero income and outcome on the same row)

## 4. Services

One `@Injectable({ providedIn: 'root' })` per bounded context, each `inject(HttpClient)` and a
`baseUrl` from `environment`, returning `Observable<DomainType>`. Signatures:

- **InstrumentsService** — `list(): Observable<Instrument[]>` (unwraps the `{ rows }` envelope);
  `create(body: CreateInstrument): Observable<InstrumentCreated>`
- **LedgerService** — `postTransaction(body: PostTransaction): Observable<PostTransactionResult>`;
  `listTransactions(filter?: { accountId?; from?; to? }): Observable<TransactionRow[]>` (unwraps the `{ rows }` envelope);
  `reverse(transactionId: string): Observable<ReverseTransactionResult>`;
  `getAccountBalance(accountId: string): Observable<AccountBalance>`;
  `recordDebitExpense(body: RecordDebitExpense): Observable<RecordDebitExpenseResult>`;
  `listExpenseCategories(): Observable<string[]>` (unwraps the `{ rows }` envelope to names)
- **FinancingService** — `createPaymentPlan(body: CreatePaymentPlan): Observable<CreatePaymentPlanResult>`;
  `getStatement(id: string): Observable<MonthlyStatement>`;
  `payStatement(id: string, body: PayStatement): Observable<PayStatementResult>`;
  `payInstallment(id: string, body: PayInstallment): Observable<PayInstallmentResult>`;
  `getFutureSchedule(cardId: string): Observable<CardFutureSchedule>`;
  `listStatements(cardId: string): Observable<MonthlyStatementSummary[]>` (unwraps the `{ rows }` envelope)
- **SubscriptionsService** — `create(body: CreateSubscription): Observable<SubscriptionResult>`;
  `cancel(id: string): Observable<void>`; `listActive(): Observable<ActiveSubscription[]>`
  (unwraps the `{ rows }` envelope)
- **PartiesService** — `create(body: CreateParty): Observable<PartyResult>`;
  `registerSharedExpense(body: RegisterSharedExpense): Observable<SharedExpenseResult>`;
  `settle(partyId: string, body: SettleCurrentAccount): Observable<SettlementResult>`;
  `getBalance(partyId: string): Observable<CurrentAccountBalance>`;
  `getTimeline(partyId: string): Observable<CurrentAccountTimelineRow[]>`
  (unwraps the `{ rows }` envelope — see D14 in `.claude/TASK.md` Phase 3)
- **ReportsService** — `monthlyExpenses(month?: string): Observable<MonthlyExpenseRow[]>`;
  `cardDueByMonth(): Observable<CardDueRow[]>`; `debtSummary(): Observable<PartyDebtRow[]>`;
  `partyTimeline(partyId: string): Observable<PartyTimelineRow[]>`
- **HealthService** — `check(): Observable<...>` (`GET /health`, degradation aware)

Services that hit `{ rows: [...] }` list endpoints unwrap to the array via `map`, so components
receive plain arrays. All routes are built from `ApiRoutes` constants mirrored client-side.

## 5. HTTP wiring & error model

**`app.config.ts`**
```ts
provideHttpClient(
  withFetch(),
  withInterceptors([baseUrlInterceptor, problemDetailsInterceptor]),
)
```

**`baseUrlInterceptor`** — prefixes relative request URLs with `environment.apiUrl`
(`http://localhost:5000/v1` in dev). Keeps services free of the host.

**`problemDetailsInterceptor`** — catches `HttpErrorResponse` and rethrows a typed `AppError`.
The API envelope is RFC-9457 `ProblemDetails` and — because it is built with
`TypedResults.Problem(extensions:)` — the `code` and any metadata are **root-level members**, not
nested. Exact wire shape:
```json
{
  "type": "https://tools.ietf.org/html/rfc9457",
  "title": "The request conflicts with the current state of the resource.",
  "status": 409,
  "detail": "Ledger.CannotReverseAReversal",
  "code": "Ledger.CannotReverseAReversal"
}
```
- `ProblemDetails = { type?: string; title?: string; status?: number; detail?: string; code?: string;
  [key: string]: unknown }`
- `AppError = { code: string; title: string; detail: string; status: number;
  metadata: Record<string, unknown> }`
- The interceptor reads `error.error.code` (root), falls back to `status`-derived defaults if the
  body is missing (network / 5xx), and preserves extra root keys as `metadata`.
- **Status meaning** (from `ErrorHttpStatusHelper`): `*.NotFound` → 404; `AlreadyPaid` /
  `AlreadyAccrued` / `AlreadyReversed` / `NotActive` / `SettlementExceedsBalance` → 409; `Invalid*` /
  `NonPositive*` and other domain violations → 422; `Instruments.UnknownType` / `Request.Malformed` →
  400. Views key user-facing messages off `code`, not off `detail` text.

## 6. State & reactivity

- Container pages own state as `WritableSignal`s (double-annotated per the guide), derive with
  `computed()`, and render by signal invocation.
- Observables are confined to service I/O. Subscriptions in pages use the guide's single cleanup
  pattern (`private destroy$ = new Subject<void>()` + `takeUntil(this.destroy$)` +
  `ngOnDestroy`). No async pipe.
- **Reactive forms** for every input view (Load expense, Instruments, Pay, Shared expense,
  Subscription). Forms built in an `initXForm()` method via `FormBuilder`; validators as pure
  functions; error copy via an `ErrorConfig` map. Money inputs are entered in major units and
  converted with `toMinorUnits` at submit.

## 7. Eventual consistency & scheduler awareness

- **Split registration (API D8):** after `createPaymentPlan` with a `split`, the plan id returns
  before the Parties receivable is posted (async Outbox). The Load-expense page confirms the plan
  immediately, then reconciles by polling `PartiesService.getBalance` with a short bounded retry
  before showing the party's updated balance. A shared `pollUntil` helper (RxJS
  `timer`+`switchMap`+`take`/`retry`) lives in `core/http`. **Card and creditor-financed splits skip
  the poll** — their co-borrower receivable accrues at the installment's due month (a scheduler, not a
  synchronous post), so `reconcile(participants, mode)` seeds the table and marks each participant
  *scheduled* before the loop when `mode === 'card' || mode === 'creditor'`
  (`docs/parties-card-split/slice-1-reconcile-loop-fix.md` +
  `docs/cycle-due-month/slice-2-creditor-split-parity.md`). Only debit/cash still polls.
- **Reversal credit (API D12):** the Statement page states that a reversed paid installment yields a
  card credit netted on the **next** statement (not cash back).
- **Scheduler-driven accrual/renewal (API D6):** the client cannot trigger these; it reflects the
  latest state on load. No optimistic UI for scheduled effects.

## 8. Instrument list (was: local registry — removed in Phase 12)

The API now serves `GET /v1/instruments` (a merge of Ledger debit/cash accounts and Financing
credit cards, each row `{ id, type, name, cutoffDate }`). `InstrumentsService.list()` unwraps the
`{ rows }` envelope; every form that offers a card or funding account loads it into a local
`WritableSignal<Instrument[]>` in `ngOnInit` and derives its `<select>` options with a `computed()`
(`type === 'credit'` for card pickers, `type === 'debit'` for bank pickers, all types for the
Subscriptions and Shared-expense funding pickers — D13/D17). `InstrumentsPage` re-fetches the list
after a successful create.

The former `InstrumentRegistryService` — a `localStorage`-persisted signal of instruments created
in this browser — was the gap-#1 stopgap. It and `core/types/registered-instrument.ts` were
deleted in Phase 12 (**D21**); the `Instrument` type lives at `features/instruments/types/instrument.ts`.

## 9. Endpoint → service → view traceability

| # | Method | Route | Service method | View |
|---|--------|-------|----------------|------|
| 1 | POST | `/v1/instruments` | `InstrumentsService.create` | Instruments setup |
| 2 | GET | `/v1/instruments` | `InstrumentsService.list` | Instruments setup + every card/funding `<select>` (Load expense, Statement pay, Subscriptions, Party settlement, Shared expense) |
| 3 | POST | `/v1/ledger/transactions` | `LedgerService.postTransaction` | (low-level; internal) |
| 4 | POST | `/v1/ledger/transactions/{id}/reversal` | `LedgerService.reverse` | Reverse movement; Money Flow's per-row Undo (income rows only, `docs/incomes-support/slice-3-undo-income.md`) |
| 5 | GET | `/v1/ledger/accounts/{id}/balance` | `LedgerService.getAccountBalance` | (detail widgets) |
| 6 | GET | `/v1/ledger/transactions` | `LedgerService.listTransactions` | Transactions feed (account + date filter → row → Reverse movement) |
| 7 | POST | `/v1/financing/payment-plans` | `FinancingService.createPaymentPlan` | Load expense |
| 8 | GET | `/v1/financing/statements/{id}` | `FinancingService.getStatement` | Statement detail (installment row → Reverse movement) |
| 9 | POST | `/v1/financing/statements/{id}/pay` | `FinancingService.payStatement` | Statement detail |
| 10 | GET | `/v1/financing/cards/{id}/future-schedule` | `FinancingService.getFutureSchedule` | Statement detail |
| 11 | GET | `/v1/financing/cards/{id}/statements` | `FinancingService.listStatements` | Statements list (card picker → row → Statement detail) |
| 12 | POST | `/v1/subscriptions` | `SubscriptionsService.create` | Subscriptions |
| 13 | DELETE | `/v1/subscriptions/{id}` | `SubscriptionsService.cancel` | Subscriptions |
| 14 | GET | `/v1/subscriptions/active` | `SubscriptionsService.listActive` | Subscriptions |
| 15 | POST | `/v1/parties` | `PartiesService.create` | Parties |
| 16 | POST | `/v1/parties/shared-expenses` | `PartiesService.registerSharedExpense` | Party detail / Shared expense |
| 17 | POST | `/v1/parties/{id}/settlements` | `PartiesService.settle` | Party detail |
| 18 | GET | `/v1/parties/{id}/balance` | `PartiesService.getBalance` | Party detail |
| 19 | GET | `/v1/parties/{id}/timeline` | `PartiesService.getTimeline` | (parity only — not wired to a view; D16) |
| 20 | GET | `/v1/reports/monthly-expenses` | `ReportsService.monthlyExpenses` | Dashboard |
| 21 | GET | `/v1/reports/card-due-by-month` | `ReportsService.cardDueByMonth` | Dashboard |
| 22 | GET | `/v1/reports/parties/{id}/timeline` | `ReportsService.partyTimeline` | Party detail (timeline row → Reverse movement) |
| 23 | GET | `/v1/reports/parties/debt-summary` | `ReportsService.debtSummary` | Parties list (balances only — merged with #28) |
| 24 | GET | `/v1/financing/creditor-payables` | `FinancingService.creditorPayables` | Owed to creditors list |
| 25 | GET | `/health` | `HealthService.check` | (status indicator) |
| 26 | GET | `/v1/expense-categories` | `LedgerService.listExpenseCategories` | Load expense (debit-cash category `<datalist>`) |
| 27 | POST | `/v1/ledger/expenses` | `LedgerService.recordDebitExpense` | Load expense (debit-cash mode) |
| 28 | GET | `/v1/parties` | `PartiesService.list` | Parties list (roster — merged with #23 for balances) + Load expense (split party picker) |
| 29 | GET | `/v1/parties/pending-shares` | `PartiesService.pendingShares` | Parties list (pending-schedule count per party — merged with #23/#28 so a $0-now scheduled party reads "Nothing owed yet · N scheduled") |
| 30 | GET | `/v1/financing/purchases/recent` | `FinancingService.recentPurchases` | Recent purchases list (§3.9; each row carries a derived `paidInstallmentCount` + next-payment `nextDueYear`/`nextDueMonth` + `pendingAmountMinorUnits`, rendered "N/M paid · $X pending · next: `<month>`" or "Fully paid") |
| 31 | GET | `/v1/financing/creditor-payables/{creditorId}` | `FinancingService.creditorDetail` | Owed to creditors — creditor detail (§3.10; one creditor's debt grouped by purchase; unknown creditor → 404 `Financing.CreditorNotFound` → friendly not-found state) |
| 32 | POST | `/v1/financing/creditor-installments/{id}/pay` | `FinancingService.payCreditorInstallment` | Creditor detail — per-cuota **Pay**, opened via the shared pay dialog (display-only; body `{ amountMinorUnits: Money \| null }`, `null` = pay remaining; card installment → 409 `Financing.NotACreditorInstallment`; 0/negative or over-remaining → 400 `InvalidPaymentAmount`/`PaymentExceedsRemaining`) |
| 33 | POST | `/v1/financing/creditor-installments/{id}/unpay` | `FinancingService.unpayCreditorInstallment` | Creditor detail — per-cuota **Undo**, removes the last payment (empty `{}` body; shown only when the cuota has at least one payment; nothing to undo → 409 `Financing.NoPaymentToUndo`, no longer a silent no-op) |
| 34 | POST | `/v1/financing/creditor-payables/{creditorId}/pay-full` | `FinancingService.payCreditorFullDebt` | Creditor detail — **Pay full debt**, now via `creditor-pay-dialog`'s `full-debt` mode; body `{ amountMinorUnits, currencyCode }`, `{ null, null }` stamps every unpaid, non-reversed cuota in every currency (unchanged); a set amount + currency fills that currency's remaining cuotas oldest due-month first across every purchase; returns `{ settledCount }`; zero settleable → `0`; unknown creditor → 404; missing/invalid currency with a set amount → 422 `Financing.InvalidCurrencyCode` |
| 35 | POST | `/v1/ledger/incomes` | `LedgerService.recordIncome` | Record income (§3.12) |
| 36 | GET | `/v1/reports/monthly-incomes` | `ReportsService.monthlyIncomes` | Dashboard — Income side of the `Out of pocket \| Income` toggle (§3.12) |
| 37 | GET | `/v1/reports/money-flow` | `ReportsService.moneyFlow` | Money Flow table (§3.13; required `month` param, one row per money movement, `kind`-driven signed rendering) |
| 38 | POST | `/v1/financing/creditor-purchases/{paymentPlanId}/pay` | `FinancingService.payCreditorExpense` | Creditor detail — per-purchase **Pay expense**, opened via the shared pay dialog in `expense` mode (display-only; body `{ amountMinorUnits: Money \| null }`, `null` = pay everything remaining, a custom amount fills that purchase's cuotas in sequence order; card plan → 409 `Financing.NotACreditorInstallment`; 0/negative or over-remaining → 400; returns `{ settledCount }`) |

`POST /v1/ledger/accounts` (dev-only account shortcut) is intentionally **not** wired — it is
removed outside Development.

Endpoints 19 and 22 (`/v1/parties/{id}/timeline`, `/v1/reports/parties/{id}/timeline`) both
return a `{ rows: [...] }` envelope; their service methods unwrap to the array via `map`
(D14 — the design text had described endpoint 18 as returning the object as-is).

## 10. Configuration & tooling

- **Environments (new).** Add `src/app/environments/environment.ts` (prod placeholder) and
  `environment.development.ts` (`apiUrl: 'http://localhost:5000/v1'`), plus an `angular.json`
  `fileReplacements` entry for the production build. CORS already allows `http://localhost:4200`
  (API D13), so `ng serve` works against a local API with no proxy.
- **Routing.** `app.routes.ts` uses `loadChildren` per feature `*.routes.ts` (lazy) — Fase-1
  features first.
- **Tailwind (config groundwork).** Not installed. It must be added before the UI task, but tokens,
  theme, and components belong to that task. This document only records the dependency.
- **Testing.** Jasmine + Karma are present. Each service gets an `HttpTestingController` spec
  asserting URL, verb, body, envelope unwrap, and `AppError` mapping. The `money` utilities and
  `problemDetailsInterceptor` get focused unit specs.

## 11. Open questions (see PRD §7)

1. ~~`GET /v1/instruments` (and card/account listing) — removes the `localStorage` stopgap (§8).~~
   **Resolved (Phase 12, D21):** the endpoint ships; `InstrumentRegistryService` and the
   `localStorage` stopgap are removed (§8).
2. ~~`GET /v1/financing/cards/{id}/statements` — makes the Statement pay flow self-navigable.~~
   **Resolved (Phase 12, D22):** the endpoint ships; the `Statements` nav item now opens a real
   `StatementsPage` (credit-card picker → statement rows → row opens `statement-page`), replacing the
   id-paste `StatementIndex` stopgap.
3. ~~A transactions feed (`GET /v1/ledger/transactions`) — enables a real history/reverse-picker view.~~
   **Resolved (Phase 12, D23):** the endpoint ships; the `Reverse` nav item now opens a real
   `TransactionsPage` (account + date-range filter → movement rows → per-row Reverse, disabled on a
   row that is itself a reversal or already reversed), replacing the id-paste `ReverseIndex` stopgap.
   **D20 closed too (Phase 12, D24):** the API now threads the reversible ledger transaction id onto
   the statement-installment rows (`reversalTransactionId`) and the party-timeline rows
   (`transactionId`), so `InstallmentsTable` and `TimelineTable` carry their own per-row Reverse
   button — the container navigates to `transactions/:id/reverse`.
4. App-level auth surface — deferred; no backing endpoint today.

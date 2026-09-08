# PersonalFinance — Angular client

Angular 20 SPA for the PersonalFinance API (`../api/`, .NET 10, acceptance-complete through
Fase 1). The client is a thin presentation layer: **no business logic** — cycles, splits,
rounding, and invariants belong to the API. It mirrors the API DTOs with hand-written types
(no codegen) and talks to `/v1` plus `/health`.

## Where the truth lives — read in this order

1. `docs/PRD.md` — product: 7 views, 3 Fases, cross-cutting rules, 4 API gaps.
2. `docs/DESIGN.md` — technical: folder tree §2, hand-written type model §3, services §4,
   HTTP + error model §5, state/forms §6, eventual consistency §7, local registry §8,
   endpoint traceability §9, config §10, open questions §11.
3. `docs/SYSTEM.md` — the interface system: direction & feel, tokens, typography, depth &
   spacing, focal pattern, component patterns, the pre-ship checks. This is the "separate UI
   task" DESIGN §10 defers to — now built out. **Every view has been redesigned against it**;
   hold to its values and extend the file when styling something new.
4. `.claude/TASK.md` — the phased build order (Phases 0 and 1 done; Phase 1 shipped the five
   Fase-1 views). Its "Deviations baked into Phases 0–3" and the per-phase "Completion notes"
   (D1–D11) record every intentional departure from PRD/DESIGN — check there before flagging
   drift.
5. `.claude/rules/typescript-frontend-style.md` — the detailed style guide (formatting,
   naming, typing, component/service shape, forms, cleanup). It is authoritative; the notes
   below are the high-frequency subset, not a replacement.

When two sources appear to conflict, surface it — do not silently pick one. Some conflicts
are already resolved deliberately and recorded as D1–D11 in TASK.md's per-phase "Completion
notes" (e.g. DESIGN §2's dotted `*.interceptor.ts` naming overrides the style guide's
hyphen table — that is D7, not drift).

## Stack

- **Angular 20.3**, standalone components, **zoneless** (`provideZonelessChangeDetection`),
  `ChangeDetectionStrategy.OnPush` everywhere.
- **TypeScript 5.9**, `strict` + `strictTemplates`, `noPropertyAccessFromIndexSignature`.
- **pnpm** — run the CLI as `pnpm ng …`.
- **RxJS 7.8**. **`@angular/build`** application builder; budgets 500 kB warn / 1 MB error.
- **Karma + Jasmine**. `zone.js/testing` is **not** loaded — no `fakeAsync` / `tick`; use
  RxJS `TestScheduler` marbles for timer-based tests.
- **Tailwind v4** wired into the build (CSS-first: `.postcssrc.json` + `@import 'tailwindcss'`
  in `styles.css`, no `tailwind.config.js` — D19). The design system is now built: a
  `@theme inline` token block over CSS custom properties in `styles.css`, IBM Plex Serif + Sans
  loaded via `<link>` in `index.html`, one committed light palette (**no dark mode**, no
  toggle). Bind the semantic utilities (`bg-paper`, `text-ink`, `text-stamp`, `border-rule`) —
  never a raw palette class (`bg-gray-100`) or hex. Full spec in `docs/SYSTEM.md`. **ESLint** —
  `angular-eslint@20` flat config (`eslint.config.js`), run via `pnpm ng lint`. **Client CI** —
  the `client-build-test` job in `/.github/workflows/ci.yml` runs lint + build + test. API gaps
  4.2 (`GET /v1/instruments`, D21), 4.3 (`GET /v1/financing/cards/{id}/statements`, D22) and
  4.4 (`GET /v1/ledger/transactions`, D23) are closed in Phase 12, and **D20** (per-row Reverse
  buttons on the statement-installment + party-timeline tables, D24) with it; 4.5 (auth) stays
  open (TASK.md Phase 4).

## Current state

Phases 0 and 1 are complete. `src/app/core/` holds:

- `types/` — every hand-written type, one per file, no `I-` prefix: `Money` (branded
  minor-units `number`), `CurrencyCode`, `IsoInstant`, `IsoDate`, `ProblemDetails`,
  `AppError`, `HealthStatus`/`HealthCheckEntry`/`HealthReport`, `RegisteredInstrument`.
- `money/` — `fromMinorUnits` / `toMinorUnits` / `formatArs`. Pure. **No float arithmetic on
  money** — `toMinorUnits` decomposes the decimal string, never `× 100`.
- `http/` — `baseUrlInterceptor` (prefixes relative URLs with `environment.apiUrl`; absolute
  URLs pass through), `problemDetailsInterceptor` (maps failures to `AppError`, trusts
  `HttpErrorResponse.status`), `pollUntil` (bounded reconciliation poll), `SKIP_ERROR_MAPPING`
  (`HttpContextToken` opt-out). Both interceptors are wired in `app.config.ts`.
- (`registry/` — removed in Phase 12. `InstrumentsService.list()` now serves the instrument
  list from `GET /v1/instruments`; every card/funding `<select>` loads it into a local
  `WritableSignal<Instrument[]>` in `ngOnInit`. `Instrument` type lives at
  `features/instruments/types/instrument.ts`. D21.)
- `health/` — `HealthService.check()`: `GET /health` at the host root, degradation-aware.

Feature code lives under `src/app/features/<feature>/` (`<feature>-service.ts`, `types/`,
`pages/<page>/`, `<feature>.routes.ts`). Seven features exist: `reports` (dashboard, the default
route), `instruments`, `financing`, `parties`, `ledger`, `subscriptions`, `creditors` (new — Slice 1
of `docs/creditor-expense-fields/slice-1-creditors-crud.md`: register a creditor + optional
free-text destination accounts, label + identifier (CBU/CVU/alias, nullable); CRUD-only, styled to
`docs/SYSTEM.md` from the start; **Slice 2 — Load-expense integration — now built**: creditor picker
+ account-to-pay selector revealed by a "Different creditor" toggle, wired into `load-expense-page`
as pure optional metadata — the toggle was **superseded by the two-way payment-mode selector** in
`docs/expense-payment-modes/` Slice 1, below). `app.routes.ts` lazy-wires all seven via `loadChildren`, redirects `''`
→ `reports`, and falls back `**` → `reports`. **Every other page has been redesigned against
`docs/SYSTEM.md`** — the markup was rewritten; component logic and its specs were largely left in
place (specs test logic, not the DOM).

**`docs/expense-description/` (new, spans `financing` + `reports`).** Slice 1 (`slice-1-description-field.md`,
built before this feature's own CLAUDE.md entry) added a required `description: string` to
`CreatePaymentPlan` — `load-expense-page` gained a required description field (`Validators.required`,
`maxLength(120)`, `noBlank`, `noNewline` in `validation-helpers.ts`) and its confirmation panel now
headlines the description (`confirmedDescription` signal) instead of a bare plan id. **Slice 2 —
Card-debt drill-down (`slice-2-card-debt-drilldown.md`) — now built:** the Dashboard's "Card Debt by
Cycle" rows are expandable — `financing-service.ts` gained `cardPurchases(cardId)` (mirrors
`listStatements`'s `{ rows }` envelope), new type `features/financing/types/card-purchase-row.ts`;
`dashboard-page.ts` threads `cardId` through `CardCycle` (was dropped by the label-based grouping
pipeline) and holds `expandedCardId`/`purchasesStatus`/`expandedPurchases` signals + a per-card
purchase cache; each card row with a `cardId` becomes an `aria-expanded` disclosure button, rows
without one stay non-expandable. **Slice 3 — Recent purchases view (`slice-3-recent-purchases-view.md`)
— now built, the final slice:** a standalone `recent-purchases-page` (new type
`features/financing/types/recent-purchase-row.ts`; `financing-service.ts` gained
`recentPurchases()`, mirroring `cardPurchases`'s `{ rows }` envelope) listing every loaded expense
across every card, newest-first, with no card picker (unlike `statements-page`, this view
deliberately spans every card). Container/table split mirrors `statements-page`/`statements-table`:
the page owns the `loadStatus` state machine and fetches once on `ngOnInit`; the presentational
`recent-purchases-table` renders each row's description, a `•` + `sr-only` creditor marker,
installment count, card name, purchase date, and `formatArs` total, staggered row-in animation
matching `statements-table`. Routed at `financing/recent-purchases`, linked from both the global
nav (`app.ts`) and a second Dashboard quick-action alongside "Record an expense".

**`docs/expense-payment-modes/` (new, `financing`).** **Slice 1 — Creditor-financed expenses**
(`slice-1-creditor-financed.md`) — now built. `CreatePaymentPlan.cardId` is optional
(`cardId?: string`), and `load-expense-page`'s "Different creditor" checkbox becomes a two-way
**payment-mode selector** — `mode: FormControl<'card' | 'creditor'>` on the form, a segmented
control (`<fieldset>` + `peer`/`peer-checked` radios, the `instruments-page` pattern) at the top of
"The purchase" — with room left for a third *My debit-cash* mode (Slice 3). `watchModeChange` swaps
validators: `'creditor'` clears + blanks `cardId` and makes `creditorId`/`creditorAccountId`
required; `'card'` reverses it and empties `creditorAccounts()`. `onSubmit` spreads
`mode === 'card' ? { cardId } : { creditorId, creditorAccountId }`; the card `<select>` renders
under `@if(mode === 'card')` and the creditor + account `<select>`s under `@else`, both inside the
purchase section — the standalone "Creditor" `<section>` and its checkbox are deleted. The split
FormArray stays visible and submittable in **both** modes. The API-retired per-row
`isCreditorPayment` flag is dropped from `features/financing/types/card-purchase-row.ts` and the
Dashboard card-purchases drill-down marker; `recent-purchase-row.ts` + `recent-purchases-*` keep
their own `isCreditorPayment` (Phase 17's field, untouched). `load-expense-page.spec.ts` reworked
for the selector (21 facts). **Slice 2 — Owed to creditors list (`slice-2-owed-to-creditors-list.md`) — now built:** a standalone `creditor-payables-page` (new type `features/financing/types/creditor-payable-row.ts` + `creditor-payable-account.ts`; `financing-service.ts` gained `creditorPayables()` unwrapping the `{ rows }` envelope) listing every creditor with an outstanding balance across card-less plans, ordered by creditor name, each row showing creditor name (+ muted account-labels sub-line), the next-due date (`—` when null), and the `formatArs` outstanding total; staggered row-in animation matching sibling tables; empty state "You don't owe any creditors." Routed at `financing/creditor-payables`, linked from the global nav right after "Recent purchases". Styled to `docs/SYSTEM.md` from the start. Specs: `financing-service.spec.ts` +1 envelope fact; new `creditor-payables-page.spec.ts` (3 facts) + `creditor-payables-table.spec.ts` (3 facts). Verification: `pnpm ng lint` clean, `pnpm ng test` **194/194**, `pnpm ng build --configuration production` clean (no budget warnings). **Slice 3 — Debit/cash expenses with categories (`slice-3-debit-cash-categories.md`) — now built:** the third *My debit-cash* mode on `load-expense-page`. `LedgerService` (`features/ledger/ledger-service.ts`) gained `recordDebitExpense(body): Observable<RecordDebitExpenseResult>` (`POST ledger/expenses`) and `listExpenseCategories(): Observable<string[]>` (`GET expense-categories`, `map`-unwrapping the `{ rows: [{ name }] }` envelope to a bare `string[]`); three new one-type-per-file models under `features/ledger/types/` — `record-debit-expense.ts` (`{ amountMinorUnits: Money; sourceInstrumentId: string; categoryName: string; purchaseDate: IsoDate; description: string; split?: DebitExpenseParticipant[] }`), `record-debit-expense-result.ts` (`{ id: string }`), `debit-expense-participant.ts` (`{ partyId: string; weight: number }`). `load-expense-page`: `mode` widened to `'card' | 'creditor' | 'debit'` with `modeOptions` gaining *My debit-cash* in the 2nd slot; the form gains `sourceInstrumentId` + `categoryName` `nonNullable` controls; new signals `bankAndCashInstruments` (`computed` — `instruments()` filtered to `type === 'debit' || type === 'cash'`), `expenseCategories: WritableSignal<string[]>` (populated in `ngOnInit` via a one-shot `loadExpenseCategories()`), and `confirmedKind: WritableSignal<'plan' | 'expense'>`; `LedgerService` injected alongside the financing services. `watchModeChange` is now 3-way: `'debit'` makes `sourceInstrumentId` required and `categoryName` `[Validators.required, noBlank]`, **drops** the `installmentCount` validator and pins it to `1`, and clears + blanks every card-mode and creditor-mode field (the `'card'`/`'creditor'` branches are unchanged — D11). `onSubmit` branches: `mode === 'debit'` builds a `RecordDebitExpense` (category `.trim()`ed, the split `FormArray` carried through unchanged — D9) and calls `ledgerService.recordDebitExpense(...)` mapped to `.id`, else the existing `createPaymentPlan(...)` path mapped to `.paymentPlanId`; the shared `subscribe` sets `confirmedPlanId`/`confirmedDescription`/`submitStatus` and still reconciles participants when a split was entered, and `confirmedKind` is set so the confirmation panel reads "expense recorded" / `Expense <code>{id}</code>` instead of "payment plan created" / `Plan …`. Template: the purchase-section `<select>` is now `@if(card) … @else if(creditor) … @else { <debit block> }` — the debit block is a "Paid from" `<select id="sourceInstrumentId">` fed by `bankAndCashInstruments()` (empty-state "No debit or cash accounts registered yet — add one on the Instruments page.") plus a **free-type Category `<input type="text" list="expense-category-options">` backed by a `<datalist>`** of `expenseCategories()` (pick-existing-or-type-new, with a hint line) — a `<datalist>`, not a bare native `<select>`, to stay within `docs/SYSTEM.md`'s caution against shipping an unstyled `<select>` as the design; the installments `<div>` is wrapped in `@if(mode !== 'debit')`. `submitErrorMessages` gains `Ledger.AccountNotFound`, `Ledger.SourceAccountNotSpendable`, `Ledger.InvalidExpenseCategory`. Specs: `ledger-service.spec.ts` +3 (POST `ledger/expenses` → `id`, POST with a split payload, GET `expense-categories` envelope→names); `load-expense-page.spec.ts` +9 (debit validators swap, both debit + cash instruments offered, blank category rejected, submit routes to `recordDebitExpense` **not** `createPaymentPlan` with the right payload + `confirmedKind() === 'expense'`, category trimmed, split included in the debit payload, `debit → card` switch restores card mode, DOM shows source/category + hides installments/cardId/creditorId, headline reads "expense recorded"). Verification: `pnpm ng lint` clean, `pnpm ng test` **206/206** (from 194), `pnpm ng build --configuration production` clean — `financing-routes` lazy chunk 50 → 56.40 kB, well under the 500 kB budget. Not committed by this session — the user commits their own.

**`docs/dashboard-fixes/` (new, spans `reports` + the API).** Bug-fix initiative over the Dashboard's
"Card Debt by Cycle" block. **Slice 1 — Installments paid of total (`slice-1-installments-paid-of-total.md`)
— done, client-only:** the drill-down purchase line in `dashboard-page.html` (~line 136) now reads
`{{ installmentCount - outstandingCount }} of {{ installmentCount }} installments paid` (was
`outstandingCount of installmentCount … outstanding`); no `.ts`/type change — both fields were already
on `CardPurchaseRow`. **Slice 2 — Card name on future rows + expand hardening
(`slice-2-card-name-and-expand.md`) — done:** two symptoms, one cause — future-installment cards showed
a GUID as their name, and because one card then produced two differently-labelled rows sharing a
`cardId`, expanding one opened every row of that card. The API half (`vw_card_future_schedule` joins
`financing_credit_cards` for a real `CardName`; Reporting `card_due_by_month.sql` labels its Future half
with it) is the API's Phase 21. Client half: `dashboard-page.ts` `cycleByCard()` regroups by the stable
`cardId` instead of the label string — grouping key `row.cardId ?? ('label:' + row.card)`, display label
per card prefers the Accrued-bucket label then falls back to the Future label (never a GUID), two-pass
ordering (accrued cards first, then future-only) preserved; `accruedByCard()`/`futureByCard()` left intact
(still spec-covered); a null-`cardId` row stays a plain non-expandable row as before. `dashboard-page.html`
`@for` `track` switched `card.card` → `card.cardId ?? card.card` — no structural template change (the
`@if(card.cardId; as cardId)` guards + `expandedCardId() === cardId` stay correct once each `cardId` is on
exactly one row). `dashboard-page.spec.ts` +2 facts (`DashboardView` gains `cycleByCard`): an Accrued + a
Future row sharing one `cardId` but different `card` labels collapse into exactly one `cycleByCard()` row
(Accrued label wins) and expanding it renders exactly one `#card-purchases-*` block; a lone Future row
keeps its name as the label. Verification: `pnpm ng lint` clean, `pnpm ng build` clean (no budget change,
`financing-routes` chunk unchanged), `pnpm ng test` **208/208** (from 206). Live browser E2E not run —
handed to the user. Committed by the user as `3b7a098` (Slice 1) + `979eb54` (Slice 2).
**Slice 3 — Parties list endpoint (`slice-3-parties-list-endpoint.md`) — now built (final slice, spans
`parties` + `financing` + the API):** fixes bug #2 — a created party was invisible because the client
only ever enumerated parties via `ReportsService.debtSummary()` (`GET /v1/reports/parties/debt-summary`),
whose INNER JOIN drops any party with zero ledger movements. The API adds `GET /v1/parties` (its
Phase 22). Client: new `features/parties/types/party.ts` (`Party = { id: string; name: string }`);
`parties-service.ts` gains `list(): Observable<Party[]>` (`GET parties`, `{ rows }` envelope unwrap)
mirroring `CreditorsService.list()`. `parties-page.ts` loads **both** `partiesService.list()` and
`reports.debtSummary()` via `forkJoin`, merging by id into a local `PartyListRow` VM
(`{ partyId, partyName, netBalanceMinorUnits }` — field names kept identical to the used `PartyDebtRow`
subset so the template + `balanceHint`/`tickWidth`/totals need no rename); a party with no matching debt
row gets `netBalanceMinorUnits: fromMinorUnits(0)`, which the existing `balanceHint` renders as
"Settled up" / `$0.00`. Empty state "No parties with movements yet." → "No parties yet."; the
created-party note reworded (new parties now appear immediately). `load-expense-page.ts` `loadParties()`
switches its source from `reportsService.debtSummary()` to `partiesService.list()` — `parties` signal
`PartyDebtRow[]` → `Party[]`, `partyName()` helper and the split `<select>` template migrated from
`partyId`/`partyName` to `id`/`name`, the `ReportsService` import + field dropped; "Add participant"
(`[disabled]="parties().length === 0"`) now enables as soon as any party exists.
`parties-page.spec.ts` rewritten (9 facts — merge, settled-at-zero, "No parties yet.", roster-load-fail
→ error); `load-expense-page.spec.ts` reworked (`PartyDebtRow`→`Party`, `debtSummary` spy →
`PartiesService.list`, `ReportsService` provider dropped). `docs/DESIGN.md` §9 gains a `GET /v1/parties`
row; `docs/PRD.md` §3.7 note updated (`debt-summary` is no longer the only way to enumerate parties).
Verification: `pnpm ng lint` clean, `pnpm ng test` **210/210** (from 208), `pnpm ng build` clean. Live
browser E2E not run — handed to the user. Not committed by this session — the user commits their own.

**`docs/parties-card-split/` (new, spans `financing` + `parties`).** Two slices fixing the credit-card-split
experience on the Parties side. **Slice 1 — Stop the reconcile loop for card splits
(`slice-1-reconcile-loop-fix.md`) — done, client-only:** `load-expense-page.ts` `reconcile()` takes a
`mode?: LoadExpenseMode` argument and, for `mode === 'card'`, seeds the reconciliation table then returns
**before** the `pollUntil` loop — a card split posts no synchronous receivable (it accrues per billing
cycle, next month), so the 5×800 ms balance poll could only ever stall. `ReconciliationStatus` gains
`'scheduled'`; participants are marked `updateReconciliation(partyId, 'scheduled', null)`;
`load-expense-page.html` adds a `@case('scheduled')` reading "scheduled — accrues monthly". Debit/cash
splits keep polling (their up-front posting makes it succeed); the creditor path **joined the `'scheduled'`
short-circuit in `docs/cycle-due-month` Slice 2** (its up-front post was removed — see below). Shipped as `959a3a2`.
**Slice 2b — Party's future monthly shares (`slice-2b-party-future-shares.md`) — done, spans the API:** the
party-detail page gains a **"Scheduled"** section between the posted timeline and the settlement form,
listing what the party will owe per upcoming billing cycle on its card-split plans. New type
`features/parties/types/future-party-share.ts` (`FuturePartyShare = { cycleYear; cycleMonth;
shareMinorUnits: Money; currencyCode; sourceLabel }`); `parties-service.ts` gains
`futureShares(partyId): Observable<FuturePartyShare[]>` (`GET parties/{id}/future-shares`, `{ rows }`
envelope unwrap). `party-detail-page.ts`: `futureShares` + `futureSharesStatus` signals loaded by a
`loadFutureShares(id)` beside `loadTimeline(id)` (same `takeUntil(this.destroy$)` shape), called from
`ngOnInit` only (not re-run after a settlement); a module-level `MONTH_LABELS` array + `cycleLabel(share)`
helper renders "Oct 2026". `party-detail-page.html` renders loading / error / empty ("Nothing scheduled —
no upcoming installment shares for this party.") / a `<ul>` of dashed-left-border rows (`cycleLabel`,
`sourceLabel`, `formatArs(shareMinorUnits)`). API half is `app/api` Phase 24 (`GET
/v1/parties/{id}/future-shares`, a Financing CQRS query reusing `PhantomPennyAllocator` so the projection
is byte-exact with accrual). **Billing-cycle anchor — resolved by `docs/cycle-due-month` Slice 1
(`app/api` steps 1–6):** the API now returns the **due** cycle (statement-close month + 1 — "when the
money moves") on `GET /v1/parties/{id}/future-shares` and every other payment-facing card surface, so a
Sept purchase's shares read Oct/Nov/Dec as the initiative docs intend. The page still renders `cycleMonth`
verbatim — `MONTH_LABELS[cycleMonth - 1]` is array indexing, unchanged — and statement-facing views keep
the raw close cycle. API `docs/PRD.md` §9 decision 8 is settled. `party-detail-page.spec.ts` +2 facts
(renders the scheduled rows; shows the empty note); Slice 1's client step re-characterises the Scheduled
fixture as the due cycle. Committed by the user as `e281437` (page + service) + `09357cb` (tests).

**`docs/cycle-due-month/` (new, spans the API + `financing` + `parties`).** "Billing cycle 'due month'
reframe + creditor-split parity" — payment-facing card surfaces show the **payment month** (statement-close
+ 1), not the close month. **Slice 1 — Card due-month (`slice-1-card-due-month.md`) — done, client had no
production change:** the API now returns the due cycle on every payment-facing surface (`app/api` Phase 25);
the client renders `cycleMonth` verbatim (`MONTH_LABELS[cycleMonth - 1]` is array indexing, unchanged) and
the dashboard "Card debt by cycle" block shows no month, only per-card bars. Only doc + spec-fixture wording
changed (`party-detail-page.spec.ts` scheduled fixture re-characterised as the due cycle; the two
"billing-cycle anchor" notes here + in `TASK.md` rewritten as resolved). Committed with the API steps as
`83d7809` + `22f88b1`. **Slice 2 — Creditor-split parity (`slice-2-creditor-split-parity.md`) — done:** a
creditor-financed split now behaves exactly like a card split — $0 owed now, accrues at the due month,
visible in the party "Scheduled" block dated the payment month (`app/api` Phase 26 removed the up-front
co-borrower receivable post and moved accrual into the shared `AccrueInstallments` scheduler; `GET
/v1/parties/{id}/future-shares` now returns creditor rows too, labelled `"{CreditorName} — {description}"`).
Client: **`load-expense-page.ts` `reconcile()` — the `mode === 'card'` `'scheduled'` short-circuit widened
to `mode === 'card' || mode === 'creditor'`.** The slice doc called the client "mostly free" — it wasn't:
with the API's synchronous up-front post gone, a creditor split's co-borrower balance no longer moves at
submit, so the `pollUntil` loop could only ever stall. Debit/cash still polls (its posting stays
synchronous). `party-detail-page.html` "Scheduled" intro copy widened — "on card-split **and
creditor-financed** plans … when its **due month** arrives" (also drops the now-wrong "once its billing
cycle closes"; no spec asserts this text). `load-expense-page.spec.ts` — `does not poll for a
creditor-financed split and marks the participant scheduled` added (mirrors the card test), two stale
"Debit/creditor splits still poll" comments corrected. `pnpm ng lint` clean, `pnpm ng test` **215/215**
(from 214), `pnpm ng build --configuration production` clean (`financing-routes` 56.55 kB). Not committed by
this session — the user commits their own (`app/api` steps landed as `d4dd08f` + `bf4477b`). **Slice 3 —
Schedule-aware summary (`slice-3-schedule-aware-summary.md`) — now built (final slice, closes the
initiative):** the Parties **list** page stops reading "Settled up" for a $0-now party that has
not-yet-accrued split installments (card or creditor) scheduled ahead. New type
`features/parties/types/pending-shares-by-party-row.ts` (`PendingSharesByPartyRow = { partyId;
scheduledCount; scheduledTotalMinorUnits: Money; currencyCode }`); `parties-service.ts` gains
`pendingShares(): Observable<PendingSharesByPartyRow[]>` (`GET parties/pending-shares`, `{ rows }`
envelope unwrap) — API half is `app/api` Phase 27 (a Financing bulk query reusing the same allocator as
`GET /v1/parties/{id}/future-shares`, aggregated per party; the slice doc's "extend `debt_by_party.sql`"
was architecturally blocked — Reporting can't reach Financing's `DbContext` and phantom-penny has no SQL
form). `parties-page.ts` `loadParties()` `forkJoin` gains a third source (`pending`), builds a
`scheduledCountByPartyId` map, and threads `scheduledCount` onto its local `PartyListRow` VM;
`balanceHint()` gets a branch **before** the "Settled up" return — `netBalanceMinorUnits === 0 &&
scheduledCount > 0` → `"Nothing owed yet · N scheduled"` (chosen over the doc's literal "$0 now · N
scheduled" — the amount cell already shows the zero, and "$0" assumes a currency glyph `formatArs` may
not use). `parties-page.html` widens the amount's `[class.text-ledger]` guard to `=== 0 &&
party.scheduledCount === 0` so a $0-now scheduled party is not painted settled-green; the roster from
`GET /v1/parties` already lists every party, so no extra merge is needed. `parties-page.spec.ts` — new
`pendingShares` spy (default `of([])`), the settled-at-zero test renamed to "no balance and no schedule",
+2 facts ($0 + schedule → the new hint; real balance + schedule → hint unchanged); `parties-service.spec.ts`
+1 envelope-unwrap fact. `docs/DESIGN.md` §9 gains a `GET /v1/parties/pending-shares` row; `docs/PRD.md`
§3.7 "List shows" updated. Verification: `pnpm ng lint` clean, `pnpm ng test` **218/218** (from 215),
`pnpm ng build --configuration production` clean (`parties-routes` 42.95 kB). Live browser E2E not run —
handed to the user. Not committed by this session — the user commits their own.

**Individual installment payments — Slice 1 (foundation, `docs/individual-installment-payments/slice-1-foundation-payable-from-installments.md`) — type wiring only, no visible change.** The API (`app/api` Phase 28) now sends a per-cuota `isPaid` / `paidOnUtc` on `GET /v1/financing/statements/{id}` and charges only the unpaid, non-reversed installments when a statement is paid. Client side: `features/financing/types/monthly-statement-installment.ts` `MonthlyStatementInstallment` gains `isPaid: boolean` + `paidOnUtc: IsoInstant | null` (branded `IsoInstant`, matching the sibling `MonthlyStatement.paidOnUtc` — the slice doc's literal `string | null` was not followed). `getStatement()` is a bare `http.get<MonthlyStatement>` cast — no field mapper — so nothing else changed; three `installments`-fixture specs (`installments-table.spec.ts`, `statement-page.spec.ts`, `financing-service.spec.ts`) got `isPaid: false, paidOnUtc: null` on their typed literals. `pnpm ng lint` clean, `pnpm ng test` **218/218**, `pnpm ng build --configuration production` clean. No template change — the per-row **Pay** button and the paid chip land in **Slice 2**. Not committed by this session — the user commits their own.

**Individual installment payments — Slice 2 (pay a single installment, `docs/individual-installment-payments/slice-2-pay-single-installment.md`) — the headline feature.** The API (`app/api` Phase 29) adds `POST /v1/financing/installments/{id}/pay` (plain `Dr CardLiability / Cr Bank`, no carried-credit netting). Client side: new one-type-per-file models `features/financing/types/pay-installment.ts` (`PayInstallment = { bankAccountId: string; paidOnUtc: IsoInstant }`) + `pay-installment-result.ts` (`{ installmentId: string }`), mirroring `pay-statement.ts`. `financing-service.ts` gains `payInstallment(id, body): Observable<PayInstallmentResult>` → `POST financing/installments/${id}/pay`, right after `payStatement`. `installments-table` (`pages/statement-page/installments-table.{ts,html}`): new `paying: InputSignal<boolean>` (default `false`) + `payClick: OutputEmitterRef<string>`; `canPay` = `!isPaid && !isReversed`; `onPay` emits `installmentId` when `canPay`; the status cell gains an `@else if(installment.isPaid)` branch rendering `<span class="installments__badge">Paid</span>` (the same green pill as "Reversed" — the CSS comment already says it matches the "Paid" badge treatment); the action cell gains a `Pay` button (accent `text-stamp`, disabled when `!canPay(installment) || paying()`) before the existing `Reverse` button; the header sr-only label `Reverse` → `Actions`. `statement-page` (`pages/statement-page/statement-page.{ts,html}`): `onPayInstallment(installmentId)` validates the **same** reactive form the full-statement pay uses (`markAllAsTouched` on invalid), builds `PayInstallment` from the shared `bankAccountId` + `paidOnUtc`, reuses the `payStatus` / `payError` signals, calls `financing.payInstallment`, and on success re-fetches via `loadStatement(statementId)` (captured in a local const after the null-guard so it narrows in the async callback); `<app-installments-table>` binds `[paying]="payStatus() === 'paying'"` + `(payClick)="onPayInstallment($event)"`; the full-statement button label `Record payment` → **`Pay full statement`** and its section heading → "Pay the full statement"; `payErrorMessages` gains `Financing.InstallmentNotFound` / `InstallmentAlreadyPaid` / `InstallmentAlreadyReversed` / `InstallmentNotAccrued`. No second selector — per-row Pay is emit-up, reusing the one form. Specs: `financing-service.spec.ts` +1 (`payInstallment` POST body/URL); `installments-table.spec.ts` — 2 Reverse tests rewritten to pick buttons by text (`buttonsByLabel` helper — each row now has Pay + Reverse) + 4 added (Pay enable/disable by `isPaid` / `isReversed`, Pay emits the id, `[paying]` disables all Pay buttons, Paid chip renders); `statement-page.spec.ts` +3 (relabel present, pays one installment via the shared form + refetch, won't pay while the form is invalid) + reverse-button test switched to text selection + `payInstallment` spy in the `FinancingService` mock. **Spec gotcha:** `fixture.nativeElement.querySelectorAll<T>(...)` fails `TS2347` (nativeElement is `any`) — annotate the receiving const `: NodeListOf<HTMLButtonElement>` instead of passing a type argument. `pnpm ng lint` clean, `pnpm ng test` **226/226** (from 218), `pnpm ng build --configuration production` clean (`financing-routes` 58.34 kB). Not committed by this session — the user commits their own.

**Individual installment payments — Slice 3 (next-payment visibility, `docs/individual-installment-payments/slice-3-next-payment-visibility.md`) — the final slice; closes the initiative.** The API (`app/api` Phase 30) now sends two derived fields on every `GET /v1/financing/purchases/recent` row: `paidInstallmentCount` and a next-payment month (`nextDueYear` / `nextDueMonth`, both null once every installment is paid or reversed). Client side, all under `features/financing/`: `types/recent-purchase-row.ts` `RecentPurchaseRow` gains `paidInstallmentCount: number` + `nextDueYear: number | null` + `nextDueMonth: number | null` (matches the API's nullable ints — the slice doc's literal). `recent-purchases-table.ts` gains a module-level `MONTH_LABELS` array (the `party-detail-page.ts` pattern — no new date library) and replaces the `installmentLabel` helper with `paidLabel` (`"1/3 paid"`) + `nextPaymentLabel` (`"next: Nov 2026"` from `MONTH_LABELS[nextDueMonth - 1]`, or `"Fully paid"` when either `nextDue*` is null). `recent-purchases-table.html` first-cell sub-line → `{{ paidLabel(purchase) }} &middot; {{ nextPaymentLabel(purchase) }}` (same `&middot;` idiom as the creditor `&bull;`; the total `M` survives as the "N/M" denominator). `getStatement()`-style bare cast — `recentPurchases()` needs no mapper change. Specs: `recent-purchases-table.spec.ts` +2 (renders `1/3 paid`; renders `next: Nov 2026` on the due row and `Fully paid` on the null row); `recent-purchases-page.spec.ts` + `financing-service.spec.ts` fixtures got the 3 new fields. `pnpm ng lint` clean, `pnpm ng test` **228/228** (from 226), `pnpm ng build --configuration production` clean. No route, nav, or service-method change — the endpoint was already wired (Phase 9). Not committed by this session — the user commits their own. **This closes `docs/individual-installment-payments/`.**

**`docs/backdated-expenses/` (new, `financing`).** **Slice 1 — Card back-dating (`slice-1-card-backdating.md`, + `00-overview.md`) — now built.** Loading a **credit-card** expense with a past purchase date now settles its already-elapsed installments server-side at creation (API `app/api` Phase 31), funded from a bank the user picks; a future purchase date is rejected on both stacks. Client side, all under `features/financing/`: `types/create-payment-plan.ts` `CreatePaymentPlan` gains `bankAccountId?: string` (mirrors the API's trailing optional `Guid? BankAccountId`). `load-expense-page.ts`: a `bankAccountId` `FormControl<string>` on the form; `isBackdatedCardPurchase()` (`mode === 'card'` && a well-formed ISO `purchaseDate` && `< todayIso()` — `new Date().toISOString().slice(0, 10)`, UTC, matching the API clock); `watchBackdatedFunding()` subscribes to `merge(mode.valueChanges, purchaseDate.valueChanges)` and toggles `Validators.required` on `bankAccountId` (clearing the value + validator when not back-dated) — a fourth watcher alongside `watchModeChange` / `watchCreditorSelection`; `onSubmit` spreads `bankAccountId` into the card-mode payload when truthy; `submitErrorMessages` gains `Financing.FuturePurchaseDate` + `Financing.BackdatedCardBankAccountRequired`. `load-expense-page.html`: a "Paid from" `<select id="bankAccountId">` inside the `@if(form.controls.mode.value === 'card')` branch, gated by `@if(isBackdatedCardPurchase())`, reusing the existing `bankAndCashInstruments()` signal (the debit-mode source list) with a back-dated explainer line, the shared empty-state, and the required-error message. `validation-helpers.ts`: new `notFuture: ValidatorFn` (returns `{ notFuture: true }` when a well-formed ISO date is strictly `> new Date().toISOString().slice(0, 10)` — today allowed, matching the API); `purchaseDate` validators → `[isoDate, notFuture]`, plus a `notFuture` error line + `errorMessages` entry. **The client's back-dated trigger is coarser than the API's** — it flags any past `purchaseDate`, where the API only requires a bank when cuota 1's due cycle is already past (it can't compute that without the card's cutoff); the API ignores `bankAccountId` when not needed, so an occasionally-shown selector is harmless. `load-expense-page.spec.ts`: `fillValidForm` / `fillValidDebitForm` switched from the hardcoded `'2026-09-01'` (which real-clock comparison would make permanently "back-dated") to a dynamic `todayIso()`, and the two `purchaseDate` payload assertions follow; `LoadExpenseView` form type gains `bankAccountId`; **+6 facts** (selector hidden for a today-dated card purchase; shown + required for a back-dated one; `bankAccountId` in the posted body; omitted when today-dated; requirement dropped when re-dated to today; future date blocks submit). Verification: `pnpm ng lint` clean, `pnpm ng test` **234/234** (from 228), `pnpm ng build --configuration production` clean. Live browser E2E not run — handed to the user. Not committed by this session — the user commits their own. **Slice 2 — Creditor 26th cutoff (`slice-2-creditor-cutoff.md`) — now built, no client production change:** the API (`app/api` Phase 32) routes creditor purchases through the same 26th cutoff as cards (uniformly, not only back-dated ones) and stamps a back-dated creditor purchase's already-elapsed cuotas `PaidOnUtc` (display-only, no ledger). Recent Purchases already renders "N/M paid · next: <month>" from `PaidOnUtc` (Phase 30), and the "Paid from" selector was already card-only (`isBackdatedCardPurchase()` → `mode === 'card'`), so nothing in `load-expense-page.{ts,html}` changed. `load-expense-page.spec.ts` +1 fact: the "Paid from" selector stays hidden for a back-dated **creditor** purchase (`#bankAccountId` null, `bankAccountId` not required, form valid, submit body carries `creditorId` and no `bankAccountId`). `pnpm ng lint` clean, `pnpm ng test` **235/235** (from 234), `pnpm ng build --configuration production` clean. **Slice 3 — Pending $ on Recent Purchases (`slice-3-pending-amount.md`) — now built, closes the initiative:** the API (`app/api` Phase 33) sends a per-row `pendingAmountMinorUnits` on `GET /v1/financing/purchases/recent` = Σ unpaid, non-reversed installment amounts (same filter as `nextDue*`). Client side, all under `features/financing/`: `types/recent-purchase-row.ts` `RecentPurchaseRow` gains `pendingAmountMinorUnits: Money` (`Money` already imported for `totalMinorUnits`). `recent-purchases-table.ts` gains `hasPending(purchase)` (`purchase.pendingAmountMinorUnits > 0`) + `pendingLabel(purchase)` (`` `${formatArs(purchase.pendingAmountMinorUnits)} pending` ``, reusing the existing `formatArs` field). `recent-purchases-table.html` first-cell sub-line → `{{ paidLabel(purchase) }}` then `@if(hasPending(purchase)) { &middot; {{ pendingLabel(purchase) }} }` then `&middot; {{ nextPaymentLabel(purchase) }}` — the pending segment is hidden entirely when `0` (so a fully-paid row still reads `N/M paid · Fully paid`). `recentPurchases()` is a bare cast — no mapper change. Three `RecentPurchaseRow` spec fixtures (`recent-purchases-table.spec.ts`, `recent-purchases-page.spec.ts`, `financing-service.spec.ts`) gain the field; `recent-purchases-table.spec.ts` +1 fact (label present on a partly-paid row, absent on a fully-paid one). `pnpm ng lint` clean, `pnpm ng test` **236/236** (from 235), `pnpm ng build --configuration production` clean. No route, nav, or service-method change. Not committed by this session — the user commits their own. **This closes `docs/backdated-expenses/`.**

**`docs/owed-to-creditors/` (new, `financing`).** **Slice 1 — Current-cycle outstanding (`slice-1-current-cycle-outstanding.md`, + `00-overview.md`; first of four slices — Slice 2 detail-by-purchase, Slice 3 pay a cuota + undo, Slice 4 pay full debt) — now built.** The "Owed to creditors" row stops showing the *historic* total to each creditor and shows **two** figures (API `app/api` Phase 34): "Due now" (unpaid, non-reversed cuotas due by the current uniform-26th creditor cycle, with earlier-month arrears folded in) and "Total owed" (all unpaid, non-reversed, future included); paid cuotas are excluded from both. Client side, all under `features/financing/`: `types/creditor-payable-row.ts` drops `outstandingMinorUnits: Money` and gains `dueNowMinorUnits: Money` + `totalOwedMinorUnits: Money` (the per-account `CreditorPayableAccount.outstandingMinorUnits` is untouched — the API kept `CreditorPayableAccountBreakdown.OutstandingMinorUnits`). `creditor-payables-table.html`: the amount `<td>` becomes a focal `<span class="block text-sm text-ink">{{ formatArs(row.dueNowMinorUnits) }}</span>` plus a muted `<span class="block text-[0.6875rem] text-ink-faint">{{ formatArs(row.totalOwedMinorUnits) }} total</span>` sub-line (mirrors the existing account-labels sub-line classes — `docs/SYSTEM.md`: name the one focal element, demote the rest); the column header `Amount` → `Due now / Total`. `creditor-payables-table.ts` / `creditor-payables-page.ts` need **no** change — neither referenced the field and `formatArs` / `Money` were already imported; `creditorPayables()` stays a bare `{ rows }` cast, no mapper. Specs: `financing-service.spec.ts` + `creditor-payables-page.spec.ts` fixtures swap `outstandingMinorUnits` for the two fields; `creditor-payables-table.spec.ts` imports `formatArs`, gives its two fixture rows **distinct** due-now / total-owed values, and adds a fact asserting a row renders both figures + the word `total`. `pnpm ng lint` clean, `pnpm ng test` **237/237** (from 236: `creditor-payables-table` +1), `pnpm ng build --configuration production` clean (no budget change). No route or nav change — the `financing/creditor-payables` page and its nav entry already exist (Phase 20). Not committed by this session — the user commits their own. **Slice 2 — Creditor detail view (`slice-2-creditor-detail-view.md`) — now built:** a read-only drill-down at `financing/creditor-payables/:creditorId` showing one creditor's debt grouped by purchase. API half is `app/api` Phase 35 (`GET /v1/financing/creditor-payables/{creditorId}` — a pure additive CQRS query; unknown creditor → 404 `Financing.CreditorNotFound`). Client, all under `features/financing/`: three new one-type-per-file models — `types/creditor-installment-row.ts` (`{ installmentId, sequence, installmentCount, amountMinorUnits: Money, dueYear, dueMonth, isPaid, isReversed, status: 'overdue'|'due'|'future'|'paid'|'reversed' }` — the union inlined on the field, not a separate export), `types/creditor-purchase-group.ts` (`{ planId, description, purchaseDate: IsoDate, totalMinorUnits, outstandingMinorUnits: Money, installments: CreditorInstallmentRow[] }`), `types/creditor-detail.ts` (`{ creditorId, creditorName, purchases: CreditorPurchaseGroup[] }`). `financing-service.ts` gains `creditorDetail(creditorId): Observable<CreditorDetail>` → bare `http.get<CreditorDetail>(\`financing/creditor-payables/${creditorId}\`)` (single object, no `{ rows }` envelope — the `getStatement` pattern). `financing.routes.ts` adds `{ path: 'creditor-payables/:creditorId', component: CreditorDetailPage }` after the list route. New `pages/creditor-detail-page/creditor-detail-page.{ts,html,css}` — container on the `statement-page` pattern: `LoadStatus = 'idle'|'loading'|'ready'|'error'`, signals `detail` / `loadStatus` / `loadError: AppError | null`; reads `:creditorId` from `route.paramMap` in `ngOnInit`; the `error` branch splits on `loadError()?.code === 'Financing.CreditorNotFound'` (`isNotFound()`) → a plain `text-ink-soft` "No creditor matches that link" line vs. the generic `role="alert"` retry copy. New `pages/creditor-detail-page/creditor-purchases-table.{ts,html,css}` — presentational, `purchases = input.required<CreditorPurchaseGroup[]>()`, module-level `MONTH_LABELS` (copied from `recent-purchases-table.ts`, not shared): a `<li class="purchase-group">` per purchase (staggered `[style.animation-delay.ms]`) with description + date + "X outstanding of Y", then an installments `<table>` (cuota `N/M`, due `Mon YYYY`, amount, status). Status cell `@switch(row.status)`: `paid`/`reversed` → a hairline `--ledger` pill (`.status-badge`, CSS copied from `installments-table.css` and renamed), `overdue`/`due`/`future` → small-caps text at descending ink weight. Two HTML comments mark the Slice-3/4 seams (header "Pay full debt" + per-row Pay/Undo column). `creditor-payables-table.{ts,html}` — creditor name wrapped in `<a [routerLink]="['/financing', 'creditor-payables', row.creditorId]">` with a `&rsaquo;` chevron (the `parties-page` link idiom), `RouterLink` added to `imports`; this forced `provideRouter([])` into both `creditor-payables-table.spec.ts` and `creditor-payables-page.spec.ts` (the latter's existing "renders on init" fact broke with `NG0201 No provider for ActivatedRoute` once rows render the `<a>`). Specs: `financing-service.spec.ts` +2 (`creditorDetail` bare GET + its 404→AppError mapping); `creditor-payables-table.spec.ts` +1 (name links to the detail route); new `creditor-detail-page.spec.ts` (4 — loads by param + renders groups; status label per installment; `CreditorNotFound` 404 → not-found state; other error → generic) + `creditor-purchases-table.spec.ts` (4 — section per purchase; installment rows with `N/M` + due month + status; `.status-badge` for paid; empty note). `pnpm ng lint` clean, `pnpm ng test` **248/248** (from 237), `pnpm ng build --configuration production` clean (`financing-routes` 58.3 → 68.7 kB). **`docs/DESIGN.md` §3** gains the three types + §9 endpoint row; **`docs/PRD.md` §3.10** records the detail view. Not committed by this session — the user commits their own. **Slice 3 — Pay a creditor cuota + undo (`slice-3-pay-installment-and-undo.md`) — now built:** the Slice-2 detail page gains per-row **Pay** / **Undo** buttons. API half is `app/api` Phase 36 — a display-only stamp: `POST /v1/financing/creditor-installments/{id}/pay` + `/unpay` stamp/clear `Installment.PaidOnUtc`, no bank, no ledger. Client, all under `features/financing/`: new `types/pay-creditor-installment-result.ts` (`{ installmentId: string }`). `financing-service.ts` gains `payCreditorInstallment(id)` / `unpayCreditorInstallment(id)` → `POST financing/creditor-installments/${id}/pay|unpay` with an empty `{}` body, after `creditorDetail`. `creditor-purchases-table` (presentational) gains `paying: input<boolean>(false)` + `payClick` / `undoClick: output<string>()`, a `canPay(row)` = `!isPaid && !isReversed` guard, and a trailing action `<td>` per installment row — **Pay** when `canPay`, **Undo** when `row.isPaid`, both `[disabled]="paying()"`, button styling copied verbatim from the card `installments-table`; the `<thead>` "Slice 3 seam" comment became a real `sr-only` "Actions" `<th>`, and the top "Pay full debt" seam comment was retagged Slice 4. `creditor-detail-page` (container) gains `payStatus: 'idle' | 'busy' | 'error'` + `payError: AppError | null` signals and a `creditorId` field; `onPay(id)` / `onUndo(id)` → `runMutation(operation)` which sets `busy`, calls the service, on success re-fetches `creditorDetail(creditorId)` (refreshes the badges + the Slice-1/2 figures), on failure sets `payError`; `payErrorText` maps `Financing.NotACreditorInstallment` / `InstallmentAlreadyPaid` / `InstallmentAlreadyReversed` / `InstallmentNotFound`. **No form, no bank selector** — a creditor pay takes no input. The template adds a `role="alert"` line for `payError()` and binds `[paying]="payStatus() === 'busy'"` + `(payClick)` + `(undoClick)` on the table. Specs: `financing-service.spec.ts` +2 (pay/unpay POST URL + `{}` body + result); `creditor-purchases-table.spec.ts` +5 (Pay/Undo button counts by status, neither on a reversed row, `payClick` / `undoClick` emit the id, `[paying]` disables all four action buttons); `creditor-detail-page.spec.ts` +3 (Pay click → `payCreditorInstallment('i-2')` + re-fetch, Undo click → `unpayCreditorInstallment('i-1')` + re-fetch, a failed pay renders "already marked paid" and does **not** re-fetch). `pnpm ng lint` clean, `pnpm ng test` **258/258** (from 248), `pnpm ng build --configuration production` clean (`financing-routes` 68.7 → 72.14 kB). Not committed by this session — the user commits their own. **Slice 4 — Pay full debt (`slice-4-pay-full-debt.md`) — now built, closes the initiative:** one **"Pay full debt"** button on the Slice-2 detail page that settles the creditor's entire remaining debt. API half is `app/api` Phase 37 — `POST /v1/financing/creditor-payables/{creditorId}/pay-full` stamps `Installment.PaidOnUtc` on every unpaid, non-reversed cuota across all the creditor's purchases in one transaction (display-only — no bank, no ledger), returns the count settled; zero settleable → `0` (idempotent); unknown creditor → 404 `Financing.CreditorNotFound`. No bulk undo — Slice 3's per-cuota Undo is the recovery path. Client, all under `features/financing/`: new `types/pay-creditor-full-debt-result.ts` (`{ settledCount: number }`). `financing-service.ts` gains `payCreditorFullDebt(creditorId)` → `POST financing/creditor-payables/${creditorId}/pay-full` with an empty `{}` body, after `unpayCreditorInstallment`. `creditor-detail-page` (container) gains `confirmingFullDebt: WritableSignal<boolean>` + `lastSettledCount: WritableSignal<number | null>` signals, `hasOutstanding()` (`detail()?.purchases` has any group with `outstandingMinorUnits > 0` — a `.some()` check, no money arithmetic), and `requestPayFullDebt()` / `cancelPayFullDebt()` / `confirmPayFullDebt()` — the last sets `payStatus='busy'`, calls the service, on success sets `lastSettledCount` + clears the confirm + re-fetches `creditorDetail(creditorId)` (refreshes badges + the Slice-1/2 figures), on failure sets `payError`; the per-cuota `runMutation` path is untouched, and the `payStatus`/`payError` signals are shared. `creditor-detail-page.html` adds an action row under the "Purchases" header: a **Pay full debt** button (`[disabled]="!hasOutstanding() || payStatus() === 'busy'"`) that swaps to a text prompt + **Confirm** / **Cancel** when armed (lightweight inline, no modal — the doc's preference), plus an `N cuota(s) settled.` line on success; button styling copied from the card `installments-table` Pay button. `creditor-purchases-table.html` — the now-satisfied `<!-- Slice 4 seam -->` comment removed. Specs: `financing-service.spec.ts` +1 (`payCreditorFullDebt` POST URL + `{}` body + `{ settledCount }` result); `creditor-detail-page.spec.ts` +3 (new `payCreditorFullDebt` spy defaulted `of({ settledCount: 3 })`: arms the inline confirm → **Confirm** calls `payCreditorFullDebt('cred-1')` + re-fetches + renders "cuota(s) settled"; `Pay full debt` disabled when every group's `outstandingMinorUnits` is 0; **Cancel** backs out without calling the service). `pnpm ng lint` clean, `pnpm ng test` **262/262** (from 258), `pnpm ng build --configuration production` clean (`financing-routes` 72.14 → 74.77 kB). `docs/DESIGN.md` §9 gains the `POST .../creditor-installments/{id}/pay`, `/unpay` (Slice 3, previously un-listed) and `.../creditor-payables/{creditorId}/pay-full` endpoint rows. Not committed by this session — the user commits their own. **This closes `docs/owed-to-creditors/` — the initiative is complete.**

## Conventions — the non-negotiables

**Class layout** — every class artifact (component, service, pipe) follows the member order in
`../../api/.claude/rules/method-organization.md`: I/O declarations → `public` → `protected` →
`private` fields → constructor → `public` → `protected` → `private` methods → lifecycle hooks
→ getters. `protected` = template surface, `private` = internals; explicit modifier on every
member.

**Components**
- Standalone. Never write `standalone: true` (it is the default).
- Template and styles in **separate files** (`templateUrl` / `styleUrl`) — never inline.
- `input()` / `output()` / `model()` functions, never the `@Input()` / `@Output()` decorators.
- No `@HostBinding` / `@HostListener` — use the `host` object.
- Native control flow (`@if` / `@for` / `@switch`), never `*ngIf` / `*ngFor` / `*ngSwitch`.
- `[class.x]` / `[style.x]` bindings, never `ngClass` / `ngStyle`.
- `NgOptimizedImage` for static images (not for inline base64).
- Container vs presentational: containers inject services and own state; presentational
  components get everything via `input()` and emit via `output()`.
- Styling: Tailwind utilities bound to the `docs/SYSTEM.md` semantic tokens — no raw palette
  classes, no hex, no dark-mode variants. A pattern that can't be a utility (the cycle-bar
  hatch `repeating-linear-gradient`, keyframes) goes in the page's `.css` file. Name the one
  focal element per view first, then demote the rest (SYSTEM.md "Focal pattern").

**State**
- **Signals-first.** All synchronous state is signals; derived state is `computed()`.
- No `effect()` for data flow. No `.mutate()` — `.set()` / `.update()`.
- Observables stay at their source (HTTP, router, events). The component subscribes and
  `.set()`s the signal. **The async pipe is not used** — templates read signals directly
  (`{{ user() }}`).
- Signals are double-annotated (`WritableSignal<T>` + `signal<T>(…)`), except unambiguous
  primitive literals.

**Services**
- `@Injectable({ providedIn: 'root' })`, `inject(HttpClient)` in a field initializer (no
  constructor injection). Methods return `Observable<T>` with explicit HTTP generics.
- Unwrap `{ rows: [...] }` list envelopes to the array via `map`.
- App-wide state as a signal on the service; expose `asReadonly()` when external mutation
  must be blocked.

**Types & imports**
- One `type` per file under a `types/` folder; `type` for domain shapes, `interface` only
  for extensible contracts. String unions + `as const` over `enum`.
- Types mirror the API DTOs by hand from `/openapi/v1.json` (the running API serves it) —
  no codegen. .NET serializes camelCase; `long` minor units → `Money`, `DateTimeOffset` →
  `IsoInstant`, `DateOnly` → `IsoDate`, GUIDs → `string`.
- Relative imports only — **no path aliases**.
- `Money` is minor units, always. Money is entered in **major** units in forms and converted
  with `toMinorUnits` at submit; `formatArs` is display-only.

**Functional artifacts**
- Guards, resolvers, interceptors are `const` functions (`CanActivateFn`, `ResolveFn<T>`,
  `HttpInterceptorFn`) — never classes. Interceptor files use the dotted `*.interceptor.ts`
  form (D7); other helpers (`poll-until.ts`, `skip-error-mapping.ts`) stay hyphenated.

**Forms**
- Reactive only. Built in a `private initXForm(): void` via `FormBuilder.group()`, called
  from `ngOnInit`. Cross-field validators at the group level. Error copy via an
  `ErrorConfig` map keyed on validator name.

**Errors**
- User-facing messages key off the error **`code`** (`AppError.code`), never the `detail`
  text. The API always sets the HTTP status from the code — trust it.

**Cleanup**
- The one pattern: `private destroy$ = new Subject<void>()` + `takeUntil(this.destroy$)` +
  `ngOnDestroy` (`implements OnInit, OnDestroy` explicitly). Not `takeUntilDestroyed` /
  `DestroyRef`. Fire-and-forget `.subscribe()` only for a single one-shot side effect.

## Testing

- Every service gets an `HttpTestingController` spec asserting URL, verb, body, envelope
  unwrap, and `AppError` mapping. `money` and `problemDetailsInterceptor` get focused unit
  specs.
- `TestBed.configureTestingModule` must include `provideZonelessChangeDetection()` (zoneless
  project — a bare config throws `NG0908`).
- Timer/polling specs use `TestScheduler` marbles, not `fakeAsync`.
- Karma `fileReplacements` are **not** applied — specs compute expected URLs from
  `environment`, never hardcode them.

## Commands

```
pnpm ng build                                # prod build
pnpm ng build --configuration development     # dev build (swaps in environment.development.ts)
pnpm ng lint                                  # angular-eslint flat config
CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless
```

If no Chrome/Chromium is installed, point `CHROME_BIN` at any Chromium-based browser —
Brave works (`/usr/bin/brave`). Dev API is `https://localhost:7095/v1` (+ `/health` at the
host root); the browser needs the ASP.NET dev cert trusted: `dotnet dev-certs https --trust`.

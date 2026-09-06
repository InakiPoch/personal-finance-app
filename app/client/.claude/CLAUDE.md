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

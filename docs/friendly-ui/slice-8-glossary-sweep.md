# Slice 8 — Plain-language sweep across the whole client

> Read `00-overview.md` (glossary) first. Client-only. Client Phase 54. Runs last so it catches everything the
> earlier slices didn't rewrite.

## Goal

Every user-visible string speaks the user's language: no developer words ("API", "client", "endpoint", "UUID",
"the ledger movement feed", "the client computes nothing"), no accounting jargon ("accrued", "statement",
"liability", "receivable", "storno", "reversal", "cycle"), no stray Spanish in English copy ("cuota"). Replace
using the glossary in `00-overview.md`. Then **lock it in** with a spec so it can't creep back.

Scope is **copy only** — templates (`.html`) and hand-written user-facing strings in `.ts` (error maps, labels,
toasts). Identifiers, routes, CSS classes, type names and API field names do **not** change (`statementId`,
`/financing/statements`, `StatementPage` stay).

## Current state (verified 2026-09-29, before slices 1–7)

- API error text never reaches the UI: `C/core/http/problem-details.interceptor.ts` → `AppError`; each page maps
  `error.code` to its own sentence (`submitErrorMessages` / `errorMessages`). So all copy is client-side.
- Hit counts (user-visible lines): `statement-page.html` 10, `dashboard-page.html` 10 (slice 5 rewrites most),
  `reverse-movement-page.html` 9 (slice 7 rewrites), `statements-table.html` 8, `statements-page.html` 6,
  `load-expense-page.html` 6, `party-detail-page.html` 4, `load-expense-page.ts` 3, `record-income-page.ts` 2,
  `subscriptions-page` 2, `party-detail-page.ts` 2, `instruments-page.html` 2, `timeline-table.html` 2, and 1 each
  in `transactions-page.html`, `subscriptions-page.html`, `parties-page.html`, `installments-table.html`,
  `statement-page.ts`, `creditor-detail-page.ts`.
- "Personal ledger" eyebrow on ~16 pages (`<p class="font-serif text-sm italic text-stamp">Personal ledger</p>`).
- Representative offenders:
  - `load-expense-page.html:8-9` "The API allocates the billing cycle and … per-party cents — the client computes neither."
  - `load-expense-page.html:313-314, 429` "The API rounds the per-party cents and posts the receivables…",
    "the receivable is being posted, refresh shortly"
  - `instruments-page.html:9` "The list is served by the API."
  - `party-detail-page.html:6-7` "The API nets the balance … the client computes nothing."; `:91`, `:142` "Settling posts a ledger transaction…"
  - `subscriptions-page.html:145` "The API bills monthly subscriptions only for now."
  - `transactions-page.html:8` "The ledger movement feed, newest first." (slice 7 replaces)
  - `.ts`: "The API rejected the expense — check the amount and dates." (`load-expense-page.ts:139`);
    "That account is not registered with the API." (`record-income-page.ts:66`);
    "This cuota is not accrued for party payments yet." (`creditor-detail-page.ts:146`).
- "installment" (47 hits) **stays**.

Re-run the sweep at the start of this slice — slices 1–7 changed many of these lines:

```
rg -n -i "\bapi\b|endpoint|uuid|accru|statement|liabilit|receivable|storno|reversal|\bcycle|cuota|personal ledger|the client" app/client/src/app --glob '*.html'
rg -n -i "'[^']*(\bapi\b|accru|statement|liabilit|receivable|storno|reversal|cuota)[^']*'" app/client/src/app --glob '*.ts' --glob '!*.spec.ts'
```

## Rewriting rules

- Describe the **outcome for the user**, not the mechanism. "The API rounds the per-party cents and posts the
  receivables" → "Each person's share is rounded to the cent and added to what they owe you."
- "Statement" pages keep their route; visible text becomes "card bill" ("Statement detail" → "Card bill",
  "Pay a statement" → "Pay a card bill"). The nav/page name "Credit Card Cycles" (slice 1) stays — user's choice.
- "Reversal" → "undo entry" / "undo"; the action name "Reverse a Transaction" stays.
- Error sentences say what to do next ("Check the amount and dates and try again."), never who rejected it.
- Keep sentences short; keep the existing tone of `app/client/docs/SYSTEM.md`.
- Remove the "Personal ledger" eyebrow `<p>` everywhere (don't replace it).

## Guard spec

Add `C/copy-glossary.spec.ts` (or similar, colocated with `app.spec.ts`) that fails when a banned term appears in
rendered page text. Cheapest reliable way in Karma: import the page templates as raw strings is not available by
default in Angular's builder, so instead **render each routed page component** in a loop with its services
stubbed to empty responses and assert `fixture.nativeElement.textContent` has no match for
`/\b(API|accrued?|accrual|liability|receivable|storno|reversal|cuota|Personal ledger)\b/i` and no
`/\bstatements?\b/i`. If stubbing every page is too heavy, fall back to a Node script `scripts/check-copy.mjs`
(`rg`-style regex over `*.html`) wired into `pnpm lint` — choose one and say which in the step report.
Allow-list the literal "Credit Card Cycles" (contains no banned term anyway) and code identifiers (the check runs on
text, not attributes).

## Test plan

- The guard spec/script above, green.
- Existing specs that assert old copy — update expectations (grep specs for the old sentences).

## Steps

- [ ] 3. Client prod — sweep all templates + `.ts` strings; remove eyebrows. Lint + prod build clean.
- [ ] 4. Client specs — guard + updated expectations. Green.
- [ ] 5. Doc-sync — client `TASK.md` Phase 54; client `CLAUDE.md` evergreen: "UI copy follows the glossary in
      `docs/friendly-ui/00-overview.md`; enforced by <guard>". Also add the glossary pointer to
      `app/client/docs/SYSTEM.md` (voice section). Closes the friendly-ui initiative.

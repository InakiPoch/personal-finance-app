# Slice 1 — Grouped navbar + page renames

> Read `00-overview.md` first. Client-only. Client Phase 47. No API change.

## Goal

The navbar today is 11 flat links in arbitrary order with mixed capitalisation. The user wants it
split into three evenly spaced blocks by purpose, the action pages emphasised as actions, and several
pages renamed.

| Block | Items (in this order) | Purpose |
|---|---|---|
| 1 — Views | Dashboard · Recent Money Movements · Owed to Creditors · Parties · Subscriptions · Credit Card Cycles | Look at your money |
| 2 — Setup | Cards and Accounts · Creditors | Things you register once |
| 3 — Actions | **Load an Expense** · **Reverse a Transaction** | Things you *do* — styled as buttons |

The blocks are spread across the bar (block 1 left, block 2 centre, block 3 right, e.g.
`justify-content: space-between` on three groups). Block 3 items look like buttons (filled/outlined,
per `app/client/docs/SYSTEM.md` action styling), not plain links, so they read as "actions".

Renames (label + the page `<h1>`, routes unchanged per D3):

| Old | New | Route (unchanged) |
|---|---|---|
| Instruments | Cards and Accounts | `/instruments` |
| Statements | Credit Card Cycles | `/financing/statements` |
| Reverse (page says "Transactions") | Reverse a Transaction | `/ledger/transactions` |
| Money Flow | Recent Money Movements | `/ledger/money-flow` |
| Recent purchases | Recent Credit Card Purchases | `/financing/recent-purchases` |
| Load expense | Load an Expense | `/financing/load-expense` |
| Owed to creditors | Owed to Creditors | `/financing/creditor-payables` |

"Recent purchases" leaves the nav entirely (the user's block list doesn't include it) but keeps its
page and the dashboard quick action (slice 5 moves that action to the top).

> Note: "Credit Card Cycles" contains "cycle", which the glossary replaces with "billing month" in
> body copy. The page *name* is the user's explicit choice — keep it. Body copy on that page still
> follows the glossary (slice 8).

## Current state (verified 2026-09-29)

- `C/app.ts:14-26` — `navItems: NavItem[]` flat array, `type NavItem = { label; path }`.
- `C/app.html` — one `<ul class="primary-nav__list">` with `@for` over `navItems`,
  `routerLinkActive="primary-nav__link--active"`. Styles in `C/app.css`.
- `C/app.spec.ts` — existing root spec; extend it.
- Page titles: `<h1>` at line ~4 of each page:
  - `C/features/instruments/pages/instruments-page/instruments-page.html`
  - `C/features/financing/pages/statements-page/statements-page.html`
  - `C/features/ledger/pages/transactions-page/transactions-page.html` (says "Transactions")
  - `C/features/ledger/pages/money-flow-page/money-flow-page.html` (says "Money Flow")
  - `C/features/financing/pages/recent-purchases-page/recent-purchases-page.html:5`
  - `C/features/financing/pages/load-expense-page/load-expense-page.html`
  - `C/features/financing/pages/creditor-payables-page/creditor-payables-page.html`
- "Recent purchases" also appears as a dashboard quick-action label
  (`C/features/reports/pages/dashboard-page/dashboard-page.html:260`) → rename to "Recent Credit Card Purchases".
- Page subtitles under the renamed titles may still use jargon; only fix those if they contradict the new
  title (e.g. "The ledger movement feed" under "Reverse a Transaction" → "Pick a transaction to undo it.").
  Full copy sweep is slice 8; slice 7 rewrites the Reverse page anyway.

## Client changes

1. `app.ts` — replace the flat array with groups (no new abstraction beyond a type):
   ```ts
   type NavItem = { label: string; path: string };
   type NavGroup = { label: string; kind: 'view' | 'setup' | 'action'; items: NavItem[] };
   protected readonly navGroups: NavGroup[] = [ ... ];
   ```
   `label` is for `aria-label` on each group (`"Views"`, `"Setup"`, `"Actions"`).
2. `app.html` — `@for` groups → a `<ul>` per group (each with `aria-label`), `@for` items inside.
   Action group links get an extra class (e.g. `primary-nav__link--action`).
3. `app.css` — three groups laid out with space between; action link button styling; keep
   `--active` state visible for all kinds. Must still wrap sensibly on narrow screens (flex-wrap).
4. Rename every `<h1>` / label listed above.

## Test plan (client specs)

- `app.spec.ts`:
  - renders exactly 3 nav groups, in order Views / Setup / Actions;
  - group 1 labels in the exact order from the table; group 2 and 3 likewise;
  - action links carry the action class;
  - no link text "Instruments", "Statements", "Reverse" (exact), "Money Flow".
- Existing page specs that assert the old `<h1>` text (grep specs for `'Transactions'`, `'Money Flow'`,
  `'Recent purchases'`, `'Instruments'`, `'Statements'`) — update to the new titles.

## Steps

- [x] 3. Client prod — groups, styles, renames. Lint + prod build clean.
- [x] 4. Client specs — as above. Green.
- [x] 5. Doc-sync — client `TASK.md` Phase 47 line; client `CLAUDE.md` only if it documents nav structure
      (it does list views — update names there).

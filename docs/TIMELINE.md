# Timeline

Condensed history of delivered work. Phase numbers refer to the ledgers in `app/api/.claude/TASK.md` and `app/client/.claude/TASK.md`. Dates are from those ledgers; the earliest foundation phases (before Sept 2026) are not itemised here.

| Date | API / client phases | Delivered | ADR |
|---|---|---|---|
| 2026-09-04 | API 13–14 / client 5–6 | Creditors CRUD; creditor fields on Load Expense | [0007](adr/0007-card-xor-creditor-on-payment-plan.md) |
| 2026-09-04/05 | API 15–17 / client 7–9 | Required expense description; card-debt drill-down; Recent Purchases | 0007 |
| 2026-09-05/06 | API 18–20 / client 10, 20–21 | Card XOR creditor; Owed to Creditors list; debit/cash expenses + categories | 0007, [0012](adr/0012-income-and-money-flow-model.md) |
| 2026-09-06 | API 21–22, 24–25 / client 22–25 | Dashboard fixes (paid-of-total, card name on future rows, Parties list); card-split "scheduled" state and future shares; DueCycle reframe | [0004](adr/0004-store-close-cycle-derive-due-cycle.md) |
| 2026-09-07 | API 26–33 / client 25–32 | Uniform creditor cutoff 26 and due-month split accrual; pending-shares; installment `PaidOnUtc`, pay single installment, next-payment visibility; back-dated purchases; pending $ | [0005](adr/0005-split-receivables-accrue-at-due-month.md), [0006](adr/0006-creditor-cutoff-26-vs-calendar-month.md), [0008](adr/0008-card-payments-derive-from-installments.md), [0009](adr/0009-backdated-purchases-settle-at-creation.md) |
| 2026-09-08 | API 34–37 / client 33–36 | Owed to Creditors: due-now vs total, detail, pay/undo installment, pay full debt | [0003](adr/0003-creditor-debt-is-display-only.md) |
| 2026-09-16/17 | API 38–40 / client 37–39 | Subscriptions rework: explicit pay, live period, undo | [0002](adr/0002-subscriptions-are-paid-explicitly.md) |
| 2026-09-24/25 | migrations 09-24; API 45–47 / client 40–42 | USD support (ledger, financing, subscriptions, splits); record income, Money Flow, undo income | [0001](adr/0001-currency-on-the-record-no-fx.md), 0012 |
| 2026-09-28 | API 48–51 / client 43–46 | Partial creditor payments: payment rows, per-expense, full-debt, party share | 0003 |
| 2026-09-29 | — | "Friendly UI" planning (8 slices) | |
| 2026-09-30 | API 52–56.1 / client 47–54.1 | Friendly UI: navbar, currency toggle, shared-expense removal, `<name> Subscription`, month-driven dashboard, closing dates, transaction feed with reverse impact, glossary sweep | [0010](adr/0010-closing-dates-overrides-and-rebucket.md), [0011](adr/0011-transaction-feed-built-in-reporting.md), [0015](adr/0015-friendly-ui-vocabulary-and-route-stability.md) |
| 2026-10-02 | — | Public-main release research, council (concluded "do not execute yet"), then `release-setup` plan chosen: build from source | [0013](adr/0013-main-is-a-generated-source-projection.md) |
| 2026-10-03 | API 57–58 | Runnable container (migrate on startup); smoke script and `docker-smoke` CI job | [0014](adr/0014-localhost-http-default-optin-https-migrate-on-start.md) |
| 2026-10-04 | API 59–62 | Opt-in HTTPS; projection + leak guard + README + MIT; release workflow, `v1.0.0` released; repo public, rulesets | 0013, 0014 |

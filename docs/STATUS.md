# Status

As of 2026-10-08 (API through Phase 62, client through Phase 54.1). Release `v1.0.0` is public. Every planned feature initiative has shipped; this file lists what exists and what is still open. Detailed ledgers: `app/api/.claude/TASK.md`, `app/client/.claude/TASK.md`.

## Shipped capabilities

| Area | What works today |
|---|---|
| Ledger | Double-entry, per-currency; debit/cash expenses with categories and optional split; manual income; reversal (undo entry) with global double-reversal guard |
| Credit cards | Plans with installments, back-dating, statements/card bills, pay statement or single installment, editable closing dates with re-bucketing |
| Creditors | Creditor-financed plans, owed-to-creditors list + detail, partial/per-expense/full-debt payments with undo-last, party-share payment |
| Parties | Splits (card, creditor, debit), roster, balance/timeline, scheduled shares, settlements |
| Subscriptions | Explicit pay/undo, `<name> Subscription` accounts, month-driven dashboard block |
| Currency | ARS and USD end to end (debit, card, subscriptions, creditors), never converted |
| Dashboard | Month picker drives every card; Due this month; spent from bank & cash; money received; Recent Money Movements |
| Reporting | Monthly expenses/incomes, money flow, rich transaction feed with reversal impact text |
| Distribution | Public repo, `docker compose up` builds from source, opt-in HTTPS, tag-driven release to a generated `main` |

## Open items

**Verify / likely bugs**
- Dashboard drill-down "paid of total" (`GetCardPurchasesHandler`) reads statement paid state, not `Installment.PaidOnUtc`; a cuota paid individually on an open statement may still count as outstanding.
- Reversing a card-bill payment does not un-mark installments paid; reversing a subscription charge via the feed does not roll back its paid period.
- Money Flow vs Out of pocket diverge for cross-month reversals (storno dated `UtcNow`).
- Mixing codes `PlanNeedsCardOrCreditor` / `PlanCannotMixCardAndCreditor` / `CreditorAccount*` fall through to HTTP 400; confirm intended.
- Parties/USD: settlement UI for a non-default currency was not verified (`SettleCurrentAccountCommand` defaults to ARS).
- Whether the one-time subscriptions wipe SQL was run on the live DB cannot be verified from code.

**Open decisions**
- Feed from→to direction for Income reads opposite to the original mock.

**Release**
- First real release through protected `main` (v1.0.1); owner README read-through; clean-clone browser walkthrough; HTTPS trust beyond Linux Chrome/Brave; mid-migration kill recovery (see [`RELEASING.md`](RELEASING.md)).

**Tech debt**
- `formatArs` shim in `core/money/money.ts` still used by `creditor-purchases-table.ts` and `load-expense-page.ts`.
- Double-reversal guard is check-then-insert (unique filtered index on `OriginalTransactionId` is the upgrade).
- Party-share compensation is best-effort (no outbox saga).
- Unnamed parties render as "Party {guid}" in reports (`vw_party_names` deferred).
- `GET /financing/cards/{id}/purchases` has an undocumented `month` filter.

## Backlog (not built, not scheduled)

Income categories · recurring incomes · net figure · subscription creation/cancel dates (fixes by-month history) · pay-all-overdue subscriptions · creditor bulk undo (intentionally absent) · editable expense descriptions · category management/rename · creditor edit/delete (unverified) · bank closing on the 1st of the next month (needs full-date overrides) · outbox saga for party share · GHCR image · FX / net worth (out of scope).

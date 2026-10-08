# 0011 — The reverse/transaction feed is built at read time in Reporting

**Status**: accepted (2026-09-30, API Phase 56)

**Decision**: `GET /v1/reports/transactions[/{id}]` replaces the Ledger transactions list. Reporting reads cross-module `vw_*` views (`vw_ledger_transaction_legs`, `vw_installment_labels`, `vw_subscription_names`, …) and a pure C# `TransactionExplainer` builds the badge, description, from→to line and "If you reverse this" text. It is the single source of undo wording and must mirror `ReversalCalculator.Decide`.

**Why**: Ledger cannot see plan, card, subscription or party names; read-time building needs no backfill and is testable without a DB.

**Rejected**: storing descriptions in Ledger.

**Open**: for some kinds (e.g. Income) the from→to direction reads opposite to the original mock — unresolved with the user. Unnamed parties render as "Party {guid}" (a `vw_party_names` view is deferred).

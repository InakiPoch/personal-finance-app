# 0015 — Friendly UI vocabulary; labels change, routes never do

**Status**: accepted (2026-09-29/30, client Phases 47–54)

**Decision**: User-facing copy follows the "UI vocabulary" table in [`GLOSSARY.md`](../GLOSSARY.md) in every view. Routes never change when labels do (no broken bookmarks or route specs). Dashboard numbers keep their meaning; only labels move. The client owns all copy: it never renders API error text and maps `error.code` to its own sentence. The shared-expense page and `POST /v1/parties/shared-expenses` were deleted (the `RegisterSharedExpenseCommand` chain stays for the debit-split path); splitting now starts from the party page via Load an Expense `?party=<id>`. Subscription accounts are renamed `<name> Subscription` by a data migration matching the template account id. A segmented ARS | USD control replaced the faint select (`$` for ARS, `US$` for USD).

**Guard**: `copy-glossary.spec.ts` renders the routed pages and fails on banned terms.

**Known leftovers**: a `formatArs` shim remains in `core/money/money.ts` and is still referenced by a couple of non-spec files; the Record Income currency select was left out of the toggle.

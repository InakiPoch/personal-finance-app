# 0006 — Creditor cutoff day 26 everywhere except the dashboard's "Due this month"

**Status**: accepted (2026-09-07 and 2026-09-30, API Phases 32, 54)

**Decision**: Creditor plans resolve their first cycle with a fixed `CreditorCutoffDay = 26` through the same `ResolveCycle` as cards (purchases after the 26th first fall due in month+2). The **Owed to Creditors** page's "Due now" uses that rule. The dashboard's **Due this month** uses the plain calendar month for cards and creditors.

**Why**: one code path for cycle math, no card-vs-creditor branching; the dashboard answers "what do I pay this month", which the user corrected away from cutoff 26.

**Consequence**: the two numbers can differ around the 26th. Deliberate; not unified.

**Rejected**: plain monthly creditors from purchase date; sharing one predicate for both surfaces.

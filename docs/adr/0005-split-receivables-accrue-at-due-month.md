# 0005 — Split receivables accrue per installment at the due month

**Status**: accepted (2026-09-07, API Phase 26 — supersedes Phase 23)

**Decision**: For card and creditor-financed splits alike, the co-borrower receivable ("Split receivable — due month") posts per installment when `DueCycle ≤ current month` (scheduler Gate 3, `AccrueInstallments`). Until then the share is *scheduled*, served by `GET /v1/parties/{id}/future-shares` and `GET /v1/parties/pending-shares`. The client shows a "scheduled" state and does not poll for a balance change. Future shares are computed in Financing C# with `PhantomPennyAllocator`.

**Why**: the party owes $0 until the due month, but the schedule must be visible; SQL cannot reproduce the allocator's tie-breaking, and Reporting stays views-only.

**Rejected**: accruing at statement close; booking the full receivable up front for creditor splits (Phase 23, reverted); a Reporting SQL view; storing the purchase month on creditor plans.

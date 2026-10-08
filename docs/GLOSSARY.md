# Glossary

Domain language shared by `app/api` and `app/client`. Use these terms in code, tests, issues and UI copy. Terms marked **UI** have a different user-facing word; the "Avoid" column lists words that must not appear in UI copy.

## Money & currency

| Term | Definition |
|---|---|
| **Money** | `(MinorUnits long, Currency)`. Always minor units on the wire and in the DB. Mixed-currency arithmetic throws. |
| **Currency** | Closed set `ARS` \| `USD`, 2 decimals each. `Currency.Reference` aliases ARS. No FX, ever. |
| **Partition by currency** | Never sum without grouping by currency (API and client). Totals are shown per currency. |
| **Poly-currency account** | One account can hold ARS and USD entries; balances are per (account, currency). Each transaction is single-currency. |

## Ledger

| Term | Definition |
|---|---|
| **Transaction** | Balanced double-entry posting. Has a `Description`; historic rows fall back to the category name. |
| **Reversal / storno** (UI: **undo entry**) | Append-only mirror transaction with `OriginalTransactionId`. An original can be reversed once (409 `Ledger.TransactionAlreadyReversed`); a reversal cannot be reversed. |
| **Income** (UI: **Money received**) | Money entering a Bank/Cash account: `Dr Bank/Cash / Cr Income` plus every party settlement, whatever it repays. Excludes card refunds and reversals. |
| **Out of pocket** (UI: **Spent from bank & cash**) | Everything that left my Bank/Cash accounts in a month: my expense share plus whatever I fronted for parties (loans, debit-split shares). Card purchases and card bill payments are excluded. |
| **Money Flow** (UI: **Recent Money Movements**) | Table of my movements, one row per transaction at the full amount that left or entered Bank/Cash; party movements flagged ("Lent to", "Shared with", "Paid back by"); reversed pairs hidden; undo on income, loan and settlement rows. |
| **Category** | No entity. It is the name of an Expense-kind ledger account, get-or-created case-insensitively. |

## Credit cards

| Term | Definition |
|---|---|
| **PaymentPlan / purchase** | One loaded expense split into installments. Card-backed **XOR** creditor-financed (`CardId` nullable). |
| **Description** | Required plan label: trimmed, 1–120 chars, single line. Lives on `PaymentPlan` only, never on the ledger. |
| **Installment** (Avoid: *cuota*) | One monthly row of a plan: `Sequence`, `Amount`, close cycle. |
| **Statement / MonthlyStatement** (UI: **card bill**; Avoid: *statement*) | A card's bill for one close cycle. |
| **Cycle (close cycle)** (UI: **billing month**) | Statement-close month, stored on the installment. Statement surfaces key on it. |
| **DueCycle** | `Cycle + 1 month`: the month money moves. Derived, never stored. Payment surfaces key on it. |
| **Usual closing day / closing override** | Card default closing day plus per-month override. Months that already have a statement are locked. |
| **Accrual** (UI: **charged to the card**) | Scheduler posts `Dr CardExpense / Cr CardLiability` once a cycle closes. |
| **Upcoming** (was *future*) | Installments not charged yet. |
| **Back-dated / Paid from** | Purchase date in the past. Card plans need a `BankAccountId` to fund elapsed installments. |
| **Elapsed installment** | DueCycle earlier than the current calendar month. Auto-paid at creation. |
| **Installment `PaidOnUtc`** | Fully paid (remaining = 0). Statement `PaidOnUtc` = statement fully settled. |
| **Pending $** | Σ unpaid, non-reversed installment amounts of a plan. |
| **Next payment** | DueCycle of the earliest unpaid, non-reversed installment; "Fully paid" when none. |

## Creditors

| Term | Definition |
|---|---|
| **Creditor** | Payee of a creditor-financed purchase (Financing entity). Not an Instrument, not a Party. |
| **CreditorAccount** | Payee's destination (label + optional identifier). |
| **Creditor payment** | Display-only: no bank, no ledger. One `CreditorInstallmentPayment` row per payment; undo removes the latest. |
| **Remaining** | `Amount − Σ payments`. "Partial" is not a status; statuses are paid / due / overdue / upcoming / reversed. |
| **Due now** | Σ remaining where DueCycle ≤ current creditor cycle (cutoff day 26, arrears included). |
| **Total owed** | Σ remaining over all unpaid installments. One row per (creditor, currency). |
| **Waterfall** | Pay fills installments in order until the amount runs out. Per expense: by `Sequence`. Full debt: by DueCycle, PurchaseDate, Sequence. |

## Parties & splits

| Term | Definition |
|---|---|
| **Party** | Person who owes me money, through shared expenses or loans; has one receivable account. |
| **Split / party share** | A party's fixed share of a purchase. Computed with `PhantomPennyAllocator` over `[holder, participants ordered by PartyId]`. |
| **Loan** (UI: **Money lent**) | Money I give a party from one of my Bank/Cash accounts, not tied to any purchase. Always owed to me; borrowing from a party is not a loan. |
| **Receivable** (UI: **owed to you**) | What a party owes me: split shares plus loans, pooled per (party, currency). Card and creditor splits accrue it at DueCycle per installment. |
| **Scheduled** | Share not yet accrued; served by `GET /v1/parties/{id}/future-shares`. |
| **Settled up** | $0 posted **and** nothing scheduled. |
| **Settlement** | `Dr Bank / Cr Receivable_party` via `SettleCurrentAccount`. |

## Subscriptions

| Term | Definition |
|---|---|
| **Subscription** | Monthly template with a flat amount; account named `<name> Subscription`. |
| **Pay** | Explicit: posts one charge dated now and advances one period. Undo reverses via the ledger and steps back one period. |
| **LastPaidPeriod / NextDueDate** | Anchor of the last paid period / due date of the next unpaid period. |
| **Status** | `paid` (LastPaidPeriod in current month), `overdue` (NextDueDate ≤ today, not paid this month), `upcoming`. |
| **Assume-paid registration** | At creation, if this month's anchor ≤ today, post one charge at the anchor and mark it paid. |

## Dashboard

| Term | Definition |
|---|---|
| **Due this month** | Unpaid card + creditor installments with DueCycle ≤ the selected **calendar** month, per currency, overdue included. Deliberately not the creditor cutoff-26 rule. |
| **Owed to you (dashboard)** | Per (party, currency): receivable entries dated ≤ end of the selected month + Scheduled shares with DueCycle ≤ that month. Only positive amounts shown; none → "No debts to settle". |
| **Month picker** | Drives every dashboard card. |

## UI vocabulary (replaced jargon)

statement → **card bill** · cycle → **billing month** · accrued → **charged to the card** · future → **upcoming** · liability → **what you owe** · receivable → **owed to you** · reversal/storno → **undo entry** · cuota → **installment** · Instruments → **Cards and Accounts** · cutoff day → **Usual closing day** · Money Flow → **Recent Money Movements**. Never shown to users: "API", "endpoint", "UUID". Route paths never change when labels do. Page name "Credit Card Cycles" and action "Reverse a Transaction" were kept on purpose. A guard spec (`copy-glossary.spec.ts`) fails on banned terms.

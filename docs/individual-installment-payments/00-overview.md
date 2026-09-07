# Individual Installment Payments — Overview

> **Status:** planning only. No code written yet. This folder holds three vertical-slice
> specs. Execute them **in order**, one at a time, and only move to the next slice once the
> current one is green (build + tests) and behavior-consistent.

## Why this change exists

Today a credit-card **statement** (`MonthlyStatement`) can only be paid **all-or-nothing**:
`PayStatementHandler` always charges the full `AmountDue` (`Dr CardLiability / Cr Bank`,
netting any carried card credit) and flips `IsPaid`.

But a statement bundles cuotas from several purchases. Example — the October statement holds
cuota-3 of Purchase A, cuota-1 of Purchase B and cuota-5 of Purchase C. In real life the user
often can only settle **some** of them now.

**Goal:** let the user pay a single purchase's installment **in full**, leaving the others
owed, so that:

- the **statement** discounts the settled cuota and becomes *partially settled*, and
- each **purchase** clearly shows "N of M paid" and "next payment will be `<month>`".

## The mental-model gap you must keep in mind

A purchase (`PaymentPlan`) is **already pre-sliced** into `Installment` rows — **one row per
month**, each pinned to a fixed billing cycle (`CycleYear/Month`, with `DueCycle = Cycle + 1`).
So "installment 4 of 12" is **not a pointer that advances on payment** — it is a row that
already lives in a future statement.

Therefore:

- Paying cuota-3 does **not** reschedule cuota-4. Nothing moves.
- **"Next payment for this purchase" is a pure derived read:** the earliest un-paid,
  un-reversed installment and its `DueCycle`.
- There is **no** per-installment paid flag today; an installment counts as "paid" only
  because its whole statement was marked paid. Slice 1 adds that flag.

## Decisions locked with the user (do not re-litigate)

1. **Unit of payment = one installment (cuota) paid in full.** "Partial" describes the
   *statement*, never a fractional cuota. No remainder / carry / minimum-payment — this app
   has **no interest engine**.
2. **"Next payment" is derived, no rescheduling.**
3. **Keep both actions:** "Pay full statement" **and** per-installment pay coexist.
4. **Credit-card installments only.** Creditor-financed (`CreditorPayables`) and debit/cash
   are out of scope.
5. **Paid state on the installment:** nullable `Installment.PaidOnUtc` + `MarkPaid` (double-pay
   guard); statement status derived from its installments. **One EF migration.**
6. **Ledger — Option A:** an individual cuota payment posts a plain
   `Dr CardLiability(amount) / Cr Bank(amount)`. Carried-credit netting stays **only** on the
   full-statement path.
7. **UI:** per-row **Pay** button on the statement `InstallmentsTable`, sharing one
   bank-account + paid-on-date selector; relabel the existing button **"Pay full statement."**
   "Next payment" surfaces on the **Recent Purchases** row.
8. **Payable rule = Option B (correct it):** statement payable =
   **Σ (accrued, non-reversed, unpaid) installment amounts** — **not** the stored `AmountDue`.
   See the reversal note below.

## Critical verified fact — the reversal / `AmountDue` divergence

`MonthlyStatement.AmountDue` is **only ever incremented** (`MonthlyStatement.cs:30`,
`AmountDue += installment.Amount`); there is **no `-=` anywhere** in the module.
`Installment.MarkReversed()` (`Installment.cs:49`) only flips a flag. **So `AmountDue` still
includes reversed cuotas.**

But reversing an *accrued* installment **always reduces the ledger `CardLiability`** via a
storno `Dr CardLiability / Cr CardPurchases` (`ReverseTransactionHandler.cs:38`), plus a
compensating `Dr CardCredit / Cr CardLiability` + `CreditCard.ApplyCredit` when the statement
was already paid (`ReversalCalculator.cs:35`, `MarkInstallmentReversedHandler.cs:31`).

**Consequence:** charging the stored `AmountDue` on a statement that has a reversed cuota
**over-debits the liability and overpays from the bank** — a latent bug today. Computing the
payable as Σ (accrued, non-reversed, unpaid) installment amounts matches what the ledger
liability already reflects, so it **fixes the overpay** and makes individual-pay and full-pay
reconcile. This is an **intentional correction**, behavior-preserving for reversal-free
statements. Slice 1 must state it plainly and cover it with a test.

## Slice map

| Slice | Title | Delivers | Doc |
|-------|-------|----------|-----|
| 1 | Foundation: paid-state + payable-from-installments | `Installment.PaidOnUtc`, migration, `PayStatement` charges the installment-derived payable (isolated ledger refactor) | [slice-1](slice-1-foundation-payable-from-installments.md) |
| 2 | Pay a single installment | New `POST /v1/financing/installments/{id}/pay`, per-row Pay UI, "Pay full statement" relabel | [slice-2](slice-2-pay-single-installment.md) |
| 3 | "Next payment for this purchase" visibility | Recent Purchases shows "N/M paid · next: `<month>`" | [slice-3](slice-3-next-payment-visibility.md) |

**Order matters.** Slice 1 isolates the risky ledger change **before** any new payment path
exists, so it is provably behavior-preserving for reversal-free statements. Slice 2 then builds
individual-pay on top of an already remainder-aware full-pay handler — no double-pay window.
Slice 3 is pure additive read.

## Repository facts a fresh session needs

- Monorepo, two independently-built sub-projects: `app/api/` (.NET 10 backend, modular
  monolith — Ledger / Financing / Reporting, CQRS + Outbox) and `app/client/` (Angular 20).
  Read each sub-project's own `.claude/CLAUDE.md` before working in it.
- **API build/test** (from `app/api/`): `dotnet build`;
  `dotnet test --project tests/PersonalFinance.Financing.Tests`.
- **Client build/test** (from `app/client/`): `pnpm ng lint`;
  `CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless`.
- Money is always **minor units (cents)**, never floats. Client: `Money` branded type in
  `src/app/core/types/money.ts`; helpers `fromMinorUnits` / `toMinorUnits` / `formatArs` in
  `src/app/core/money/money.ts`.
- The client hand-mirrors DTO types from the API's OpenAPI doc — there is **no codegen and no
  shared-types package**. Every API shape change needs a matching hand edit in
  `app/client/src/app/features/financing/types/`.

## Out of scope

Fractional/partial cuota amounts; creditor-financed & debit payments; interest/carry/minimum
payment; rescheduling remaining installments; dashboard "Card debt by cycle" changes.

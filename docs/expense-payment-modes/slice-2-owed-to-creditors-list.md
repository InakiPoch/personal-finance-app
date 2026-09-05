# Slice 2 — "Owed to creditors" list

> **Part of** the "expense payment modes" effort. Depends on **Slice 1** (creditor-financed plans must
> exist first). Self-contained: a fresh session should execute it from this doc alone.

---

## 1. Context — the gap this slice closes

Slice 1 lets the user record a **creditor-financed** expense (a card-less `PaymentPlan` carrying a
`CreditorId` + `CreditorAccountId`, with a plain monthly schedule). But there is nowhere to **see** that
debt. The user's stated need is simple and concrete: *"I want to immediately recognize — oh, I need to pay
this to this person."* The creditor **label** is enough.

This slice ships the smallest thing that satisfies that: a **read-only "Owed to creditors" roll-up** —
each creditor and what is still outstanding to them.

**Relevant locked decisions:**
- **D7** — Creditor debt is **Financing-only**. This list reads straight from Financing `PaymentPlan`s;
  it does **not** touch the Ledger.
- **D8** — Minimal payables roll-up only. **Not** the full monthly financial table.
- **D10** — The full financial-table dashboard is **out of scope** (a future session).

**Non-goals:** no gains/losses, no debit/cash spending, no Ledger reads, no charts. Just "who do I owe, and
how much is left."

---

## 2. Current state

- **Creditor-financed plans** exist after Slice 1: Financing `PaymentPlan`s with a non-null `CreditorId`,
  a `CreditorAccountId`, installments, and a plain schedule. Domain in
  `app/api/src/Modules/Financing/PersonalFinance.Financing/Domain/PaymentPlan.cs`; installments carry
  amount + due date.
- **Creditor names** live in `.../Financing/Domain/Creditor.cs` (`Creditor.Name`) and each
  `CreditorAccount` has a `label`. List endpoint `GET /v1/creditors` already returns
  `{ id, name, accounts: [{ id, label, identifier }] }`
  (client type: `app/client/src/app/features/creditors/types/creditor.ts`).
- **Existing read-model precedent:** the Financing module already exposes query endpoints (e.g.
  `GET /v1/financing/...` for card purchases/statements) built as CQRS read queries in the host under
  `src/Bootstrap/PersonalFinance.Api/Endpoints/`. Follow that same shape — do **not** invent a new pattern.
- **No** "creditor payables" endpoint or view exists yet.

---

## 3. Target behavior

**API** — a read query that, over all `PaymentPlan`s with a `CreditorId` set, groups by creditor and
returns each creditor's outstanding total (sum of not-yet-paid installments) plus, optionally, the next
due date and per-account breakdown.

Suggested contract: `GET /v1/financing/creditor-payables` →
```jsonc
{
  "rows": [
    {
      "creditorId": "…",
      "creditorName": "Juan",
      "outstandingMinorUnits": 45000,
      "nextDueDate": "2026-03-10",          // optional; earliest unpaid installment
      "accounts": [                          // optional; how to pay them
        { "accountId": "…", "label": "Galicia", "outstandingMinorUnits": 45000 }
      ]
    }
  ]
}
```
"Outstanding" definition for this slice: sum of installments whose due date is in the future / not marked
paid. If the plan model has no "paid" flag yet, treat **all** installments of a creditor plan as
outstanding and note it as a known limitation (payments are a later concern, consistent with D8/D10).

**Client** — a plain read-only page or dashboard section, reachable from the main nav (`app.ts` nav-shell),
listing creditor → outstanding amount (+ next due). Money formatted via the existing Money-branded
formatting the other views use. An **empty state** ("You don't owe any creditors") when there are no rows.

---

## 4. Step-by-step implementation flow (API → Client → Tests)

### API
1. Add a Financing read query (e.g. `GetCreditorPayablesQuery` + handler) that selects `PaymentPlan`s with
   `CreditorId != null`, joins creditor names, groups by creditor, and sums outstanding installments.
   Model it on the existing card-purchases query for structure and testing style.
2. Expose `GET /v1/financing/creditor-payables` in the host `Endpoints/` folder, returning a
   `{ rows: [...] }` envelope (match the existing list-envelope convention used by `GET /v1/instruments`).
3. Keep it strictly read-only — no Ledger calls, no writes.

### Client
4. Add a `CreditorPayablesService` (mirror `instruments-service.ts` / existing Financing services) calling
   the endpoint and mapping the envelope's `rows`.
5. Add a type under `features/financing/types/` (or a small `creditors` view type) for the payable row.
6. Build the list page/section: creditor label + outstanding (+ next due). Wire a nav link so it is
   reachable (see the nav-shell in `app/client/src/app/app.ts` / `app.html`). Include the empty state.

### Tests
7. **API** (`tests/PersonalFinance.Financing.Tests`): given creditor plans across two creditors with mixed
   installments, the query groups correctly and sums outstanding; plans **without** a creditor are
   excluded; card-backed plans never appear.
8. **Client**: renders one row per creditor with the right label + formatted amount; shows the empty state
   when `rows` is `[]`.

---

## 5. Verification

- **API** (from `app/api/`): `dotnet build`, then
  `dotnet test --project tests/PersonalFinance.Financing.Tests`
  (`--filter "FullyQualifiedName~CreditorPayables"` while iterating).
- **Client** (from `app/client/`): `pnpm ng lint`,
  `CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless`,
  `pnpm ng build --configuration production`.
- **Manual sanity:** with the API + client running, record two creditor-financed expenses (Slice 1) to two
  different creditors, open the "Owed to creditors" view, confirm each creditor shows with the correct
  outstanding total and is reachable from the nav.
- **Passing state:** Financing tests green; the list renders live data grouped by creditor; empty state
  works.

---

## 6. Open risks / accepted tradeoffs

- **No "paid" concept yet.** If the plan/installment model has no paid/settled flag, "outstanding" = the
  whole plan. That is acceptable for this slice (payments are out of scope); document it in the endpoint
  summary so the future session knows to refine it.
- Design (visual) should follow `app/client/docs/SYSTEM.md` ("warm homebanking") like every other view —
  extend the system, don't diverge.
- This view is deliberately *not* the financial table (D10). Resist scope creep into gains/losses here.

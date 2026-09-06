# Slice 3 — Schedule-aware summary (Q5)

> **Depends on:** Slices 1 & 2 (future-shares visibility must exist for both card and creditor).
> **Read `00-overview.md` first.**
> **Goal of this slice:** the Parties **list/summary** stops calling a party with pending scheduled
> installments "Settled up." Applies to card and creditor alike.

## The intent, in one paragraph

After Slices 1 & 2, a freshly-loaded split (card or creditor) correctly owes **$0 right now** with
installments scheduled ahead. But the Parties **list** decides "Settled up" purely from the
**posted balance** — it never looks at future scheduled installments — so it labels such a party
"Settled up" even though three payments are coming. That is its own small lie, and the user chose
(Q5) to fix it deliberately: distinguish **truly settled** ($0 owed, nothing scheduled) from
**$0 now, N scheduled**.

## Code landscape

### The "Settled up" logic today
- `app/client/src/app/features/parties/pages/parties-page/parties-page.ts` → `balanceHint`:
  ```typescript
  if (row.netBalanceMinorUnits > 0) return 'They owe you';
  if (row.netBalanceMinorUnits < 0) return 'You owe them';
  return 'Settled up';   // fires on netBalance == 0, ignores any pending schedule
  ```
- Backend summary: `app/api/src/Reporting/PersonalFinance.Reporting/Sql/debt_by_party.sql` sums
  `DeltaMinorUnits` over `vw_current_account_timeline`
  (`app/api/src/Modules/Parties/.../ReadViews/vw_current_account_timeline.sql`, **INNER JOIN** to
  receivable movements — so zero-movement parties are dropped entirely; the prior "parties list"
  work already LEFT-merges the roster on the client so created parties still appear).

### API — expose the pending schedule alongside the posted balance
- Extend the debt/summary read so each party row carries, in addition to `netBalanceMinorUnits`, a
  **pending scheduled total and/or count** — the sum of that party's **unaccrued, non-reversed**
  installment shares (card and creditor), i.e. the same future-shares population Slices 1 & 2 made
  visible. Reuse that query shape rather than re-deriving it.
- Watch the INNER JOIN: a party whose only involvement is a future schedule (no posted movement yet)
  must still appear. Fold the pending-schedule figure into the roster merge so `$0 posted + pending`
  parties are represented.

### Client — a third state
- `parties-page.ts` `balanceHint` / the list row: when `netBalance == 0` **and** pending scheduled
  `> 0`, show something like **"$0 now · N scheduled"** instead of "Settled up". Keep "Settled up"
  only when there is genuinely nothing owed **and** nothing scheduled.
- Keep the copy consistent with the design system (`app/client/docs/SYSTEM.md`, "warm homebanking").

## Testing

- Party with **$0 posted + future installments** → summary reflects the schedule (not "Settled up").
- Party with **genuinely nothing** (no balance, no schedule) → still "Settled up".
- Party who **actively owes** (posted balance ≠ 0) → unchanged "They owe you" / "You owe them".
- Applies equally to a party whose schedule came from a **card** split and one from a **creditor**
  split.

## Definition of done

- The Parties list distinguishes truly-settled from $0-now-with-a-schedule, for both card and
  creditor.
- No regression to the existing "They owe you / You owe them / Settled up" states when there is a
  real posted balance.
- API + client build, all tests green, live smoke: a scheduled-but-$0 party reads as scheduled, not
  settled.
- **Do not commit** — hand the slice to the user to commit.

# LLM Council — Slice 1, Step 5: split-accrual timing (A vs B)

**Date:** 2026-09-06
**Question brought to the council:** "Should I pick Option A?"

---

## Framed question

Decision: pick Option A or Option B for how a split credit-card installment's co-borrower
receivable gets timed, in Slice 1 of a 3-slice refactor of a single-user personal-finance app
(.NET modular monolith, double-entry ledger). The user *is* the developer. The user's north star:
"open the app and see what I need to pay THIS month." User already decided (Q4, previously grilled)
that the fix must **move the money**, not just relabel — the future→owed flip moves to the due month.
Parties settle in-app (`SettleCurrentAccount`). User commits each slice himself.

**Today:** a 1-minute scheduler `AccrueInstallments` accrues each installment when its billing
cycle closes (~day after the card cutoff, mid-cycle-month): opens/finds the `MonthlyStatement`,
posts ONE atomic ledger transaction (split: `Dr CardExpense(holderShare) + Dr Receivable_k(partyShare) / Cr CardLiability(full)`),
grows `AmountDue`, sets `AccruedOnUtc`, bumps `ExpenseSplit.AccruedReceivable` metadata, dispatches
`InstallmentAccruedIntegrationEvent` (no consumer anywhere). For a split, the friend's debt becomes
"owed now" ~2 weeks before the payment is actually due, and the installment vanishes from the
"future shares" view (filtered on `AccruedOnUtc == null`).

**Option A** — for split installments only, gate the whole accrual on `today >= first day of DueCycle`
instead of `IsClosedAsOf`. Atomic transaction unchanged, statement roll-on still keyed to the stored
September cycle, fires ~2 weeks later. ~15 lines, no schema change. Costs: the September statement
is understated/absent for ~2 weeks; if the user pays that statement before Oct 1,
`statement.Accrue` returns `StatementAlreadyPaid`, the code logs + skips, the split installment is
stranded and never posts.

**Option B** — card legs accrue at close (statement correct on time); a second gate at due-month
arrival posts a reclassification transaction (`Dr Receivable_k / Cr CardExpense`) + sets a new
nullable `Installment.SplitAccruedOnUtc` column; the "is it future" predicate switches to
`SplitAccruedOnUtc == null`. Costs: new column + EF migration + data backfill, a new ledger
transaction shape, predicate change across queries, harder to mirror for creditors in Slice 2.

**Recommendation on the table:** Option A.

---

## Advisor responses

### The Contrarian

Option A has a silent-data-loss bug the recommendation waves away. The "stranded installment" path
isn't a sharp edge, it's a hole in the floor. If the user pays the September statement before Oct 1
— to dodge interest, or because cash is on hand, entirely normal behavior — `StatementAlreadyPaid`
fires, the code logs and skips, and that co-borrower receivable *never posts*. No consumer watches
the integration event, so nothing alerts. A real debt a friend owes you just evaporates from the
ledger, and the installment sits in "future shares" forever because `AccruedOnUtc` stays null. On a
double-entry app, permanently. "Against the app's own model" is not a guardrail.

Second: the understatement window is not a dead zone. The north star is "open the app, see what I
owe this month." Between statement-close and Oct 1 the user does exactly that, and reconciles the
app against the real bank statement — which already shows the full installment. The numbers won't
match. The user concludes there's a bug, or "fixes" it.

Third, the framing undersells the distortion. Option A delays the *entire* transaction, including
the holder's own card-expense leg. So Option A makes the September statement *more* wrong than
Option B, which at least books the card leg on time.

Fourth, `AccruedOnUtc == null` now means two things: "not on a statement" and "co-borrower doesn't
owe it yet." Slice 3 will need to split those and can't. That's your rework-twice risk — landing on A.

Don't ship A as written. At minimum, kill the skip-on-`StatementAlreadyPaid` path first.

### The First Principles Thinker

You're both arguing about which day to fire a cron job that probably shouldn't be firing these
transactions at all.

Step back. An installment plan is fully deterministic the instant the purchase is entered: cycles,
due cycles, per-party shares. Nothing is *discovered* at statement-close or at due-month arrival.
Yet Step 5 is a 1-minute poller that imperatively mutates state (`AmountDue`, `AccruedReceivable`,
`AccruedOnUtc`) to make data "appear" on a date, plus an integration event with no consumer. That's
a projection re-implemented as a stateful side effect.

The north star — "what do I owe THIS month" — and "future shares" are read models: queries with a
`today` parameter over the known schedule. "Is it future" is `DueCycle > currentCycle`, a WHERE
clause — not `AccruedOnUtc == null`, and definitely not a new `SplitAccruedOnUtc` column. A
statement's `AmountDue` is `SUM(installments WHERE cycle closed)` — derivable, not accumulated.

Tell: Option A's "sharp edge" — paying September before Oct 1 strands the split installment —
exists only because you're modeling recognition as mutation. A projection has no such edge. That's
your framing smell.

What actually moves money is paying the statement. Post the plan once at entry; make everything
payment-facing a query. Slice 2's "one shared due-month accrual mechanism" then collapses into "one
shared `DueCycle` projection" — less code than A, and nothing to mirror.

If scope genuinely forces you to keep the scheduler this slice: yes, pick A. It's smaller, honest
under real timing, atomic, trivially shared. But pick it knowing it's the option nearest to
*deleting* Step 5 — and write Slice 1's read side as a projection now, so Slices 2 and 3 don't
inherit a scheduler they'll have to keep feeding.

### The Expansionist

**Neither as framed. Both A and B treat accrual timing as a write-time decision. That's the trap.**

The asset hiding in Slice 1 isn't "when do we post the receivable" — it's a schedule-derived
projection keyed by `DueCycle`. You already built the `DueCycle` offset in Steps 1–4. Extend it:
keep posting the split atomically at statement close (holder share + receivable + full liability,
one transaction, statement correct on time — B's benefit without B's column). Then the
future-vs-accrued distinction becomes a *read-time* comparison of `today` against `DueCycle`,
computed from installment schedule rows, not from a flag or a reclassification entry.

Why this unlocks more:

- **Slice 2 parity is free.** Creditors have installment schedules too. Same projection function,
  same `DueCycle` math, zero new ledger shapes. That's the "one shared accrual mechanism" the slice
  literally asks for — and it's shared because it's *derivation logic*, not a scheduler both modules
  poke.
- **Forecasting / what-if / notifications are all the same query.** "What do I owe in November?"
  "What if I split this differently?" "Alert me 3 days before due" — every one of these is *project
  the schedule forward*, not *inspect posted transactions*. Option B's reclassification transaction
  is dead weight for all of them.
- **Multi-party scales.** Receivable_k per co-borrower already projects independently at read time.
  No per-party column growth.

If the council forces A or B: **A**. B's `SplitAccruedOnUtc` column and reclassification shape are
permanent accounting debt that the projection layer makes obsolete in Slice 2 anyway. A stays cheap
and disposable. Ship A, then build the projection where it belongs — shared, in Slice 2.

### The Outsider

**What's unstated:** Option A's "downside" bundles two very different things. A briefly-understated
statement is cosmetic. An installment that gets *stranded and never recorded* if you pay early is
data loss — in an app whose entire job is tracking who owes whom. That's not a footnote, it's
disqualifying.

**Does "it doesn't matter" hold up?** No. It conflates *when the friend's debt activates* with
*whether the statement is correct*. The developer only wants to fix the first. Option A fixes it by
breaking the second. You don't trade a correctness property for a UI-timing preference.

**If I opened the app in that window:** I see my statement says $200. I know I spent $300. I do not
think "the split installment isn't filed yet" — I have no way to know that. I think the app is
broken, or I forgot something, or I underpay my real card because I trusted the number. A finance
app that shows a wrong total has failed at the one thing it exists to do.

**What I'd expect:** "Cycle closes" means the statement is final — everything on it, split or not.
The friend owing me is a *separate* fact that can lag. Option B models exactly that: the charge is
mine until the friend's share transfers. Option A pretends the purchase doesn't exist for two weeks.

**The framing trick:** "15 lines vs. a database change" is carrying the whole argument. But those
15 lines ship a known money-losing bug. That's not simpler, it's cheaper-looking.

**Pick B.** If B is too heavy now, do its core: file the statement entry at cycle close, delay only
the "friend owes" flag. That's B without the ceremony — and it's still not A.

### The Executor

**Option A ships this week. Option B doesn't — not clean, not with a backfill in it.**

Where A bites: you're moving the `useSplit`/`splitParticipants` computation above the close-gate.
Verify that computation doesn't lean on anything the close-gate previously guaranteed — an open
statement, a resolved cutoff. Second bite, the real one: the statement roll-on stays keyed to the
stored September cycle, but now fires ~2 weeks after that cycle closed. Does open/find
`MonthlyStatement` tolerate being called when its target cycle is already closed? If yes, A is a
half-day. If no, that's the actual work — and it's the same work under B, so it's not a reason to
prefer B.

Where B bites: the backfill. You're synthesizing historical `SplitAccruedOnUtc` values for
installments that already accrued under the old one-phase rule. That's judgment-call data surgery
with its own test burden. Plus a new reclassification ledger shape doubles your balance-assertion
scenarios, and the `AccruedOnUtc == null` → `SplitAccruedOnUtc == null` predicate swap has to be
hunted down in every "is it future" read, not just the one they named. That's a slice of its own.

Monday-morning first move: before touching the gate, write the one probe test — accrue a split
installment with `today` set two weeks past cycle close, assert the September statement opens/rolls
without throwing. That single test tells you whether A is 15 lines or a statement-lifecycle fix.

Then, red first: "split posts $0 before due month," "posts on due-month arrival, statement labeled
September." Flip the gate ternary, move the computation up. Run `AccrualBoundaryTests` — must stay
green with zero edits, that's your write-side tripwire. Run handlers via the xUnit host binary
directly; skip the flaky MTP wrapper.

---

## Peer reviews (anonymization map: A=First Principles, B=Executor, C=Expansionist, D=Contrarian, E=Outsider)

### Review 1
1. **Strongest: A.** Explains *why* Option A's stranded-installment edge exists — recognition
   modeled as mutation instead of projection — and still answers the question: if scope forces a
   pick, take A because it's closest to deleting the scheduler, and write the read side as a
   `DueCycle` projection now. D lands the best catch inside the given frame: A delays the holder's
   own card leg too, making September *more* wrong than B.
2. **Biggest blind spot: B.** Evaluates only whether A is *fast*, never whether it's *correct*.
   Waves off the silent data-loss path.
3. **All five missed the minimal fix.** The reported bug is the "future shares" filter using the
   wrong predicate (`AccruedOnUtc == null`). Re-key that read to compare `DueCycle` against today
   and leave the atomic close-time accrual untouched: visible bug gone, statement stays correct, no
   migration, nothing stranded. Also: split-only gating spawns a third accrual path once Slice 2
   adds creditors.

### Review 2
1. **Strongest: D.** Stays on the decision and breaks the framing with specifics others miss (A
   delays the CardExpense leg → September *more* wrong than B; `AccruedOnUtc == null` becomes
   overloaded, blocking Slice 3). B best runner-up for the probe test.
2. **Biggest blind spot: A and C.** Both reframe to a read-time projection but neither engages A's
   silent data-loss path, yet still land on "if forced, ship A."
3. **All five missed:** single-user app → B's backfill is a few rows, doable by hand; "B is too
   heavy" is inflated. Stranding is a code choice, not inherent to A: on `StatementAlreadyPaid`,
   post to the next open statement instead of log-and-skip. The consumer-less integration event is
   the obvious seam for due-month activation — no column, no reclassification.

### Review 3
1. **Strongest: B.** The only response that converts the decision into a falsifiable test. Also
   correctly notes the statement-lifecycle work is identical under A and B.
2. **Biggest blind spot: B.** Reduces the early-payment stranded-installment path to "move the
   computation up," waving past the one genuinely disqualifying flaw.
3. **All five missed:** stranding is an *independent* bug — routing `StatementAlreadyPaid` to the
   next open statement neutralizes it in ~5 lines. None states the truly minimal fix plainly: leave
   accrual untouched, change only the future-shares predicate to `DueCycle > currentCycle`. Nobody
   asks how often early payment actually occurs, or what Slice 3 concretely requires.

### Review 4
1. **Strongest: D.** Most specific and actionable; catches the CardExpense-leg fact and the Slice 3
   trap. B is the best execution plan; A has the deepest reframe but dodges the scoped decision.
2. **Biggest blind spot: B.** Endorses shipping A while never engaging the early-payment strand;
   its probe test only checks whether roll-on throws, not the `StatementAlreadyPaid` log-and-skip.
3. **All five missed:** the symptom is two separable bugs. "Friend owes early" is accrual timing.
   "Vanishes from future-shares" is a pure read-predicate bug — switch the filter to
   `DueCycle > currentCycle`, dies in ~5 lines under either option. Also: the user *is* the
   developer, weakening the "reconciles and thinks it's broken" severity.

### Review 5
1. **Strongest: A.** The only one that names why A's stranded-installment edge exists at all
   (recognition as mutation by a poller). B is the best tactical answer (the probe test on whether
   open/find `MonthlyStatement` tolerates an already-closed cycle is the one concrete blocker
   nobody else caught).
2. **Biggest blind spot: B.** Accepts the framing wholesale; commits effort to feeding a scheduler
   that A, C, and D independently identify as the root problem. Overstates B's backfill as "data
   surgery" when the values are derivable from stored cycles.
3. **All five missed:** the stranded-installment bug can be fixed independently of the timing
   decision (catch-up post to the next open statement). Nobody asked whether the co-borrower
   receivable is ever settled in-app; if it isn't, the whole timing question may be cosmetic.
   *(Chairman note: it is settled in-app via `SettleCurrentAccount` — so the question is not
   cosmetic.)*

---

## Chairman synthesis

### Where the council agrees

- **Both options are framed as write-time decisions; the durable shape is a read-time projection
  keyed on `DueCycle`.** First Principles, Expansionist, and Contrarian reach this independently;
  four of five peer reviews amplify it. "Is it future" should be `DueCycle > currentCycle`, not
  `AccruedOnUtc == null`, and *not* a new stored column.
- **The reported symptom is two separable bugs.** (1) "Friend owes 2 weeks early" — genuine money
  timing, and the user's Q4 already settled that it must move. (2) "Vanishes from future-shares" —
  a pure read-predicate bug: the query filters on `AccruedOnUtc == null` when it should compare
  `DueCycle` to the current cycle. ~5 lines, no ledger change, under either option.
- **Option A as framed regresses the statement feature.** It delays the *entire* transaction,
  including the holder's own `CardExpense` leg, so the September statement is understated or absent
  for ~2 weeks — a feature Slice 1 explicitly promised not to touch.
- **`AccruedOnUtc == null` is already overloaded** ("not on a statement" *and* "co-borrower doesn't
  owe yet"). Landing on plain A bakes that in and blocks Slice 3's "settled vs. $0-now-scheduled"
  distinction.
- **Plain A's split-only gate spawns a third accrual path** once Slice 2 adds creditors — the
  opposite of the "one shared mechanism" the slice asks for.

### Where the council clashes

- **Contrarian & Outsider: pick B (or B's core).** Statement correctness is non-negotiable; A ships
  a known money-losing bug.
- **First Principles & Expansionist: "neither as framed"; if forced, A.** A is cheap and disposable;
  B's column is permanent accounting debt the projection layer makes obsolete in Slice 2.
- **Executor: A.** It's the only option that ships clean this week; B's backfill + new ledger shape
  is its own slice.

Peer review partly dissolves this clash: the single-user context makes B's backfill trivial
(values derive from stored cycles), which undercuts the Executor's main argument; and the user
being the developer softens the Contrarian/Outsider severity (no third party is misled). What
survives on both sides: **statement-correct-at-close is a hard requirement**, and **a new stored
column is unjustified**.

### Blind spots the council caught (peer-review round)

- The **two-bug decomposition** — the visibility symptom is fixable at the read layer alone.
- **Stranding is not intrinsic to A** — on `StatementAlreadyPaid`, catch-up to the next open
  statement (~5 lines) instead of log-and-skip. Fix this regardless of A/B.
- The **consumer-less `InstallmentAccruedIntegrationEvent` is the natural seam** for due-month
  activation — no column, no reclassification.
- The **Executor's probe test** is the one concrete blocker nobody else surfaced: does open/find
  `MonthlyStatement` + `statement.Accrue` tolerate a target cycle that's already closed? (From the
  code: yes — `Accrue` only fails on `IsPaid`. So the *only* failure mode is the stranding path.)

### The recommendation

**Do not pick plain Option A.** Take the middle path that the Expansionist and Outsider both pointed
at, adjusted to honor the user's Q4:

1. **Card legs + statement roll-on stay at cycle-close.** For a split installment, Gate 1 posts
   `Dr CardExpense(full) / Cr CardLiability(full)` and rolls onto the September statement. The
   statement is always complete and correctly labeled — this kills A's disqualifying flaw.
2. **The co-borrower receivable posts at due-month arrival** via a second gate:
   `Dr Receivable_k / Cr CardExpense` + `RecordSplitAccrualAsync`. This honors Q4 — the money moves
   when it actually moves, and the party owes $0 until then.
3. **No `SplitAccruedOnUtc` column.** Make Gate 2 idempotent by computing the target accrued
   receivable for all installments whose `DueCycle <= currentCycle`, subtracting
   `ExpenseSplit.AccruedReceivable`, and posting the delta — the exact pattern the retired
   `AccrueCreditorSplitInstallments` / `CreditorSplitReceivableCalculator` already used. Slice 2
   then reuses this mechanism verbatim for creditors: one shared due-month accrual path, no
   card-vs-creditor branching.
4. **The party "is it future" predicate becomes `DueCycle > currentCycle`**, so an
   accrued-but-not-yet-due split installment still shows in the schedule — and Slice 3 can tell
   "settled" from "$0 now, N scheduled."
5. **Fix the stranding class regardless:** `StatementAlreadyPaid` → catch-up post, not log-and-skip.

This costs the reclassification ledger shape (unavoidable given "statement correct at close" +
"money moves at due month") but not the migration, the backfill, or the stored column. It is
larger than "15 lines" and smaller than Option B as written.

### The one thing to do first

Write the **Executor's probe test** before touching any gate: accrue a split installment with
`today` set two weeks past its cycle close, and assert the September `MonthlyStatement` opens and
rolls without throwing — then assert the `StatementAlreadyPaid` branch does *not* silently drop the
installment. That single test tells you whether the statement lifecycle even permits the two-phase
split, and turns the stranding bug into a red test you fix on the way in.

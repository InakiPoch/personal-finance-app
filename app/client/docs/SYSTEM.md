# Interface System — PersonalFinance client

The design decisions behind the client UI. Established with `reports/DashboardPage`
(the default route). Extend this file as more views are styled; hold to the values
here rather than reinventing them.

Related: `CLAUDE.md` (stack, conventions), `.claude/rules/typescript-frontend-style.md`
(code style), `docs/DESIGN.md §10` (this is the "separate UI task" it defers to).

---

## Direction & feel

**Warm homebanking.** A single-user finance app for the app owner in Argentina, opened
to answer two anxious questions: "what actually left my pocket this month?" and "how
big is the credit-card hit coming?" It should feel like a personal banking dashboard —
key figures in the accent colour, content on cards that lift gently off the canvas.
The canvas is a **warm white**; **titles (h1/h2) are near-pure black** for the loudest
possible contrast, and **every other run of text is a deep navy-blue** — three tiers
(`ink` / `ink-soft` / `ink-faint`), all held at **≥4.5:1 on the canvas** so nothing is a
strain to read. The accent is a slightly brighter blue for the hero figure and the
magnitude ticks — the banking-app blue, never bright. **No dark mode** — the app commits
to this one light palette regardless of the OS preference. The underlying structure stays
ledger-like: ranked columns, tabular figures, a measured feel — just dressed as a
dashboard, not a passbook.

**Domain vocabulary that shapes the UI:** out-of-pocket ("lo que salió del bolsillo",
cash + debit, card excluded) · accrual vs commitment (devengado now vs future cuotas) ·
billing cycle / cutoff ("cierre") · installments ("cuotas") · category totals ·
receivables from parties.

---

## Tokens

Defined in `src/styles.css` as a Tailwind v4 `@theme inline` block over CSS custom
properties, so a token can be retuned in one place without regenerating utilities.
**Always bind to the semantic utility (`bg-paper`, `text-ink`, `border-rule`) — never a
raw Tailwind palette class (`bg-gray-100`) or hex.**

**No dark mode — one committed light palette.** The rest of the client is already
hardcoded light-only, so a system-level dark preference must not change this UI. There is
no `@media (prefers-color-scheme: dark)` block and no toggle; `:root { color-scheme: light }`
keeps native controls (the `<input type="month">` picker) light.

Utility names (`paper` / `ink` / `rule` / `stamp` / `ledger`) are carried over from the
first design pass and are a little misleading now — read them as:
`paper` = **surface**, `heading` = **title black**, `ink` = **body text (navy-blue)**,
`stamp` = **accent (brighter blue)**, `stamp-soft` = **light-blue tint**,
`ledger` = **positive/settled**, `negative` = **error**.

| Token | Value | Role |
| --- | --- | --- |
| `--paper` / `bg-paper` | `#f7f2e7` | page canvas — warm white (also the nav, no separate sidebar colour) |
| `--paper-raised` / `bg-paper-raised` | `#fffdf7` | cards / panels — near-white, lifts on `shadow-card` + border |
| `--paper-inset` / `bg-paper-inset` | `#ece5d6` | inputs, empty progress-bar tracks |
| `--heading` / `text-heading` | `#0f0f0f` | **titles only** (h1, h2 section labels) — reads as black, ~16:1. Base `h1/h2/h3` default to it in `styles.css` |
| `--ink` / `text-ink` | `#234a86` | primary body text, money figures — deep navy-blue, ~7.6:1 on canvas |
| `--ink-soft` / `text-ink-soft` | `#3a5f9a` | secondary text, row labels, quiet links — ~5.6:1 |
| `--ink-faint` / `text-ink-faint` | `#476594` | captions, metadata, legend text, hatch lines — ~5:1 (was ~2.8:1, failed AA) |
| `--rule` / `border-rule` | `rgba(38,34,28,.13)` | hairline separators, input underline, card border |
| `--rule-strong` / `bg-rule-strong` | `rgba(38,34,28,.22)` | emphasis edges (not currently used for accent ticks — see `stamp-soft`) |
| `--stamp` / `text-stamp` `bg-stamp` | `#2f5a8c` | **the accent** (a touch brighter/bluer than `ink`). Hero figure, "Personal ledger" kicker, cycle-bar Accrued segment, the `+` on "Record an expense", focus ring, active nav underline |
| `--stamp-soft` / `bg-stamp-soft` | `#9ec3e5` | light-blue magnitude ticks — hero underline, per-row proportion ticks in the ranked list |
| `--ledger` / `text-ledger` | `#3f7a54` | positive / settled states only ("Nothing due — you're square") |
| `--negative` / `text-negative` | `#a5443a` | error text (`role="alert"` lines) |

Also: `--shadow-card` = `0 1px 2px rgba(43,40,37,.04), 0 2px 8px rgba(43,40,37,.05)`
(`shadow-card`) — the single lift shadow.

**~60/30/10:** warm-white canvas ~60%, navy-blue body text + rule structure ~30%, black
titles + the brighter `stamp` / `stamp-soft` accent ≤10%. The blue-on-blue is why the
tiers are spaced by weight and size, not just hue. `ledger` (green, positive) and
`negative` (brick, error) are semantic, not decoration.

---

## Typography

- **`--font-serif` = IBM Plex Serif** (400/500) — the hero figure and headings. Slab
  terminals read "official financial document". Loaded via `<link>` in `index.html`.
- **`--font-sans` = IBM Plex Sans** (400/500/600, + 400 italic) — all UI text.
- No mono font; `[font-variant-numeric:tabular-nums_lining-nums]` on every money figure
  instead (Plex Sans has good tabular figures).

**Scale (≈1.25 ratio off a 14px body):** caption `text-[0.6875rem]` 11px · meta
`text-xs` 12px · body `text-[0.8125rem]`/`text-sm` 13–14px · h2 label `text-xs`
uppercase tracked · h1 `text-2xl` · **hero figure `text-[2.6rem]`** (~42px) serif/500.

**Colour does most of the hierarchy: black titles vs. blue everything-else.** Section
labels (`<h2>`) are 11–12px, `font-semibold`, `uppercase`, `tracking-[0.14em]`,
`text-heading` (black) — small and tracked so they frame rather than shout, but no
longer a low-contrast whisper. The hero figure still leads on size + serif + the
brighter `stamp` blue. Headings get `tracking-tight` / `tracking-[-0.01em]`; body stays
default tracking with relaxed leading. `text-wrap: balance` on h1–h3, `text-pretty` on
captions (set in `styles.css` base).

---

## Depth & spacing

- **Depth strategy: hairline borders + one whisper shadow on lifted cards.** A card is
  `bg-paper-raised` (lighter than canvas) + `border border-rule` + `shadow-card`.
  Everything else is separated by whitespace and tonal shift, not lines. No shadow
  anywhere except the card lift.
- **Spacing base: 4px.** Reading-document density, not tool density. Between major
  sections `mb-12` (48px); panel padding `p-6` (24px); row rhythm `py-2.5` (10px);
  micro gaps `gap-1`–`gap-1.5` (4–6px); label-to-figure `mt-4`.
- **Radius scale:** `--radius-card` = 8px (`rounded-card`) for panels; `rounded-xs`
  for bars/tracks; `rounded-[1px]` for legend swatches. Concentric: a bar inside a
  `p-6` panel stays small-radius, never matching the card.
- **Column width: `max-w-184`** (= 46rem / 736px), centred, single column. States "a
  page you read top to bottom", not "a control surface". Every view keeps this measure.

---

## Motion

Occasional-surface level only. `.cat-row` fades up 4px over 220ms
`cubic-bezier(0.23,1,0.32,1)`, staggered `i * 40ms` via `[style.animation-delay.ms]`.
Link chevrons nudge `translate-x-0.5` on `group-hover`. Everything guarded by
`@media (prefers-reduced-motion: reduce)`. No animation on anything repeated often.

---

## Focal pattern

**One focal point per view.** On the dashboard it is the month's out-of-pocket total:
serif, `text-[2.6rem]`, **`text-stamp`** (the blue accent carries the key figure, the
way a banking app colours your balance), tabular, alone above a `w-28 h-0.5
rounded-full bg-stamp-soft` light-blue tick. Everything else is demoted — category breakdown is a
quiet ranked column below it; card debt is a lifted `paper-raised` card; quick actions
are `text-sm` chevron links, never buttons competing with the numbers.

When styling a new view: name its one focal element first, make it win with
size + weight + the `stamp` accent, demote the rest.

---

## Component patterns

### Section label
`text-xs font-semibold uppercase tracking-[0.14em] text-heading`, paired in a
`flex flex-wrap items-baseline justify-between gap-x-4 gap-y-2` row with its control
(month picker) or a chevron link on the right.

### Hero figure
`font-serif text-[2.6rem] font-medium leading-none tracking-[-0.01em] text-stamp` +
`[font-variant-numeric:tabular-nums_lining-nums]`, followed by a `mt-3 h-0.5 w-28
rounded-full bg-stamp-soft` light-blue tick, then a `max-w-[52ch] text-[0.8125rem]
text-ink-faint` caption.

### Ranked-totals list
`<ul>` of `<li class="relative flex items-baseline justify-between gap-4 py-2.5">`.
Label `text-sm text-ink-soft`, amount `text-sm text-ink` tabular. Magnitude cue: an
absolutely-positioned `bottom-0 left-0 h-0.5 rounded-full bg-stamp-soft` light-blue
tick, `[style.width.%]` = amount ÷ largest. A measured column, not a bar chart, not a
row highlight. Largest first (API/order-preserving sum).

### Cycle bar (signature)
Per card, one `flex h-2.5 overflow-hidden rounded-xs bg-paper-inset` track split into
`bg-stamp` (Accrued — hits the bill now) and `.cycle-future` (Future — committed, not
yet accrued; a `-45deg` `repeating-linear-gradient` hatch in `--ink-faint`, in
`dashboard-page.css` since a repeating gradient can't be a utility). Segment widths
`[style.width.%]` = segment ÷ card total. `aria-hidden` — the `Accrued $x / Future $y`
text row directly under it carries the data for SR users. A small legend with
`bg-stamp` and `.cycle-future` swatches sits above the list.

### Card / secondary panel
`rounded-card border border-rule bg-paper-raised p-6 shadow-card`. Header row = section
label + chevron link. Used for "card debt by cycle"; reuse for any grouped block.

### Quiet link
`group inline-flex items-center gap-1 text-xs|text-sm text-ink-soft transition-colors
hover:text-ink` with a trailing `&rsaquo;` span that does
`transition-transform group-hover:translate-x-0.5`. "Record an expense" adds a
leading `text-stamp` `+`.

### Primary nav (`app.css`, plain CSS on the shared tokens)
Horizontal bar, `border-bottom: 1px var(--rule)`, same `--paper` as canvas.
Links: `0.75rem`, `letter-spacing: .12em`, `text-transform: uppercase`,
`color: var(--ink-faint)` → `--ink-soft` on hover. Active
(`.primary-nav__link--active`): `color: var(--ink)` + `border-bottom: 2px var(--stamp)`.
`overflow-x: auto` for narrow screens.

### Inputs
Native `<input type="month">`: `border-b border-rule bg-transparent pb-0.5 font-sans
text-xs text-ink-soft outline-none focus-visible:border-stamp`. **Known compromise:**
the native picker glyph can't be themed further; acceptable for a single-user tool.
For `<select>` / richer date entry elsewhere, compose a headless primitive rather than
hand-rolling — do not ship an unstyled native `<select>` as the "design".

---

## States checklist (every data view)

loading (plain `text-ink-faint` line) · error (`text-negative`, `role="alert"`) · empty
(per-section, specific copy; "square/settled" wording uses `text-ledger`) · ready.
Interactive elements: hover, focus-visible (`stamp` ring), disabled.

---

## The checks (run before showing UI work)

- **Swap test** — swap Plex Serif → Inter and the warm tokens → slate/gray: if nothing
  feels lost, you defaulted.
- **Squint test** — blur: the hero figure leads, black titles frame, blue body recedes,
  nothing harsh or bright.
- **Contrast test** — every text colour is ≥4.5:1 on its background (`ink` ~7.6:1,
  `ink-soft` ~5.6:1, `ink-faint` ~5:1, `heading` ~16:1). No text below AA.
- **Signature test** — point to 5: cycle bar (blue Accrued / hatched Future) · blue
  serif hero figure · ranked column with light-blue baseline ticks · "Personal ledger"
  serif-italic kicker in the accent · warm-white canvas with lifted near-white cards.
- **Token test** — every colour resolves to one `--paper/-raised/-inset`, `--heading`,
  `--ink*`, `--rule*`, `--stamp`, `--ledger` or `--negative`. No raw hex, no `gray-500`.

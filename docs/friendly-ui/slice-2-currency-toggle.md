# Slice 2 — Visible ARS | USD toggle on Load an Expense

> Read `00-overview.md` first. Client-only. Client Phase 48. No API change.

## Goal

The user doesn't notice they can switch currency. Replace the faint `<select>` with a
**segmented toggle** (D11) sitting right next to the amount, same visual family as the "Paid with"
picker, so the choice is obvious at a glance:

```
Amount
┌─────────────────────────┐ ┏━━━━━┓┌─────┐
│ $ 125.000               │ ┃ ARS ┃│ USD │
└─────────────────────────┘ ┗━━━━━┛└─────┘
```

The selected option is **filled** (not just underlined — underline is how "Paid with" shows it, but
here it must pop more because it's a small two-option control next to a big number). Follow the
stamp/ink tokens in `app/client/docs/SYSTEM.md`.

Also make the currency sign follow the selection: `$` for ARS, `US$` for USD (the `<span aria-hidden>`
before the input). That's the cheapest possible extra hint and removes ambiguity.

## Current state (verified 2026-09-29)

`C/features/financing/pages/load-expense-page/load-expense-page.html`:

- `:17-31` — "Paid with" `<fieldset>`: `@for(option of modeOptions)` → `<label>` wrapping
  `<input type="radio" formControlName="mode" class="peer sr-only">` + a styled `<span>` using
  `peer-checked:` classes. **Copy this pattern.**
- `:37` — amount row wrapper `flex items-baseline gap-2 border-b …`.
- `:38` — `<span … aria-hidden="true">$</span>`.
- `:50-58` — `<select id="currency" formControlName="currency" aria-label="Currency" class="appearance-none … text-xs … text-ink-faint">` with ARS/USD options.
- Form control: `currency: FormControl<'ARS' | 'USD'>` (default `'ARS'`). Payload already sends
  `currencyCode: raw.currency` — **no TS logic change needed**.
- Spec `load-expense-page.spec.ts:184-195` sets `form.controls.currency.setValue('USD')` and checks the
  payload — still valid after the change.

## Client changes

1. Replace the `<select>` with a `<fieldset>` + `<legend class="sr-only">Currency</legend>` containing two
   radio `<label>`s (`<input type="radio" formControlName="currency" value="ARS" class="peer sr-only">`),
   styled as a bordered pill pair with `peer-checked:bg-… peer-checked:text-…`, `peer-focus-visible:` ring.
   Place it outside the underlined amount wrapper (to the right of it) so the underline stays the amount's.
2. Currency sign: `{{ form.controls.currency.value === 'USD' ? 'US$' : '$' }}` (or a tiny computed/signal
   if the page uses signals for form values — follow the file's idiom).
3. Nothing else. Record Income (`C/features/ledger/pages/record-income-page/`) also has a currency select;
   **out of scope** unless the user asks — mention it at the end of the step.

## Test plan (client specs)

In `load-expense-page.spec.ts`:
- renders two currency radios (ARS, USD) and ARS is checked by default;
- clicking the USD label sets `form.controls.currency.value` to `'USD'` and the card payload carries
  `currencyCode: 'USD'` (DOM-driven version of the existing test);
- the currency sign renders `US$` when USD is selected, `$` otherwise.

## Steps

- [ ] 3. Client prod — toggle + sign. Lint + prod build clean.
- [ ] 4. Client specs — as above. Green.
- [ ] 5. Doc-sync — client `TASK.md` Phase 48 line. `CLAUDE.md` untouched (no new convention) unless it
      documents the currency select explicitly.

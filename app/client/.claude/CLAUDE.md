# PersonalFinance — Angular client

Angular 20 SPA for the PersonalFinance API (`../api/`, .NET 10, acceptance-complete through
Fase 1). The client is a thin presentation layer: **no business logic** — cycles, splits,
rounding, and invariants belong to the API. It mirrors the API DTOs with hand-written types
(no codegen) and talks to `/v1` plus `/health`.

## Where the truth lives — read in this order

1. `docs/PRD.md` — product: 7 views, 3 Fases, cross-cutting rules, 4 API gaps.
2. `docs/DESIGN.md` — technical: folder tree §2, hand-written type model §3, services §4,
   HTTP + error model §5, state/forms §6, eventual consistency §7, local registry §8,
   endpoint traceability §9, config §10, open questions §11.
3. `.claude/TASK.md` — the phased build order (Phase 0 done; Phase 1 = the five Fase-1 views).
   Its "Deviations baked into Phases 0–3" and the Phase 0 "Completion notes" (D1–D8) record
   every intentional departure from PRD/DESIGN — check there before flagging drift.
4. `.claude/rules/typescript-frontend-style.md` — the detailed style guide (formatting,
   naming, typing, component/service shape, forms, cleanup). It is authoritative; the notes
   below are the high-frequency subset, not a replacement.

When two sources appear to conflict, surface it — do not silently pick one. Some conflicts
are already resolved deliberately and recorded as D1–D8 in TASK.md's Phase 0 "Completion
notes" (e.g. DESIGN §2's dotted `*.interceptor.ts` naming overrides the style guide's
hyphen table — that is D7, not drift).

## Stack

- **Angular 20.3**, standalone components, **zoneless** (`provideZonelessChangeDetection`),
  `ChangeDetectionStrategy.OnPush` everywhere.
- **TypeScript 5.9**, `strict` + `strictTemplates`, `noPropertyAccessFromIndexSignature`.
- **pnpm** — run the CLI as `pnpm ng …`.
- **RxJS 7.8**. **`@angular/build`** application builder; budgets 500 kB warn / 1 MB error.
- **Karma + Jasmine**. `zone.js/testing` is **not** loaded — no `fakeAsync` / `tick`; use
  RxJS `TestScheduler` marbles for timer-based tests.
- No Tailwind / ESLint / client CI yet — all Phase 4 (see TASK.md deviation 5).

## Current state

Phase 0 is complete. `src/app/core/` holds:

- `types/` — every hand-written type, one per file, no `I-` prefix: `Money` (branded
  minor-units `number`), `CurrencyCode`, `IsoInstant`, `IsoDate`, `ProblemDetails`,
  `AppError`, `HealthStatus`/`HealthCheckEntry`/`HealthReport`, `RegisteredInstrument`.
- `money/` — `fromMinorUnits` / `toMinorUnits` / `formatArs`. Pure. **No float arithmetic on
  money** — `toMinorUnits` decomposes the decimal string, never `× 100`.
- `http/` — `baseUrlInterceptor` (prefixes relative URLs with `environment.apiUrl`; absolute
  URLs pass through), `problemDetailsInterceptor` (maps failures to `AppError`, trusts
  `HttpErrorResponse.status`), `pollUntil` (bounded reconciliation poll), `SKIP_ERROR_MAPPING`
  (`HttpContextToken` opt-out). Both interceptors are wired in `app.config.ts`.
- `registry/` — `InstrumentRegistryService`: `localStorage`-backed signal, stopgap for the
  missing `GET /v1/instruments` (API gap 1).
- `health/` — `HealthService.check()`: `GET /health` at the host root, degradation-aware.

Feature code lives under `src/app/features/<feature>/` (`<feature>-service.ts`, `types/`,
`pages/<page>/`, `<feature>.routes.ts`). `app.routes.ts` is `Routes = []` with a documented
comment block — each feature lazy-wires its own `loadChildren` entry in its Phase 1 step.

## Conventions — the non-negotiables

**Class layout** — every class artifact (component, service, pipe) follows the member order in
`../../api/.claude/rules/method-organization.md`: I/O declarations → `public` → `protected` →
`private` fields → constructor → `public` → `protected` → `private` methods → lifecycle hooks
→ getters. `protected` = template surface, `private` = internals; explicit modifier on every
member.

**Components**
- Standalone. Never write `standalone: true` (it is the default).
- Template and styles in **separate files** (`templateUrl` / `styleUrl`) — never inline.
- `input()` / `output()` / `model()` functions, never the `@Input()` / `@Output()` decorators.
- No `@HostBinding` / `@HostListener` — use the `host` object.
- Native control flow (`@if` / `@for` / `@switch`), never `*ngIf` / `*ngFor` / `*ngSwitch`.
- `[class.x]` / `[style.x]` bindings, never `ngClass` / `ngStyle`.
- `NgOptimizedImage` for static images (not for inline base64).
- Container vs presentational: containers inject services and own state; presentational
  components get everything via `input()` and emit via `output()`.

**State**
- **Signals-first.** All synchronous state is signals; derived state is `computed()`.
- No `effect()` for data flow. No `.mutate()` — `.set()` / `.update()`.
- Observables stay at their source (HTTP, router, events). The component subscribes and
  `.set()`s the signal. **The async pipe is not used** — templates read signals directly
  (`{{ user() }}`).
- Signals are double-annotated (`WritableSignal<T>` + `signal<T>(…)`), except unambiguous
  primitive literals.

**Services**
- `@Injectable({ providedIn: 'root' })`, `inject(HttpClient)` in a field initializer (no
  constructor injection). Methods return `Observable<T>` with explicit HTTP generics.
- Unwrap `{ rows: [...] }` list envelopes to the array via `map`.
- App-wide state as a signal on the service; expose `asReadonly()` when external mutation
  must be blocked.

**Types & imports**
- One `type` per file under a `types/` folder; `type` for domain shapes, `interface` only
  for extensible contracts. String unions + `as const` over `enum`.
- Types mirror the API DTOs by hand from `/openapi/v1.json` (the running API serves it) —
  no codegen. .NET serializes camelCase; `long` minor units → `Money`, `DateTimeOffset` →
  `IsoInstant`, `DateOnly` → `IsoDate`, GUIDs → `string`.
- Relative imports only — **no path aliases**.
- `Money` is minor units, always. Money is entered in **major** units in forms and converted
  with `toMinorUnits` at submit; `formatArs` is display-only.

**Functional artifacts**
- Guards, resolvers, interceptors are `const` functions (`CanActivateFn`, `ResolveFn<T>`,
  `HttpInterceptorFn`) — never classes. Interceptor files use the dotted `*.interceptor.ts`
  form (D7); other helpers (`poll-until.ts`, `skip-error-mapping.ts`) stay hyphenated.

**Forms**
- Reactive only. Built in a `private initXForm(): void` via `FormBuilder.group()`, called
  from `ngOnInit`. Cross-field validators at the group level. Error copy via an
  `ErrorConfig` map keyed on validator name.

**Errors**
- User-facing messages key off the error **`code`** (`AppError.code`), never the `detail`
  text. The API always sets the HTTP status from the code — trust it.

**Cleanup**
- The one pattern: `private destroy$ = new Subject<void>()` + `takeUntil(this.destroy$)` +
  `ngOnDestroy` (`implements OnInit, OnDestroy` explicitly). Not `takeUntilDestroyed` /
  `DestroyRef`. Fire-and-forget `.subscribe()` only for a single one-shot side effect.

## Testing

- Every service gets an `HttpTestingController` spec asserting URL, verb, body, envelope
  unwrap, and `AppError` mapping. `money` and `problemDetailsInterceptor` get focused unit
  specs.
- `TestBed.configureTestingModule` must include `provideZonelessChangeDetection()` (zoneless
  project — a bare config throws `NG0908`).
- Timer/polling specs use `TestScheduler` marbles, not `fakeAsync`.
- Karma `fileReplacements` are **not** applied — specs compute expected URLs from
  `environment`, never hardcode them.

## Commands

```
pnpm ng build                                # prod build
pnpm ng build --configuration development     # dev build (swaps in environment.development.ts)
CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless
```

If no Chrome/Chromium is installed, point `CHROME_BIN` at any Chromium-based browser —
Brave works (`/usr/bin/brave`). Dev API is `https://localhost:7095/v1` (+ `/health` at the
host root); the browser needs the ASP.NET dev cert trusted: `dotnet dev-certs https --trust`.

# TypeScript Frontend Style (Angular / React)

Generic, transferable guidance for how this developer writes **TypeScript components**,
**templates/JSX**, **services**, and **shared state**. Not project-specific — adapt the
placeholders (`{Feature}`, selector prefix, base URL) to the target project. Angular is
the reference implementation; a **React adaptation** note is given where the concept maps.

Member ordering for every class here also obeys [`method-organization.md`](../../../api/.claude/rules/method-organization.md).

---

## 1. Formatting

| Concern | Rule |
| --- | --- |
| Indentation | **2 spaces**, never tabs. Same width in `.ts`, HTML/JSX, CSS. |
| Quotes | **Single quotes** in TS. Double quotes only where the host language forces it (HTML attributes). |
| Semicolons | **Always.** |
| Line length | **100 characters** soft cap. |
| Trailing commas | **Yes** in every multi-line list, object, import, and param list. |
| Bracket spacing | `{ x }`, not `{x}`. |
| Braces | Opening brace on the **same line** as its statement/declaration. |
| Keyword spacing | No space between keyword and paren: `if(x)`, `for(...)`, `while(...)`, `switch(x)`. |

**Blank lines**
- One blank line after the import block, before the decorator / first declaration.
- One blank line between methods. **No** blank lines between adjacent field declarations.
- Inside a method, use single blank lines to separate logical blocks (setup / subscribe / cleanup).

---

## 2. Import Organization

Group with one blank line between groups, alphabetized within each group:

1. Framework (`@angular/*`, or `react` / `react-dom`)
2. Reactive / async libs (`rxjs`, `rxjs/operators`)
3. Third-party packages
4. Local project imports (relative paths)

Relative paths throughout; no path aliases unless the project already defines them.

---

## 3. Naming Conventions

| Element | File name | Symbol name | Example |
| --- | --- | --- | --- |
| Component | `feature-name.ts` (kebab-case) | `FeatureName` — **PascalCase, no `Component` suffix** | `member-list.ts` → `MemberList` |
| Service | `feature-service.ts` | `FeatureService` | `account-service.ts` → `AccountService` |
| Resolver / loader | `feature-resolver.ts` | `featureResolver` (camelCase) | `member-resolver.ts` → `memberResolver` |
| Guard | `feature-guard.ts` | `featureGuard` (camelCase) | `auth-guard.ts` → `authGuard` |
| Interceptor / middleware | `feature-interceptor.ts` | `featureInterceptor` (camelCase) | `jwt-interceptor.ts` → `jwtInterceptor` |
| Pipe / formatter | `feature-pipe.ts` | `FeaturePipe` | `age-pipe.ts` → `AgePipe` |
| Stateless util (class) | `x-manager.ts` | `XManager` — static methods only, no state | `LocalStorageManager` |
| Util (bare functions) | `x-helpers.ts` | exported `camelCase` functions | `validation-helpers.ts` → `matchPasswordsValueValidator()` |
| Type / model | `model-name.ts` | `ModelName` — **no `I` prefix** | `member.ts` → `Member` |
| Param / filter object | `feature.ts` | `FeatureParams` — `Params` suffix | `MemberParams`, `LikeParams` |
| Error-message map | — | `fieldErrors` — `Errors` suffix | `passwordErrors`, `emailErrors` |

**Other rules**
- Component selectors: kebab-case with a **single project-wide prefix** (e.g. `app-`).
- Methods / properties / signals: `camelCase`.
- Event handlers: verb-first (`toggleLike()`) or `on`-prefixed for I/O boundaries (`onPageChange()`, `onFilterChange()`).
- RxJS streams / Subjects: trailing `$` (`destroy$`, `queryParams$`).
- Private members use the `private` keyword — **no `_` prefix**.
- Class-based artifacts (component, service, pipe) are **PascalCase**; functional artifacts
  (resolver, guard, interceptor) are **camelCase**. The casing signals the kind.

---

## 4. Typing — what is annotated and what is not

**Always explicitly typed:**
- Every class field, including injected dependencies: `private http: HttpClient = inject(HttpClient);`
- Every method return type (`: void`, `: Observable<T>`, `: boolean`).
- Every method parameter.
- Every signal input/output: `member: InputSignal<Member> = input.required<Member>();`

**Signals — double annotation is the default:**
```typescript
protected selectedTheme: WritableSignal<string> = signal<string>('light');
protected hasLiked: Signal<boolean> = computed(() => this.likes().includes(this.id));
```
Inference is acceptable **only** when the initializer is an unambiguous primitive literal:
```typescript
protected currentStep: WritableSignal<number> = signal(1);
```

**Type declarations:**
- `type` aliases for domain models / immutable shapes; `interface` for extensible contracts
  (e.g. a config object consumed by a shared component). No `I` prefix on either.
- **One type per file**, grouped under a `types/` folder, direct named `export`.
- Prefer **string union types + `as const` object maps** over `enum`.
- **`class`** (not `interface`) for **mutable parameter/filter objects** — public fields with
  sensible defaults, reset via `new FeatureParams()`:
  ```typescript
  export class MemberParams {
    gender?: string;
    minAge: number = 18;
    maxAge: number = 130;
    pageNumber: number = 1;
    pageSize: number = 10;
  }
  ```

**React adaptation:** props interfaces replace `InputSignal`; the `Params` class stays as-is
(plain class or a typed object factory).

---

## 5. Component Structure

**Metadata / decorator formatting** (multi-line, fixed key order):
```typescript
@Component({
  selector: 'app-feature-name',
  imports: [/* ... */],
  templateUrl: './feature-name.html',
  styleUrl: './feature-name.css',
})
```
- Template and styles live in **separate files** — never inline `template:` / `styles:`.
- Rely on framework defaults (`standalone`, change detection) unless a deviation is required.

**Member order** (per `method-organization.md`):
1. I/O declarations (`input()`, `output()`, `model()`, `@ViewChild`)
2. `public` fields → `protected` fields → `private` fields
3. Constructor (usually empty; DI happens in field initializers)
4. `public` methods → `protected` methods → `private` methods
5. Lifecycle hooks
6. Getters

**Access modifiers carry meaning:**
- `protected` — anything the template binds to (signals, injected services used in markup, handlers).
- `private` — internal-only state and collaborators.
- Explicit modifier on **every** member; do not rely on the implicit default.

**Dependency injection:** always the `inject()` function in a field initializer. No constructor
parameter injection, even with many dependencies. Field visibility follows usage
(`protected` if the template needs it, otherwise `private`).

**Inputs / outputs — signal-first API only:**
```typescript
label:  InputSignal<string>        = input<string>('Default');
member: InputSignal<Member>        = input.required<Member>();
cancel: OutputEmitterRef<void>     = output<void>();
pageNumber: ModelSignal<number>    = model(1);
```
No `@Input()` / `@Output() EventEmitter`.

**React adaptation:** `input()` → typed props; `output()` → callback props; `model()` →
`value` + `onChange` prop pair; lifecycle → `useEffect`.

---

## 6. State & Reactivity — signals-first, Observable-light

- **Signals** own all synchronous state: local UI state, cached service state, derived values.
- **Derived state** via `computed()`. No `effect()` watchers for data flow.
- **Observables** are confined to their sources: HTTP calls, router streams, event streams.
- The boundary is crossed **in the component**: subscribe to the Observable, `.set()` the signal.
  There is no shared Observable→Signal conversion helper and no `toSignal()` ritual.
- Templates read signals by **direct invocation** (`{{ user() }}`). The **async pipe is not used**.

**React adaptation:** signals → `useState` / `useSyncExternalStore`; `computed()` → derived
value in render or `useMemo`; the HTTP boundary → data-fetching hook that returns state.

---

## 7. Separation of Concerns

| Layer | Owns | Does **not** |
| --- | --- | --- |
| **Component** | Local UI state (signals); reading route-resolved data; subscribing to handle side effects; cleanup | Build query strings; hold app-wide state; contain HTTP URLs |
| **Service** | App-wide state as signals; HTTP methods that **return `Observable<T>`**; state mutation via `tap()` on those calls | Subscribe (except one-shot init/polling); own view concerns |
| **Resolver / loader** | Pre-fetch route data; read `paramMap` / `queryParamMap`; build the `Params` object; return `Observable<T>`; `catchError` → navigate | Transform for display; hold state |
| **Util / helper** | Pure, stateless transformations (static class or bare functions) | Touch the DOM, inject services, keep state |

**Container vs presentational**
- **Container**: injects services, owns state, composes children. Reads resolver data, wires events.
- **Presentational**: no service calls for its own data; receives everything via `input()`,
  communicates out via `output()`. Custom form controls are the one allowed exception
  (they integrate with the forms API directly).

**Service shape:**
```typescript
@Injectable({ providedIn: 'root' })
export class AccountService {
  currentUser: WritableSignal<User | null> = signal<User | null>(null);

  private http: HttpClient = inject(HttpClient);
  private baseUrl: string = environment.apiUrl;

  login(credentials: LoginCredentials): Observable<User> {
    return this.http.post<User>(this.baseUrl + 'users/login', credentials).pipe(
      tap((user: User): void => {
        if(user) this.setCurrentUser(user);
      }),
    );
  }
}
```
- `@Injectable({ providedIn: 'root' })` for every service — singleton, no module providers.
- State is a **public `WritableSignal`** by default. When external mutation must be prevented,
  keep a `private` signal and expose `signal.asReadonly()`, mutating only through named methods
  (`setError()`, `clearError()`).
- HTTP generics are always explicit (`http.get<T>()`).

**Functional, not class-based:** guards, interceptors, and resolvers are `const` functions
(`HttpInterceptorFn`, `ResolveFn<T>`, `CanActivateFn`). Interceptors read service signals
directly and use `catchError` with status-code branching for cross-cutting side effects
(toasts, error routing, cache).

---

## 8. Reactive Forms

- Build with **`FormBuilder.group()`** inside a `private initXForm(): void` method called from
  `ngOnInit()`.
- **Cross-field validation** at the group level — validator passed as the second `group()` arg;
  the validator both sets and clears the error on the child control:
  ```typescript
  this.fb.group({ /* controls */ }, { validators: matchPasswordsValueValidator('password', 'confirm') });
  ```
- Validator functions live in a `validation-helpers.ts` util module, typed `ValidatorFn`.
- **Custom form controls** implement **`ControlValueAccessor`** (not a `FormControl` `@Input`):
  - `@Self() public ngControl: NgControl` via constructor; expose
    `get control(): FormControl { return this.ngControl.control as FormControl; }`.
  - Inputs via `input()` / `input.required()`.
  - Error state is a `signal<Array<{ key: string; message: string }>>([])`, refreshed from
    **both** `control.statusChanges` and `control.valueChanges`.
- **Error messages** use the `ErrorConfig` map pattern — a validator-key → message (or
  `(error, label) => string`) map, merged over a shared `defaultErrorConfig`, passed into the
  control as an `errorConfig` input:
  ```typescript
  protected passwordErrors = {
    required: 'Password is required.',
    minlength: (e: { requiredLength: number }) => `Must be at least ${e.requiredLength} characters.`,
  };
  ```

**React adaptation:** `ControlValueAccessor` → controlled component (`value` + `onChange`);
the `ErrorConfig` map → a shared `getErrorMessage(errors, config)` helper or hook.

---

## 9. Subscription Lifecycle

The one cleanup pattern — used on every component that subscribes:
```typescript
private destroy$ = new Subject<void>();

ngOnInit(): void {
  this.route.data.pipe(takeUntil(this.destroy$)).subscribe({
    next: (data) => this.model.set(data['feature']),
  });
}

ngOnDestroy(): void {
  this.destroy$.next();
  this.destroy$.complete();
}
```
- Always `implements OnInit, OnDestroy` explicitly and pair each hook with its method.
- Fire-and-forget `.subscribe()` is acceptable for a single one-shot effect (navigate after save).
- Not used: `takeUntilDestroyed()` / `DestroyRef`, manual unsubscribe bookkeeping, the async pipe.

---

## 10. Templates & Markup

- **2-space indentation.** Attributes on one line while short; wrap to one-per-line
  (indented 2 spaces, closing `>` on its own line) when long or when multiple bindings pile up.
- **Attribute order:** native attrs (`type`, `name`, `id`) → structural / control flow →
  property bindings `[x]` / `[class.x]` / `[attr.x]` → event bindings `(y)` → static `class` →
  form directives (`formControlName`).
- **Interpolation always spaced:** `{{ value }}`, never `{{value}}`.
- **Bindings:** `[prop]`, `(event)`, `[(model)]`. Toggle classes with `[class.name]="cond"` —
  **not** `ngClass` / `ngStyle`.
- **Control flow** (`@if` / `@else` / `@for` / `@switch`): opening brace on the same line as the
  keyword, body indented 2 spaces. `@for` always has a real `track` (`track item.id`, `$index`
  only as fallback). Use `; as alias` to bind a signal once and read the alias inside the block:
  ```html
  @if(memberService.member(); as member) {
    <h1>{{ member.displayName }}</h1>
  }
  ```
- **Utility-class ordering** (Tailwind / DaisyUI or equivalent): layout → sizing → spacing →
  color → component classes → dynamic `[class.x]`.
- **Accessibility baseline:** `alt` on every image, `for`/`id` on labels, `aria-label` on
  icon-only and radio controls, semantic elements (`<header>`, `<nav>`, `<button type="button">`).
- Prefer pre-resolved data + signals over the async pipe.

**React adaptation:** control-flow blocks → `{cond && ...}` / `.map(... key=)`; `[class.x]` →
`clsx` / template literals; attribute-order and a11y rules carry over unchanged.

---

## 11. Rationale

- **Explicit types everywhere** make each file readable in isolation and let the compiler catch
  drift at the boundary between async sources and synchronous view state.
- **Signals-first, Observable-light** keeps components free of RxJS plumbing; Observables stay
  where they are genuinely needed (I/O), and the conversion point is always visible.
- **Access-modifier discipline** (`protected` = template surface, `private` = internals) makes a
  component's public/template contract obvious at a glance.
- **Functional guards/resolvers/interceptors + `providedIn: 'root'` services** is the smallest
  wiring surface: no modules, no provider arrays, no constructor boilerplate.
- **One cleanup pattern, one form pattern, one error-message pattern** — consistency over
  cleverness, so any file is predictable once you've read one.

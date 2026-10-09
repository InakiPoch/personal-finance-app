import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { Type, provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { config, of } from 'rxjs';
import { ActivatedRoute, Route, convertToParamMap, provideRouter } from '@angular/router';

import { problemDetailsInterceptor } from './core/http/problem-details.interceptor';
import { routes as creditorsRoutes } from './features/creditors/creditors.routes';
import { routes as financingRoutes } from './features/financing/financing.routes';
import { routes as instrumentsRoutes } from './features/instruments/instruments.routes';
import { routes as ledgerRoutes } from './features/ledger/ledger.routes';
import { routes as partiesRoutes } from './features/parties/parties.routes';
import { routes as reportsRoutes } from './features/reports/reports.routes';
import { routes as subscriptionsRoutes } from './features/subscriptions/subscriptions.routes';

/** Glossary guard (docs/GLOSSARY.md, UI vocabulary): banned jargon must never reach rendered page text. */
const BANNED: RegExp = /\b(API|accrued?|accrual|liability|receivable|storno|reversal|cuota|Personal ledger)\b/i;
const BANNED_STATEMENT: RegExp = /\bstatements?\b/i;

const pages: Type<unknown>[] = [
  ...creditorsRoutes,
  ...financingRoutes,
  ...instrumentsRoutes,
  ...ledgerRoutes,
  ...partiesRoutes,
  ...reportsRoutes,
  ...subscriptionsRoutes,
]
  .map((route: Route) => route.component as Type<unknown>)
  .filter((component: Type<unknown> | undefined) => component !== undefined);

type Scenario = { name: string; respond: (http: HttpTestingController) => void };

const flushAll = (http: HttpTestingController, send: (req: TestRequest) => void): void => {
  for(let round: number = 0; round < 5; round++) {
    const open = http.match(() => true);
    if(open.length === 0) {
      return;
    }
    open.forEach(send);
  }
};

const scenarios: Scenario[] = [
  { name: 'loading', respond: () => undefined },
  {
    name: 'server error',
    respond: (http) => flushAll(http, (req) => req.flush(null, { status: 500, statusText: 'Server Error' })),
  },
  {
    name: 'not found',
    respond: (http) => flushAll(http, (req) => req.flush({ status: 404, code: 'Unknown.NotFound' }, { status: 404, statusText: 'Not Found' })),
  },
];

describe('UI copy glossary guard', () => {
  // Some pages leave secondary lookups without an error handler; that's not this guard's concern,
  // and rxjs would otherwise rethrow it asynchronously and abort the run.
  beforeAll(() => { config.onUnhandledError = () => undefined; });
  afterAll(() => { config.onUnhandledError = null; });

  it('covers every routed page', () => {
    expect(pages.length).toBe(16);
  });

  for(const page of pages) {
    for(const scenario of scenarios) {
      it(`${page.name} (${scenario.name}) renders no banned terms`, async () => {
        const params = convertToParamMap({ id: '1', creditorId: '1' });
        TestBed.configureTestingModule({
          providers: [
            provideZonelessChangeDetection(),
            provideRouter([]),
            provideHttpClient(withInterceptors([problemDetailsInterceptor])),
            provideHttpClientTesting(),
            {
              provide: ActivatedRoute,
              useValue: {
                snapshot: { paramMap: params, queryParamMap: params },
                paramMap: of(params),
                queryParamMap: of(params),
                fragment: of(null),
              },
            },
          ],
        });
        const fixture: ComponentFixture<unknown> = TestBed.createComponent(page);
        fixture.detectChanges();
        const http: HttpTestingController = TestBed.inject(HttpTestingController);
        // A page may choke on a synthetic body; the text check below still covers what rendered.
        try { scenario.respond(http); } catch { /* ignore */ }
        await fixture.whenStable();
        fixture.detectChanges();
        const text: string = fixture.nativeElement.textContent ?? '';
        expect(text).not.toMatch(BANNED);
        expect(text).not.toMatch(BANNED_STATEMENT);
      });
    }
  }
});

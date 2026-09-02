import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { problemDetailsInterceptor } from '../http/problem-details.interceptor';
import { SKIP_ERROR_MAPPING } from '../http/skip-error-mapping';
import { HealthReport } from '../types/health-report';
import { HealthService } from './health-service';

describe('HealthService', () => {
  let service: HealthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(withInterceptors([problemDetailsInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(HealthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  const healthy: HealthReport = { status: 'Healthy', totalDurationMs: 12, checks: [] };

  it('returns a 200 Healthy body unchanged and opts out of error mapping', () => {
    let report: HealthReport | undefined;
    service.check().subscribe((r: HealthReport) => (report = r));
    const req = httpMock.expectOne(environment.healthUrl);
    expect(req.request.context.get(SKIP_ERROR_MAPPING)).toBe(true);
    req.flush(healthy);
    expect(report).toEqual(healthy);
  });
  it('returns a 503 Unhealthy body as a HealthReport rather than throwing', () => {
    const unhealthy: HealthReport = {
      status: 'Unhealthy',
      totalDurationMs: 30,
      checks: [{ name: 'outbox', status: 'Unhealthy', description: 'backlog', data: { pending: 5 } }],
    };
    let report: HealthReport | undefined;
    let errored: boolean = false;
    service.check().subscribe({ next: (r: HealthReport) => (report = r), error: () => (errored = true) });
    httpMock
      .expectOne(environment.healthUrl)
      .flush(unhealthy, { status: 503, statusText: 'Service Unavailable' });
    expect(errored).toBe(false);
    expect(report).toEqual(unhealthy);
  });
  it('synthesizes Unhealthy when the API is unreachable', () => {
    let report: HealthReport | undefined;
    service.check().subscribe((r: HealthReport) => (report = r));
    httpMock.expectOne(environment.healthUrl).error(new ProgressEvent('error'), { status: 0 });
    expect(report).toEqual({ status: 'Unhealthy', totalDurationMs: 0, checks: [] });
  });
});

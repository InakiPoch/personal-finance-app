import { HttpClient, HttpContext, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AppError } from '../types/app-error';
import { problemDetailsInterceptor } from './problem-details.interceptor';
import { SKIP_ERROR_MAPPING } from './skip-error-mapping';

describe('problemDetailsInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(withInterceptors([problemDetailsInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('maps a 409 ProblemDetails body to an AppError keyed off code', () => {
    let error: AppError | undefined;
    http.get('/expenses').subscribe({ error: (e: AppError) => (error = e) });
    httpMock.expectOne('/expenses').flush(
      {
        type: 'about:blank',
        title: 'Version conflict',
        status: 409,
        detail: 'The expense was modified by someone else',
        code: 'Expense.VersionConflict',
      },
      { status: 409, statusText: 'Conflict' },
    );
    expect(error).toEqual({
      code: 'Expense.VersionConflict',
      title: 'Version conflict',
      detail: 'The expense was modified by someone else',
      status: 409,
      metadata: {},
    });
  });
  it('routes non-reserved root keys into metadata', () => {
    let error: AppError | undefined;
    http.get('/expenses').subscribe({ error: (e: AppError) => (error = e) });
    httpMock.expectOne('/expenses').flush(
      {
        type: 'https://errors/validation',
        title: 'Validation failed',
        status: 422,
        detail: 'Amount must be positive',
        code: 'Expense.ValidationFailed',
        errors: { amount: ['must be greater than 0'] },
        traceId: '00-abc-01',
      },
      { status: 422, statusText: 'Unprocessable Content' },
    );
    expect(error?.metadata).toEqual({
      errors: { amount: ['must be greater than 0'] },
      traceId: '00-abc-01',
    });
  });
  it('derives a code from the status when the body omits it', () => {
    let error: AppError | undefined;
    http.get('/expenses').subscribe({ error: (e: AppError) => (error = e) });
    httpMock.expectOne('/expenses').flush(
      { title: 'Bad request', status: 400, detail: 'Malformed payload' },
      { status: 400, statusText: 'Bad Request' },
    );
    expect(error?.code).toBe('Http.BadRequest');
    expect(error?.status).toBe(400);
  });
  it('maps a transport failure (status 0) to Http.NetworkError', () => {
    let error: AppError | undefined;
    http.get('/expenses').subscribe({ error: (e: AppError) => (error = e) });
    httpMock.expectOne('/expenses').error(new ProgressEvent('error'), { status: 0 });
    expect(error?.code).toBe('Http.NetworkError');
    expect(error?.status).toBe(0);
    expect(error?.metadata).toEqual({});
  });
  it('maps a bodyless 500 to Http.ServerError', () => {
    let error: AppError | undefined;
    http.get('/expenses').subscribe({ error: (e: AppError) => (error = e) });
    httpMock.expectOne('/expenses').flush(null, { status: 500, statusText: 'Server Error' });
    expect(error?.code).toBe('Http.ServerError');
    expect(error?.status).toBe(500);
  });
  it('passes the raw HttpErrorResponse through when the request opts out', () => {
    let error: unknown;
    http
      .get('/health', { context: new HttpContext().set(SKIP_ERROR_MAPPING, true) })
      .subscribe({ error: (e: unknown) => (error = e) });
    httpMock
      .expectOne('/health')
      .flush({ status: 'Unhealthy' }, { status: 503, statusText: 'Service Unavailable' });
    expect(error).toBeInstanceOf(HttpErrorResponse);
  });
});

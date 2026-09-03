import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { baseUrlInterceptor } from '../../core/http/base-url.interceptor';
import { problemDetailsInterceptor } from '../../core/http/problem-details.interceptor';
import { AppError } from '../../core/types/app-error';
import { environment } from '../../environments/environment';
import { CreateInstrument } from './types/create-instrument';
import { InstrumentCreated } from './types/instrument-created';
import { InstrumentsService } from './instruments-service';

describe('InstrumentsService', () => {
  let service: InstrumentsService;
  let httpMock: HttpTestingController;

  const url: string = `${environment.apiUrl}/instruments`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(withInterceptors([baseUrlInterceptor, problemDetailsInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(InstrumentsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('POSTs the instrument body and returns the created instrument', () => {
    const body: CreateInstrument = { type: 'credit', name: 'Visa', cutoffDate: 20 };
    let result: InstrumentCreated | undefined;
    service.create(body).subscribe((created: InstrumentCreated) => (result = created));
    const req = httpMock.expectOne(url);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush({ id: 'inst-1', type: 'credit' });
    expect(result).toEqual({ id: 'inst-1', type: 'credit' });
  });
  it('maps a 400 to an AppError keyed off code', () => {
    let error: AppError | undefined;
    service
      .create({ type: 'debit', name: 'Checking' })
      .subscribe({ next: () => {}, error: (e: AppError) => (error = e) });
    httpMock.expectOne(url).flush(
      { title: 'Bad request', status: 400, detail: 'unknown type', code: 'Instruments.UnknownType' },
      { status: 400, statusText: 'Bad Request' },
    );
    expect(error).toEqual({
      code: 'Instruments.UnknownType',
      title: 'Bad request',
      detail: 'unknown type',
      status: 400,
      metadata: {},
    });
  });
});

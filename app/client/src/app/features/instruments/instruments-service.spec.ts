import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { baseUrlInterceptor } from '../../core/http/base-url.interceptor';
import { problemDetailsInterceptor } from '../../core/http/problem-details.interceptor';
import { AppError } from '../../core/types/app-error';
import { environment } from '../../environments/environment';
import { ClosingScheduleRow } from './types/closing-schedule-row';
import { CreateInstrument } from './types/create-instrument';
import { Instrument } from './types/instrument';
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

  it('GETs the instrument list and unwraps the { rows } envelope', () => {
    const rows: Instrument[] = [
      { id: 'acc-1', type: 'debit', name: 'Checking', cutoffDate: null, nextClosingDate: null },
      { id: 'card-1', type: 'credit', name: 'Visa', cutoffDate: 15, nextClosingDate: null }
    ];
    let result: Instrument[] | undefined;
    service.list().subscribe((instruments: Instrument[]) => (result = instruments));
    const req = httpMock.expectOne(url);
    expect(req.request.method).toBe('GET');
    req.flush({ rows });
    expect(result).toEqual(rows);
  });
  it('maps a failed list to an AppError keyed off code', () => {
    let error: AppError | undefined;
    service.list().subscribe({ next: () => {}, error: (e: AppError) => (error = e) });
    httpMock.expectOne(url).flush(
      { title: 'Server error', status: 500, detail: 'boom', code: 'Http.ServerError' },
      { status: 500, statusText: 'Internal Server Error' },
    );
    expect(error?.code).toBe('Http.ServerError');
  });

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
      { status: 400, statusText: 'Bad Request' }
    );
    expect(error).toEqual({
      code: 'Instruments.UnknownType',
      title: 'Bad request',
      detail: 'unknown type',
      status: 400,
      metadata: {}
    });
  });

  describe('closing dates', () => {
    const cardUrl: string = `${environment.apiUrl}/instruments/cards/card-1`;

    it('PUTs the usual closing day', () => {
      let done = false;
      service.changeUsualClosingDay('card-1', 24).subscribe(() => (done = true));
      const req = httpMock.expectOne(`${cardUrl}/closing-day`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ day: 24 });
      req.flush(null, { status: 204, statusText: 'No Content' });
      expect(done).toBe(true);
    });
    it('GETs the closing schedule and unwraps the { rows } envelope', () => {
      const rows: ClosingScheduleRow[] = [
        { year: 2026, month: 10, closingDate: '2026-10-24', isOverride: true, isLocked: false },
        { year: 2026, month: 11, closingDate: '2026-11-20', isOverride: false, isLocked: false },
      ];
      let result: ClosingScheduleRow[] | undefined;
      service.closingSchedule('card-1').subscribe((r: ClosingScheduleRow[]) => (result = r));
      const req = httpMock.expectOne(`${cardUrl}/closing-dates`);
      expect(req.request.method).toBe('GET');
      req.flush({ rows });
      expect(result).toEqual(rows);
    });
    it('PUTs a per-month closing date', () => {
      service.setClosingDate('card-1', 2026, 10, 24).subscribe();
      const req = httpMock.expectOne(`${cardUrl}/closing-dates/2026/10`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ day: 24 });
      req.flush(null, { status: 204, statusText: 'No Content' });
    });
    it('DELETEs a per-month closing date', () => {
      service.clearClosingDate('card-1', 2026, 10).subscribe();
      const req = httpMock.expectOne(`${cardUrl}/closing-dates/2026/10`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null, { status: 204, statusText: 'No Content' });
    });
    it('maps a 409 on a mutation to an AppError keyed off code', () => {
      let error: AppError | undefined;
      service
        .changeUsualClosingDay('card-1', 24)
        .subscribe({ next: () => {}, error: (e: AppError) => (error = e) });
      httpMock.expectOne(`${cardUrl}/closing-day`).flush(
        {
          title: 'Conflict',
          status: 409,
          detail: 'charged',
          code: 'Financing.ClosingChangeMovesChargedPurchase',
        },
        { status: 409, statusText: 'Conflict' },
      );
      expect(error?.code).toBe('Financing.ClosingChangeMovesChargedPurchase');
      expect(error?.status).toBe(409);
    });
  });
});

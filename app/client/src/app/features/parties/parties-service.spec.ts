import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { baseUrlInterceptor } from '../../core/http/base-url.interceptor';
import { problemDetailsInterceptor } from '../../core/http/problem-details.interceptor';
import { AppError } from '../../core/types/app-error';
import { Money } from '../../core/types/money';
import { environment } from '../../environments/environment';
import { CurrentAccountBalance } from './types/current-account-balance';
import { PartiesService } from './parties-service';

describe('PartiesService', () => {
  let service: PartiesService;
  let httpMock: HttpTestingController;

  const money = (value: number): Money => value as Money;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(withInterceptors([baseUrlInterceptor, problemDetailsInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(PartiesService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('GETs the current-account balance for a party', () => {
    const balance: CurrentAccountBalance = {
      partyId: 'p1',
      name: 'Alice',
      balanceMinorUnits: money(250000),
    };
    let result: CurrentAccountBalance | undefined;
    service.getBalance('p1').subscribe((r: CurrentAccountBalance) => (result = r));
    const req = httpMock.expectOne(`${environment.apiUrl}/parties/p1/balance`);
    expect(req.request.method).toBe('GET');
    req.flush(balance);
    expect(result).toEqual(balance);
  });
  it('maps a 404 to an AppError keyed off code', () => {
    let error: AppError | undefined;
    service.getBalance('missing').subscribe({ next: () => {}, error: (e: AppError) => (error = e) });
    httpMock.expectOne(`${environment.apiUrl}/parties/missing/balance`).flush(
      { title: 'Not found', status: 404, detail: 'no such party', code: 'Parties.NotFound' },
      { status: 404, statusText: 'Not Found' },
    );
    expect(error?.code).toBe('Parties.NotFound');
    expect(error?.status).toBe(404);
  });
});

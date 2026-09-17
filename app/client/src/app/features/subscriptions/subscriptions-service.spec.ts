import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { baseUrlInterceptor } from '../../core/http/base-url.interceptor';
import { problemDetailsInterceptor } from '../../core/http/problem-details.interceptor';
import { AppError } from '../../core/types/app-error';
import { Money } from '../../core/types/money';
import { environment } from '../../environments/environment';
import { ActiveSubscription } from './types/active-subscription';
import { CreateSubscription } from './types/create-subscription';
import { PaySubscriptionResult } from './types/pay-subscription-result';
import { SubscriptionResult } from './types/subscription-result';
import { SubscriptionsService } from './subscriptions-service';

describe('SubscriptionsService', () => {
  let service: SubscriptionsService;
  let httpMock: HttpTestingController;

  const activeUrl: string = `${environment.apiUrl}/subscriptions/active`;
  const collectionUrl: string = `${environment.apiUrl}/subscriptions`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(withInterceptors([baseUrlInterceptor, problemDetailsInterceptor])),
        provideHttpClientTesting(),
      ]
    });
    service = TestBed.inject(SubscriptionsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('GETs the active list, unwraps { rows } and normalises frequency to lowercase', () => {
    let result: ActiveSubscription[] | undefined;
    service.listActive().subscribe((rows: ActiveSubscription[]) => (result = rows));
    const req = httpMock.expectOne(activeUrl);
    expect(req.request.method).toBe('GET');
    req.flush({
      rows: [
        {
          subscriptionId: 'sub-1',
          name: 'Netflix',
          amountMinorUnits: 500000,
          category: 'Entertainment',
          frequency: 'Monthly',
          anchorDay: 15,
          nextDueDate: '2026-10-15',
          status: 'paid'
        }
      ]
    });
    expect(result).toEqual([
      {
        subscriptionId: 'sub-1',
        name: 'Netflix',
        amountMinorUnits: 500000 as Money,
        category: 'Entertainment',
        frequency: 'monthly',
        anchorDay: 15,
        nextDueDate: '2026-10-15',
        status: 'paid'
      }
    ]);
  });

  it('POSTs the create body and returns the new id', () => {
    const body: CreateSubscription = {
      name: 'Spotify',
      amountMinorUnits: 300000 as Money,
      category: 'Music',
      fundingAccountId: 'acc-1',
      frequency: 'monthly',
      anchorDay: 1
    };
    let result: SubscriptionResult | undefined;
    service.create(body).subscribe((created: SubscriptionResult) => (result = created));
    const req = httpMock.expectOne(collectionUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush({ id: 'sub-9' });
    expect(result).toEqual({ id: 'sub-9' });
  });

  it('DELETEs the subscription by id and completes with no value', () => {
    let completed: boolean = false;
    let result: void | undefined;
    service.cancel('sub-1').subscribe({
      next: (value: void) => (result = value),
      complete: () => (completed = true),
    });
    const req = httpMock.expectOne(`${collectionUrl}/sub-1`);
    expect(req.request.method).toBe('DELETE');
    expect(req.request.body).toBeNull();
    req.flush(null, { status: 204, statusText: 'No Content' });
    expect(result).toBeNull();
    expect(completed).toBeTrue();
  });

  it('POSTs the pay request and returns the result', () => {
    let result: PaySubscriptionResult | undefined;
    service.pay('sub-1').subscribe((paid: PaySubscriptionResult) => (result = paid));
    const req = httpMock.expectOne(`${collectionUrl}/sub-1/pay`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush({ subscriptionId: 'sub-1' });
    expect(result).toEqual({ subscriptionId: 'sub-1' });
  });

  it('maps a 409 on pay to an AppError keyed off code', () => {
    let error: AppError | undefined;
    service.pay('sub-1').subscribe({ next: () => {}, error: (e: AppError) => (error = e) });
    httpMock.expectOne(`${collectionUrl}/sub-1/pay`).flush(
      {
        title: 'Conflict',
        status: 409,
        detail: 'already paid',
        code: 'Subscriptions.SubscriptionAlreadyPaid',
      },
      { status: 409, statusText: 'Conflict' }
    );
    expect(error).toEqual({
      code: 'Subscriptions.SubscriptionAlreadyPaid',
      title: 'Conflict',
      detail: 'already paid',
      status: 409,
      metadata: {}
    });
  });

  it('POSTs the unpay request and returns the result', () => {
    let result: PaySubscriptionResult | undefined;
    service.unpay('sub-1').subscribe((unpaid: PaySubscriptionResult) => (result = unpaid));
    const req = httpMock.expectOne(`${collectionUrl}/sub-1/unpay`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush({ subscriptionId: 'sub-1' });
    expect(result).toEqual({ subscriptionId: 'sub-1' });
  });

  it('maps a 409 on unpay to an AppError keyed off code', () => {
    let error: AppError | undefined;
    service.unpay('sub-1').subscribe({ next: () => {}, error: (e: AppError) => (error = e) });
    httpMock.expectOne(`${collectionUrl}/sub-1/unpay`).flush(
      {
        title: 'Conflict',
        status: 409,
        detail: 'not paid',
        code: 'Subscriptions.SubscriptionNotPaid',
      },
      { status: 409, statusText: 'Conflict' }
    );
    expect(error).toEqual({
      code: 'Subscriptions.SubscriptionNotPaid',
      title: 'Conflict',
      detail: 'not paid',
      status: 409,
      metadata: {}
    });
  });

  it('maps a 422 on create to an AppError keyed off code', () => {
    let error: AppError | undefined;
    service
      .create({
        name: '',
        amountMinorUnits: 0 as Money,
        category: 'Music',
        fundingAccountId: 'acc-1',
        frequency: 'monthly',
        anchorDay: 1
      })
      .subscribe({ next: () => {}, error: (e: AppError) => (error = e) });
    httpMock.expectOne(collectionUrl).flush(
      {
        title: 'Unprocessable entity',
        status: 422,
        detail: 'amount must be positive',
        code: 'Subscriptions.NonPositiveAmount',
      },
      { status: 422, statusText: 'Unprocessable Entity' }
    );
    expect(error).toEqual({
      code: 'Subscriptions.NonPositiveAmount',
      title: 'Unprocessable entity',
      detail: 'amount must be positive',
      status: 422,
      metadata: {}
    });
  });

  it('maps a 404 on cancel to an AppError keyed off code', () => {
    let error: AppError | undefined;
    service.cancel('missing').subscribe({ next: () => {}, error: (e: AppError) => (error = e) });
    httpMock.expectOne(`${collectionUrl}/missing`).flush(
      {
        title: 'Not found',
        status: 404,
        detail: 'subscription not found',
        code: 'Subscriptions.SubscriptionNotFound',
      },
      { status: 404, statusText: 'Not Found' }
    );
    expect(error).toEqual({
      code: 'Subscriptions.SubscriptionNotFound',
      title: 'Not found',
      detail: 'subscription not found',
      status: 404,
      metadata: {}
    });
  });
});

import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { AppError } from '../../../../core/types/app-error';
import { Money } from '../../../../core/types/money';
import { FinancingService } from '../../financing-service';
import { CreditorDetail } from '../../types/creditor-detail';
import { CreditorDetailPage } from './creditor-detail-page';

type CreditorDetailView = {
  detail: () => CreditorDetail | null;
  loadStatus: () => 'idle' | 'loading' | 'ready' | 'error';
  isNotFound: () => boolean;
};

describe('CreditorDetailPage', () => {
  let fixture: ComponentFixture<CreditorDetailPage>;
  let view: CreditorDetailView;
  let creditorDetail: jasmine.Spy<(creditorId: string) => Observable<CreditorDetail>>;

  const money = (value: number): Money => value as Money;

  const detail: CreditorDetail = {
    creditorId: 'cred-1',
    creditorName: 'Juan',
    purchases: [{
      planId: 'pl-1',
      description: 'Sofa',
      purchaseDate: '2026-01-10',
      totalMinorUnits: money(300000),
      outstandingMinorUnits: money(200000),
      installments: [{
        installmentId: 'i-1',
        sequence: 1,
        installmentCount: 3,
        amountMinorUnits: money(100000),
        dueYear: 2026,
        dueMonth: 2,
        isPaid: true,
        isReversed: false,
        status: 'paid'
      }, {
        installmentId: 'i-2',
        sequence: 2,
        installmentCount: 3,
        amountMinorUnits: money(100000),
        dueYear: 2026,
        dueMonth: 3,
        isPaid: false,
        isReversed: false,
        status: 'overdue'
      }]
    }]
  };

  function setup(): void {
    TestBed.configureTestingModule({
      imports: [CreditorDetailPage],
      providers: [
        provideZonelessChangeDetection(),
        { provide: FinancingService, useValue: { creditorDetail } },
        {
          provide: ActivatedRoute,
          useValue: { paramMap: of(convertToParamMap({ creditorId: 'cred-1' })) }
        }
      ]
    });
    fixture = TestBed.createComponent(CreditorDetailPage);
    view = fixture.componentInstance as unknown as CreditorDetailView;
    fixture.detectChanges();
  }

  it('loads the creditor named by the route param and renders its purchases', () => {
    creditorDetail = jasmine.createSpy('creditorDetail').and.returnValue(of(detail));
    setup();
    expect(creditorDetail).toHaveBeenCalledWith('cred-1');
    expect(view.loadStatus()).toBe('ready');
    expect(view.detail()?.creditorId).toBe('cred-1');
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('Juan');
    expect(text).toContain('Sofa');
    expect(text).toContain('1/3');
    expect(text).toContain('Feb 2026');
  });
  it('renders a status label per installment', () => {
    creditorDetail = jasmine.createSpy('creditorDetail').and.returnValue(of(detail));
    setup();
    const body: string = fixture.nativeElement.querySelector('tbody').textContent;
    expect(body).toContain('Paid');
    expect(body).toContain('Overdue');
  });
  it('shows the friendly not-found state on a CreditorNotFound 404', () => {
    const appError: AppError = {
      code: 'Financing.CreditorNotFound',
      title: 'Not found',
      detail: 'x',
      status: 404,
      metadata: {}
    };
    creditorDetail = jasmine.createSpy('creditorDetail').and.returnValue(throwError(() => appError));
    setup();
    expect(view.loadStatus()).toBe('error');
    expect(view.isNotFound()).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('No creditor matches that link');
  });
  it('shows the generic error state on any other failure', () => {
    const appError: AppError = {
      code: 'Http.ServerError',
      title: 'Server error',
      detail: 'x',
      status: 500,
      metadata: {}
    };
    creditorDetail = jasmine.createSpy('creditorDetail').and.returnValue(throwError(() => appError));
    setup();
    expect(view.loadStatus()).toBe('error');
    expect(view.isNotFound()).toBe(false);
    expect(fixture.nativeElement.textContent).toContain('Could not load this creditor');
  });
});

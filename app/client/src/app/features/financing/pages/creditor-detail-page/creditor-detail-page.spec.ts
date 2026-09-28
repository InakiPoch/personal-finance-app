import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { AppError } from '../../../../core/types/app-error';
import { Money } from '../../../../core/types/money';
import { FinancingService } from '../../financing-service';
import { CreditorDetail } from '../../types/creditor-detail';
import { PayCreditorFullDebtResult } from '../../types/pay-creditor-full-debt-result';
import { PayCreditorInstallment } from '../../types/pay-creditor-installment';
import { PayCreditorInstallmentResult } from '../../types/pay-creditor-installment-result';
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
  let payCreditorInstallment: jasmine.Spy<(id: string, body: PayCreditorInstallment) => Observable<PayCreditorInstallmentResult>>;
  let unpayCreditorInstallment: jasmine.Spy<(id: string) => Observable<PayCreditorInstallmentResult>>;
  let payCreditorFullDebt: jasmine.Spy<(creditorId: string) => Observable<PayCreditorFullDebtResult>>;

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
      currencyCode: 'ARS',
      installments: [{
        installmentId: 'i-1',
        sequence: 1,
        installmentCount: 3,
        amountMinorUnits: money(100000),
        dueYear: 2026,
        dueMonth: 2,
        isPaid: true,
        isReversed: false,
        status: 'paid',
        paidMinorUnits: money(100000),
        remainingMinorUnits: money(0),
        hasPayments: true
      }, {
        installmentId: 'i-2',
        sequence: 2,
        installmentCount: 3,
        amountMinorUnits: money(100000),
        dueYear: 2026,
        dueMonth: 3,
        isPaid: false,
        isReversed: false,
        status: 'overdue',
        paidMinorUnits: money(0),
        remainingMinorUnits: money(100000),
        hasPayments: false
      }]
    }]
  };

  function setup(): void {
    TestBed.configureTestingModule({
      imports: [CreditorDetailPage],
      providers: [
        provideZonelessChangeDetection(),
        {
          provide: FinancingService,
          useValue: { creditorDetail, payCreditorInstallment, unpayCreditorInstallment, payCreditorFullDebt }
        },
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

  function buttonByLabel(label: string): HTMLButtonElement {
    const all: NodeListOf<HTMLButtonElement> = fixture.nativeElement.querySelectorAll('button');
    return Array.from(all).find((button: HTMLButtonElement) => button.textContent?.trim() === label) as HTMLButtonElement;
  }

  beforeEach(() => {
    if(!HTMLDialogElement.prototype.showModal) {
      spyOn(HTMLDialogElement.prototype, 'showModal').and.callFake(function(this: HTMLDialogElement): void {
        this.setAttribute('open', '');
      });
      spyOn(HTMLDialogElement.prototype, 'close').and.callFake(function(this: HTMLDialogElement): void {
        this.removeAttribute('open');
      });
    }
    payCreditorInstallment = jasmine.createSpy('payCreditorInstallment')
      .and.returnValue(of({ installmentId: 'i-2' }));
    unpayCreditorInstallment = jasmine.createSpy('unpayCreditorInstallment')
      .and.returnValue(of({ installmentId: 'i-1' }));
    payCreditorFullDebt = jasmine.createSpy('payCreditorFullDebt')
      .and.returnValue(of({ settledCount: 3 }));
  });

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
  it('opens the pay dialog on Pay, titled with the cuota and purchase', () => {
    creditorDetail = jasmine.createSpy('creditorDetail').and.returnValue(of(detail));
    setup();
    buttonByLabel('Pay').click();
    fixture.detectChanges();
    expect(payCreditorInstallment).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('dialog').textContent).toContain('Cuota 2/3 · Sofa');
  });
  it('confirms the pay dialog in full and re-fetches the detail', () => {
    creditorDetail = jasmine.createSpy('creditorDetail').and.returnValue(of(detail));
    setup();
    buttonByLabel('Pay').click();
    fixture.detectChanges();
    buttonByLabel('Confirm').click();
    expect(payCreditorInstallment).toHaveBeenCalledWith('i-2', { amountMinorUnits: null });
    expect(creditorDetail).toHaveBeenCalledTimes(2);
  });
  it('backs out of the pay dialog via Cancel without calling the service', () => {
    creditorDetail = jasmine.createSpy('creditorDetail').and.returnValue(of(detail));
    setup();
    buttonByLabel('Pay').click();
    fixture.detectChanges();
    buttonByLabel('Cancel').click();
    fixture.detectChanges();
    expect(payCreditorInstallment).not.toHaveBeenCalled();
    expect(creditorDetail).toHaveBeenCalledTimes(1);
  });
  it('undoes a payment via the row Undo button and re-fetches the detail', () => {
    creditorDetail = jasmine.createSpy('creditorDetail').and.returnValue(of(detail));
    setup();
    buttonByLabel('Undo').click();
    expect(unpayCreditorInstallment).toHaveBeenCalledWith('i-1');
    expect(creditorDetail).toHaveBeenCalledTimes(2);
  });
  it('arms an inline confirm, settles the full debt on confirm, and re-fetches the detail', () => {
    creditorDetail = jasmine.createSpy('creditorDetail').and.returnValue(of(detail));
    setup();
    buttonByLabel('Pay full debt').click();
    fixture.detectChanges();
    buttonByLabel('Confirm').click();
    expect(payCreditorFullDebt).toHaveBeenCalledWith('cred-1');
    expect(creditorDetail).toHaveBeenCalledTimes(2);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('cuota(s) settled');
  });
  it('disables Pay full debt when nothing is outstanding', () => {
    const settled: CreditorDetail = {
      creditorId: 'cred-1',
      creditorName: 'Juan',
      purchases: [{
        planId: 'pl-1',
        description: 'Sofa',
        purchaseDate: '2026-01-10',
        totalMinorUnits: money(300000),
        outstandingMinorUnits: money(0),
        currencyCode: 'ARS',
        installments: []
      }]
    };
    creditorDetail = jasmine.createSpy('creditorDetail').and.returnValue(of(settled));
    setup();
    expect(buttonByLabel('Pay full debt').disabled).toBe(true);
  });
  it('backs out of the confirm without calling the service', () => {
    creditorDetail = jasmine.createSpy('creditorDetail').and.returnValue(of(detail));
    setup();
    buttonByLabel('Pay full debt').click();
    fixture.detectChanges();
    buttonByLabel('Cancel').click();
    fixture.detectChanges();
    expect(payCreditorFullDebt).not.toHaveBeenCalled();
    expect(buttonByLabel('Pay full debt')).toBeTruthy();
  });
  it('shows a friendly message keyed off code when a pay fails', () => {
    creditorDetail = jasmine.createSpy('creditorDetail').and.returnValue(of(detail));
    const appError: AppError = {
      code: 'Financing.InstallmentAlreadyPaid',
      title: 'Conflict',
      detail: 'x',
      status: 409,
      metadata: {}
    };
    payCreditorInstallment = jasmine.createSpy('payCreditorInstallment')
      .and.returnValue(throwError(() => appError));
    setup();
    buttonByLabel('Pay').click();
    fixture.detectChanges();
    buttonByLabel('Confirm').click();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('already marked paid');
    expect(creditorDetail).toHaveBeenCalledTimes(1);
  });
});

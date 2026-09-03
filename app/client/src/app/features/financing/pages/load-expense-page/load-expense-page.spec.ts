import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormArray, FormControl, FormGroup } from '@angular/forms';
import { Observable, of, throwError } from 'rxjs';
import { TestScheduler } from 'rxjs/testing';
import { AppError } from '../../../../core/types/app-error';
import { Money } from '../../../../core/types/money';
import { InstrumentsService } from '../../../instruments/instruments-service';
import { Instrument } from '../../../instruments/types/instrument';
import { CurrentAccountBalance } from '../../../parties/types/current-account-balance';
import { PartiesService } from '../../../parties/parties-service';
import { PartyDebtRow } from '../../../reports/types/party-debt-row';
import { ReportsService } from '../../../reports/reports-service';
import { CreatePaymentPlan } from '../../types/create-payment-plan';
import { CreatePaymentPlanResult } from '../../types/create-payment-plan-result';
import { FinancingService } from '../../financing-service';
import { LoadExpensePage } from './load-expense-page';

type SplitRow = FormGroup<{ partyId: FormControl<string>; weight: FormControl<number | null> }>;

type LoadExpenseView = {
  form: FormGroup<{
    amount: FormControl<number | null>;
    cardId: FormControl<string>;
    installmentCount: FormControl<number | null>;
    purchaseDate: FormControl<string>;
    split: FormArray<SplitRow>;
  }>;
  parties: () => PartyDebtRow[];
  partiesStatus: () => 'loading' | 'ready' | 'error';
  submitStatus: () => 'idle' | 'submitting' | 'confirmed' | 'error';
  submitError: () => AppError | null;
  confirmedPlanId: () => string | null;
  reconciliations: () => Array<{
    partyId: string;
    partyName: string;
    status: 'reconciling' | 'reconciled' | 'stalled';
    balanceMinorUnits: number | null;
  }>;
  addSplitRow: () => void;
  onSubmit: () => void;
};

describe('LoadExpensePage', () => {
  let fixture: ComponentFixture<LoadExpensePage>;
  let view: LoadExpenseView;
  let createPaymentPlan: jasmine.Spy<(body: CreatePaymentPlan) => Observable<CreatePaymentPlanResult>>;
  let getBalance: jasmine.Spy<(partyId: string) => Observable<CurrentAccountBalance>>;
  let debtSummary: jasmine.Spy<() => Observable<PartyDebtRow[]>>;

  const money = (value: number): Money => value as Money;
  const partyRows: PartyDebtRow[] = [
    { partyId: 'p1', partyName: 'Alice', netBalanceMinorUnits: money(0), currencyCode: 'ARS' },
  ];

  function balance(value: number): CurrentAccountBalance {
    return { partyId: 'p1', name: 'Alice', balanceMinorUnits: money(value) };
  }

  function fillValidForm(): void {
    view.form.patchValue({
      amount: 1234.5,
      cardId: 'card-credit',
      installmentCount: 3,
      purchaseDate: '2026-09-01',
    });
  }

  function addParticipant(partyId: string, weight: number): void {
    view.addSplitRow();
    view.form.controls.split.at(0).patchValue({ partyId, weight });
  }

  const instruments: Instrument[] = [
    { id: 'card-credit', type: 'credit', name: 'Visa', cutoffDate: 12 },
    { id: 'acct-debit', type: 'debit', name: 'Checking', cutoffDate: null }
  ];

  beforeEach(() => {
    createPaymentPlan = jasmine
      .createSpy('createPaymentPlan')
      .and.returnValue(of<CreatePaymentPlanResult>({ paymentPlanId: 'plan-1' }));
    getBalance = jasmine.createSpy('getBalance').and.returnValue(of(balance(100000)));
    debtSummary = jasmine.createSpy('debtSummary').and.returnValue(of(partyRows));
    TestBed.configureTestingModule({
      imports: [LoadExpensePage],
      providers: [
        provideZonelessChangeDetection(),
        { provide: FinancingService, useValue: { createPaymentPlan } },
        { provide: PartiesService, useValue: { getBalance } },
        { provide: ReportsService, useValue: { debtSummary } },
        { provide: InstrumentsService, useValue: { list: () => of<Instrument[]>(instruments) } }
      ]
    });
    fixture = TestBed.createComponent(LoadExpensePage);
    view = fixture.componentInstance as unknown as LoadExpenseView;
    fixture.detectChanges();
  });

  it('creates and loads the party list', () => {
    expect(fixture.componentInstance).toBeTruthy();
    expect(view.partiesStatus()).toBe('ready');
    expect(view.parties()).toEqual(partyRows);
  });
  it('offers only credit cards from the instrument list', () => {
    expect(view.form.value.cardId).toBe('');
    const view2 = fixture.componentInstance as unknown as { creditCards: () => Array<{ id: string }> };
    expect(view2.creditCards().map((c) => c.id)).toEqual(['card-credit']);
  });
  it('is invalid until every required field is filled', () => {
    expect(view.form.valid).toBe(false);
    fillValidForm();
    expect(view.form.valid).toBe(true);
  });
  it('rejects an amount with more than two decimal places', () => {
    fillValidForm();
    view.form.controls.amount.setValue(10.005);
    expect(view.form.controls.amount.hasError('atMostTwoDecimals')).toBe(true);
  });
  it('submits the amount in minor units and omits split when there are no participants', () => {
    fillValidForm();
    view.onSubmit();
    expect(createPaymentPlan).toHaveBeenCalledTimes(1);
    const body: CreatePaymentPlan = createPaymentPlan.calls.mostRecent().args[0];
    expect(body.amountMinorUnits).toBe(money(123450));
    expect(body.installmentCount).toBe(3);
    expect(body.purchaseDate).toBe('2026-09-01');
    expect('split' in body).toBe(false);
    expect(view.submitStatus()).toBe('confirmed');
    expect(view.confirmedPlanId()).toBe('plan-1');
  });
  it('includes the split array when participants were added', () => {
    fillValidForm();
    addParticipant('p1', 2);
    view.onSubmit();
    const body: CreatePaymentPlan = createPaymentPlan.calls.mostRecent().args[0];
    expect(body.split).toEqual([{ partyId: 'p1', weight: 2 }]);
  });
  it('surfaces an AppError when the plan cannot be created', () => {
    const appError: AppError = {
      code: 'Http.UnprocessableEntity',
      title: 'Unprocessable',
      detail: 'x',
      status: 422,
      metadata: {},
    };
    createPaymentPlan.and.returnValue(throwError(() => appError));
    fillValidForm();
    view.onSubmit();
    expect(view.submitError()).toEqual(appError);
    expect(view.submitStatus()).toBe('error');
  });
  it('shows the reconciled balance once a participant balance changes', () => {
    const scheduler: TestScheduler = new TestScheduler((actual, expected) =>
      expect(actual).toEqual(expected),
    );
    let call: number = 0;
    getBalance.and.callFake(() => {
      call += 1;
      return of(balance(call === 1 ? 100000 : 150000));
    });
    fillValidForm();
    addParticipant('p1', 1);
    scheduler.run(() => {
      view.onSubmit();
    });
    const row = view.reconciliations()[0];
    expect(row.status).toBe('reconciled');
    expect(row.balanceMinorUnits).toBe(150000);
  });
  it('marks a participant as stalled when the balance never changes', () => {
    const scheduler: TestScheduler = new TestScheduler((actual, expected) =>
      expect(actual).toEqual(expected),
    );
    getBalance.and.returnValue(of(balance(100000)));
    fillValidForm();
    addParticipant('p1', 1);
    scheduler.run(() => {
      view.onSubmit();
    });
    expect(view.reconciliations()[0].status).toBe('stalled');
  });
});

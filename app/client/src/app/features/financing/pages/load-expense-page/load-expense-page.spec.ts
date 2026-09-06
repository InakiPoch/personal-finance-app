import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormArray, FormControl, FormGroup } from '@angular/forms';
import { Observable, of, throwError } from 'rxjs';
import { TestScheduler } from 'rxjs/testing';
import { AppError } from '../../../../core/types/app-error';
import { Money } from '../../../../core/types/money';
import { CreditorsService } from '../../../creditors/creditors-service';
import { Creditor, CreditorAccount } from '../../../creditors/types/creditor';
import { InstrumentsService } from '../../../instruments/instruments-service';
import { Instrument } from '../../../instruments/types/instrument';
import { LedgerService } from '../../../ledger/ledger-service';
import { RecordDebitExpense } from '../../../ledger/types/record-debit-expense';
import { RecordDebitExpenseResult } from '../../../ledger/types/record-debit-expense-result';
import { CurrentAccountBalance } from '../../../parties/types/current-account-balance';
import { Party } from '../../../parties/types/party';
import { PartiesService } from '../../../parties/parties-service';
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
    description: FormControl<string>;
    split: FormArray<SplitRow>;
    mode: FormControl<'card' | 'creditor' | 'debit'>;
    creditorId: FormControl<string>;
    creditorAccountId: FormControl<string>;
    sourceInstrumentId: FormControl<string>;
    categoryName: FormControl<string>;
  }>;
  creditorAccounts: () => CreditorAccount[];
  parties: () => Party[];
  partiesStatus: () => 'loading' | 'ready' | 'error';
  submitStatus: () => 'idle' | 'submitting' | 'confirmed' | 'error';
  submitError: () => AppError | null;
  confirmedPlanId: () => string | null;
  confirmedKind: () => 'plan' | 'expense';
  confirmedDescription: () => string | null;
  reconciliations: () => Array<{
    partyId: string;
    partyName: string;
    status: 'reconciling' | 'reconciled' | 'stalled' | 'scheduled';
    balanceMinorUnits: number | null;
  }>;
  addSplitRow: () => void;
  onSubmit: () => void;
};

describe('LoadExpensePage', () => {
  let fixture: ComponentFixture<LoadExpensePage>;
  let view: LoadExpenseView;
  let createPaymentPlan: jasmine.Spy<(body: CreatePaymentPlan) => Observable<CreatePaymentPlanResult>>;
  let recordDebitExpense: jasmine.Spy<(body: RecordDebitExpense) => Observable<RecordDebitExpenseResult>>;
  let getBalance: jasmine.Spy<(partyId: string) => Observable<CurrentAccountBalance>>;
  let listParties: jasmine.Spy<() => Observable<Party[]>>;

  const money = (value: number): Money => value as Money;
  const partyRoster: Party[] = [{ id: 'p1', name: 'Alice' }];

  function balance(value: number): CurrentAccountBalance {
    return { partyId: 'p1', name: 'Alice', balanceMinorUnits: money(value) };
  }

  function fillValidForm(): void {
    view.form.patchValue({
      amount: 1234.5,
      cardId: 'card-credit',
      installmentCount: 3,
      purchaseDate: '2026-09-01',
      description: 'New laptop'
    });
  }

  function fillValidDebitForm(): void {
    view.form.patchValue({
      mode: 'debit',
      amount: 1234.5,
      purchaseDate: '2026-09-01',
      description: 'Weekly shop',
      sourceInstrumentId: 'acct-debit',
      categoryName: 'Groceries'
    });
  }

  function addParticipant(partyId: string, weight: number): void {
    view.addSplitRow();
    view.form.controls.split.at(0).patchValue({ partyId, weight });
  }

  const instruments: Instrument[] = [
    { id: 'card-credit', type: 'credit', name: 'Visa', cutoffDate: 12 },
    { id: 'acct-debit', type: 'debit', name: 'Checking', cutoffDate: null },
    { id: 'acct-cash', type: 'cash', name: 'Wallet', cutoffDate: null }
  ];
  const creditors: Creditor[] = [
    {
      id: 'creditor-1',
      name: 'Juan',
      accounts: [
        { id: 'acct-1', label: 'Galicia', identifier: 'CBU1' },
        { id: 'acct-2', label: 'Mercado Pago', identifier: null }
      ]
    }
  ];

  beforeEach(() => {
    createPaymentPlan = jasmine
      .createSpy('createPaymentPlan')
      .and.returnValue(of<CreatePaymentPlanResult>({ paymentPlanId: 'plan-1' }));
    recordDebitExpense = jasmine
      .createSpy('recordDebitExpense')
      .and.returnValue(of<RecordDebitExpenseResult>({ id: 'expense-1' }));
    getBalance = jasmine.createSpy('getBalance').and.returnValue(of(balance(100000)));
    listParties = jasmine.createSpy('list').and.returnValue(of<Party[]>(partyRoster));
    TestBed.configureTestingModule({
      imports: [LoadExpensePage],
      providers: [
        provideZonelessChangeDetection(),
        { provide: FinancingService, useValue: { createPaymentPlan } },
        {
          provide: LedgerService,
          useValue: { recordDebitExpense, listExpenseCategories: () => of<string[]>(['Groceries']) }
        },
        { provide: PartiesService, useValue: { getBalance, list: listParties } },
        { provide: InstrumentsService, useValue: { list: () => of<Instrument[]>(instruments) } },
        { provide: CreditorsService, useValue: { list: () => of<Creditor[]>(creditors) } }
      ]
    });
    fixture = TestBed.createComponent(LoadExpensePage);
    view = fixture.componentInstance as unknown as LoadExpenseView;
    fixture.detectChanges();
  });

  it('creates and loads the party list', () => {
    expect(fixture.componentInstance).toBeTruthy();
    expect(view.partiesStatus()).toBe('ready');
    expect(view.parties()).toEqual(partyRoster);
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
    expect(body.cardId).toBe('card-credit');
    expect(body.installmentCount).toBe(3);
    expect(body.purchaseDate).toBe('2026-09-01');
    expect(body.description).toBe('New laptop');
    expect('split' in body).toBe(false);
    expect('creditorId' in body).toBe(false);
    expect('creditorAccountId' in body).toBe(false);
    expect(view.submitStatus()).toBe('confirmed');
    expect(view.confirmedPlanId()).toBe('plan-1');
  });
  it('rejects a whitespace-only description', () => {
    fillValidForm();
    view.form.controls.description.setValue('   ');
    expect(view.form.controls.description.hasError('noBlank')).toBe(true);
  });
  it('rejects a description over 120 characters', () => {
    fillValidForm();
    view.form.controls.description.setValue('a'.repeat(121));
    expect(view.form.controls.description.hasError('maxlength')).toBe(true);
  });
  it('submits the trimmed description', () => {
    fillValidForm();
    view.form.controls.description.setValue('  New laptop  ');
    view.onSubmit();
    const body: CreatePaymentPlan = createPaymentPlan.calls.mostRecent().args[0];
    expect(body.description).toBe('New laptop');
  });
  it('renders the description in the confirmation panel', () => {
    fillValidForm();
    view.onSubmit();
    fixture.detectChanges();
    const text: string = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('New laptop');
    expect(view.confirmedDescription()).toBe('New laptop');
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
      metadata: {}
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
    fillValidDebitForm();
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
    // A debit/cash split still polls; card and creditor are the no-poll 'scheduled' cases below.
    fillValidDebitForm();
    addParticipant('p1', 1);
    scheduler.run(() => {
      view.onSubmit();
    });
    expect(view.reconciliations()[0].status).toBe('stalled');
  });
  it('does not poll for a card split and marks the participant scheduled', () => {
    fillValidForm();
    addParticipant('p1', 1);
    view.onSubmit();
    expect(getBalance).not.toHaveBeenCalled();
    expect(view.reconciliations()[0].status).toBe('scheduled');
  });
  it('does not poll for a creditor-financed split and marks the participant scheduled', () => {
    fillValidForm();
    view.form.controls.mode.setValue('creditor');
    view.form.controls.creditorId.setValue('creditor-1');
    addParticipant('p1', 1);
    view.onSubmit();
    expect(getBalance).not.toHaveBeenCalled();
    expect(view.reconciliations()[0].status).toBe('scheduled');
  });
  it('still polls for a debit split (the card early-exit must not swallow other modes)', () => {
    const scheduler: TestScheduler = new TestScheduler((actual, expected) =>
      expect(actual).toEqual(expected),
    );
    getBalance.and.returnValue(of(balance(100000)));
    fillValidDebitForm();
    addParticipant('p1', 1);
    scheduler.run(() => {
      view.onSubmit();
    });
    expect(getBalance).toHaveBeenCalled();
    expect(view.reconciliations()[0].status).not.toBe('scheduled');
  });
  it('swaps cardId validation for the creditor fields when the mode is "creditor"', () => {
    expect(view.form.controls.cardId.hasError('required')).toBe(true);
    expect(view.form.controls.creditorId.hasError('required')).toBe(false);
    view.form.controls.mode.setValue('creditor');
    expect(view.form.controls.cardId.hasError('required')).toBe(false);
    expect(view.form.controls.cardId.value).toBe('');
    expect(view.form.controls.creditorId.hasError('required')).toBe(true);
    expect(view.form.controls.creditorAccountId.hasError('required')).toBe(true);
  });
  it('restores cardId validation and clears the creditor fields when switched back to "card"', () => {
    view.form.controls.mode.setValue('creditor');
    view.form.controls.creditorId.setValue('creditor-1');
    view.form.controls.mode.setValue('card');
    expect(view.form.controls.cardId.hasError('required')).toBe(true);
    expect(view.form.controls.creditorId.hasError('required')).toBe(false);
    expect(view.form.controls.creditorId.value).toBe('');
    expect(view.form.controls.creditorAccountId.value).toBe('');
    expect(view.creditorAccounts()).toEqual([]);
  });
  it('populates the account options and auto-selects the first when a creditor is chosen', () => {
    view.form.controls.mode.setValue('creditor');
    view.form.controls.creditorId.setValue('creditor-1');
    expect(view.creditorAccounts().map((account) => account.id)).toEqual(['acct-1', 'acct-2']);
    expect(view.form.controls.creditorAccountId.value).toBe('acct-1');
  });
  it('shows the card select in "card" mode and the creditor selects in "creditor" mode', () => {
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('#cardId')).not.toBeNull();
    expect(host.querySelector('#creditorId')).toBeNull();
    view.form.controls.mode.setValue('creditor');
    fixture.detectChanges();
    expect(host.querySelector('#cardId')).toBeNull();
    expect(host.querySelector('#creditorId')).not.toBeNull();
    expect(host.querySelector('#creditorAccountId')).not.toBeNull();
  });
  it('keeps the split section rendered in both modes', () => {
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('[formArrayName="split"]')).not.toBeNull();
    view.form.controls.mode.setValue('creditor');
    fixture.detectChanges();
    expect(host.querySelector('[formArrayName="split"]')).not.toBeNull();
  });
  it('submits cardId and no creditor fields in "card" mode', () => {
    fillValidForm();
    view.onSubmit();
    const body: CreatePaymentPlan = createPaymentPlan.calls.mostRecent().args[0];
    expect(body.cardId).toBe('card-credit');
    expect('creditorId' in body).toBe(false);
    expect('creditorAccountId' in body).toBe(false);
  });
  it('submits creditorId and creditorAccountId and omits cardId in "creditor" mode', () => {
    fillValidForm();
    view.form.controls.mode.setValue('creditor');
    view.form.controls.creditorId.setValue('creditor-1');
    view.onSubmit();
    expect(view.form.valid).toBe(true);
    const body: CreatePaymentPlan = createPaymentPlan.calls.mostRecent().args[0];
    expect('cardId' in body).toBe(false);
    expect(body.creditorId).toBe('creditor-1');
    expect(body.creditorAccountId).toBe('acct-1');
  });
  it('includes a split added in "creditor" mode in the submit body', () => {
    fillValidForm();
    view.form.controls.mode.setValue('creditor');
    view.form.controls.creditorId.setValue('creditor-1');
    addParticipant('p1', 2);
    view.onSubmit();
    const body: CreatePaymentPlan = createPaymentPlan.calls.mostRecent().args[0];
    expect(body.split).toEqual([{ partyId: 'p1', weight: 2 }]);
    expect(body.creditorId).toBe('creditor-1');
    expect('cardId' in body).toBe(false);
  });
  it('requires the source instrument and category and drops cardId in "debit" mode', () => {
    view.form.controls.mode.setValue('debit');
    expect(view.form.controls.cardId.hasError('required')).toBe(false);
    expect(view.form.controls.cardId.value).toBe('');
    expect(view.form.controls.sourceInstrumentId.hasError('required')).toBe(true);
    expect(view.form.controls.categoryName.hasError('required')).toBe(true);
  });
  it('offers both debit and cash instruments as the expense source', () => {
    const view2 = fixture.componentInstance as unknown as { bankAndCashInstruments: () => Array<{ id: string }> };
    expect(view2.bankAndCashInstruments().map((instrument) => instrument.id)).toEqual(['acct-debit', 'acct-cash']);
  });
  it('rejects a whitespace-only category in "debit" mode', () => {
    fillValidDebitForm();
    view.form.controls.categoryName.setValue('   ');
    expect(view.form.controls.categoryName.hasError('noBlank')).toBe(true);
    expect(view.form.valid).toBe(false);
  });
  it('submits the debit-expense payload to the ledger, not a payment plan', () => {
    fillValidDebitForm();
    view.onSubmit();
    expect(recordDebitExpense).toHaveBeenCalledTimes(1);
    expect(createPaymentPlan).not.toHaveBeenCalled();
    const body: RecordDebitExpense = recordDebitExpense.calls.mostRecent().args[0];
    expect(body.amountMinorUnits).toBe(money(123450));
    expect(body.sourceInstrumentId).toBe('acct-debit');
    expect(body.categoryName).toBe('Groceries');
    expect(body.purchaseDate).toBe('2026-09-01');
    expect(body.description).toBe('Weekly shop');
    expect('split' in body).toBe(false);
    expect(view.submitStatus()).toBe('confirmed');
    expect(view.confirmedPlanId()).toBe('expense-1');
    expect(view.confirmedKind()).toBe('expense');
  });
  it('trims the typed category before submitting', () => {
    fillValidDebitForm();
    view.form.controls.categoryName.setValue('  Groceries  ');
    view.onSubmit();
    const body: RecordDebitExpense = recordDebitExpense.calls.mostRecent().args[0];
    expect(body.categoryName).toBe('Groceries');
  });
  it('includes the split array in the debit payload when participants were added', () => {
    fillValidDebitForm();
    addParticipant('p1', 3);
    view.onSubmit();
    const body: RecordDebitExpense = recordDebitExpense.calls.mostRecent().args[0];
    expect(body.split).toEqual([{ partyId: 'p1', weight: 3 }]);
  });
  it('restores cardId validation and clears the debit fields when switched back to "card"', () => {
    fillValidDebitForm();
    view.form.controls.mode.setValue('card');
    expect(view.form.controls.cardId.hasError('required')).toBe(true);
    expect(view.form.controls.sourceInstrumentId.hasError('required')).toBe(false);
    expect(view.form.controls.sourceInstrumentId.value).toBe('');
    expect(view.form.controls.categoryName.value).toBe('');
  });
  it('shows the source select and category input and hides installments in "debit" mode', () => {
    const host = fixture.nativeElement as HTMLElement;
    view.form.controls.mode.setValue('debit');
    fixture.detectChanges();
    expect(host.querySelector('#sourceInstrumentId')).not.toBeNull();
    expect(host.querySelector('#categoryName')).not.toBeNull();
    expect(host.querySelector('#cardId')).toBeNull();
    expect(host.querySelector('#creditorId')).toBeNull();
    expect(host.querySelector('#installmentCount')).toBeNull();
    expect(host.querySelector('[formArrayName="split"]')).not.toBeNull();
  });
  it('headlines the confirmation panel as a recorded expense in "debit" mode', () => {
    fillValidDebitForm();
    view.onSubmit();
    fixture.detectChanges();
    const text: string = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('expense recorded');
  });
});

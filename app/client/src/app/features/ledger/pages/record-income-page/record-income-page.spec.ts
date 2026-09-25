import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { Router } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { AppError } from '../../../../core/types/app-error';
import { Money } from '../../../../core/types/money';
import { InstrumentsService } from '../../../instruments/instruments-service';
import { Instrument } from '../../../instruments/types/instrument';
import { LedgerService } from '../../ledger-service';
import { RecordIncome } from '../../types/record-income';
import { RecordIncomeResult } from '../../types/record-income-result';
import { RecordIncomePage } from './record-income-page';

type RecordIncomeView = {
  form: FormGroup<{
    amount: FormControl<number | null>;
    currency: FormControl<'ARS' | 'USD'>;
    targetAccountId: FormControl<string>;
    receivedOn: FormControl<string>;
    description: FormControl<string>;
  }>;
  submitStatus: () => 'idle' | 'submitting' | 'error';
  submitError: () => AppError | null;
  onSubmit: () => void;
};

describe('RecordIncomePage', () => {
  let fixture: ComponentFixture<RecordIncomePage>;
  let view: RecordIncomeView;
  let recordIncome: jasmine.Spy<(body: RecordIncome) => Observable<RecordIncomeResult>>;
  let navigate: jasmine.Spy<(commands: unknown[]) => Promise<boolean>>;

  const money = (value: number): Money => value as Money;
  const todayIso = (): string => new Date().toISOString().slice(0, 10);
  const futureIso = (): string => new Date(Date.now() + 86_400_000).toISOString().slice(0, 10);

  const instruments: Instrument[] = [
    { id: 'acct-bank', type: 'debit', name: 'Checking', cutoffDate: null },
    { id: 'acct-cash', type: 'cash', name: 'Wallet', cutoffDate: null },
    { id: 'card-credit', type: 'credit', name: 'Visa', cutoffDate: 12 }
  ];

  function fillValidForm(): void {
    view.form.patchValue({
      amount: 500,
      targetAccountId: 'acct-bank',
      receivedOn: todayIso(),
      description: 'Salary'
    });
  }

  beforeEach(() => {
    recordIncome = jasmine.createSpy('recordIncome').and.returnValue(of<RecordIncomeResult>({ id: 'income-1' }));
    navigate = jasmine.createSpy('navigate').and.resolveTo(true);
    TestBed.configureTestingModule({
      imports: [RecordIncomePage],
      providers: [
        provideZonelessChangeDetection(),
        { provide: LedgerService, useValue: { recordIncome } },
        { provide: InstrumentsService, useValue: { list: () => of<Instrument[]>(instruments) } },
        { provide: Router, useValue: { navigate } }
      ]
    });
    fixture = TestBed.createComponent(RecordIncomePage);
    view = fixture.componentInstance as unknown as RecordIncomeView;
    fixture.detectChanges();
  });

  it('offers only bank and cash accounts from the instrument list', () => {
    const view2 = fixture.componentInstance as unknown as { bankAndCashInstruments: () => Instrument[] };
    expect(view2.bankAndCashInstruments().map((i) => i.id)).toEqual(['acct-bank', 'acct-cash']);
  });
  it('is invalid until every required field is filled', () => {
    expect(view.form.valid).toBe(false);
    fillValidForm();
    expect(view.form.valid).toBe(true);
  });
  it('does not submit an invalid form', () => {
    view.onSubmit();
    expect(recordIncome).not.toHaveBeenCalled();
  });
  it('rejects a future received-on date', () => {
    fillValidForm();
    view.form.controls.receivedOn.setValue(futureIso());
    expect(view.form.controls.receivedOn.hasError('notFuture')).toBe(true);
    view.onSubmit();
    expect(recordIncome).not.toHaveBeenCalled();
  });
  it('rejects a whitespace-only description', () => {
    fillValidForm();
    view.form.controls.description.setValue('   ');
    expect(view.form.controls.description.hasError('noBlank')).toBe(true);
  });
  it('submits the amount in minor units, ARS by default, and the trimmed description', () => {
    fillValidForm();
    view.form.controls.description.setValue('  Salary  ');
    view.onSubmit();
    expect(recordIncome).toHaveBeenCalledTimes(1);
    const body: RecordIncome = recordIncome.calls.mostRecent().args[0];
    expect(body.amountMinorUnits).toBe(money(50000));
    expect(body.targetAccountId).toBe('acct-bank');
    expect(body.receivedOn).toBe(todayIso());
    expect(body.description).toBe('Salary');
    expect(body.currencyCode).toBe('ARS');
  });
  it('sends currencyCode USD when USD is selected', () => {
    fillValidForm();
    view.form.controls.currency.setValue('USD');
    view.onSubmit();
    const body: RecordIncome = recordIncome.calls.mostRecent().args[0];
    expect(body.currencyCode).toBe('USD');
  });
  it('navigates to the Dashboard once the income is recorded', () => {
    fillValidForm();
    view.onSubmit();
    expect(navigate).toHaveBeenCalledWith(['reports']);
  });
  it('surfaces an AppError when the income cannot be recorded', () => {
    const appError: AppError = {
      code: 'Ledger.IncomeDateInFuture',
      title: 'Unprocessable',
      detail: 'x',
      status: 422,
      metadata: {}
    };
    recordIncome.and.returnValue(throwError(() => appError));
    fillValidForm();
    view.onSubmit();
    expect(view.submitError()).toEqual(appError);
    expect(view.submitStatus()).toBe('error');
    expect(navigate).not.toHaveBeenCalled();
  });
});

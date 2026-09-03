import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { InstrumentRegistryService } from '../../../../core/registry/instrument-registry-service';
import { AppError } from '../../../../core/types/app-error';
import { Money } from '../../../../core/types/money';
import { FinancingService } from '../../financing-service';
import { MonthlyStatement } from '../../types/monthly-statement';
import { PayStatement } from '../../types/pay-statement';
import { PayStatementResult } from '../../types/pay-statement-result';
import { StatementPage } from './statement-page';

type StatementView = {
  form: FormGroup<{
    bankAccountId: FormControl<string>;
    paidOnUtc: FormControl<string>;
  }>;
  statement: () => MonthlyStatement | null;
  loadStatus: () => 'loading' | 'ready' | 'error';
  payStatus: () => 'idle' | 'paying' | 'paid' | 'error';
  payError: () => AppError | null;
  onSubmit: () => void;
};

describe('StatementPage', () => {
  let fixture: ComponentFixture<StatementPage>;
  let view: StatementView;
  let getStatement: jasmine.Spy<(id: string) => Observable<MonthlyStatement>>;
  let payStatement: jasmine.Spy<(id: string, body: PayStatement) => Observable<PayStatementResult>>;

  const money = (value: number): Money => value as Money;

  const unpaidStatement: MonthlyStatement = {
    statementId: 'st-1',
    cardId: 'card-credit',
    cardName: 'Visa',
    cycleYear: 2026,
    cycleMonth: 9,
    amountDueMinorUnits: money(450000),
    isPaid: false,
    paidOnUtc: null,
    installments: [{
      planId: 'pl1',
      installmentId: 'i1',
      sequence: 1,
      installmentCount: 3,
      purchaseDate: '2026-09-01',
      cycleYear: 2026,
      cycleMonth: 9,
      amountMinorUnits: money(150000),
      isReversed: false
    }]
  };
  const paidStatement: MonthlyStatement = {
    ...unpaidStatement,
    isPaid: true,
    paidOnUtc: '2026-09-20T12:00:00Z',
  };

  function setup(): void {
    fixture = TestBed.createComponent(StatementPage);
    view = fixture.componentInstance as unknown as StatementView;
  }

  function fillPayForm(): void {
    view.form.setValue({ bankAccountId: 'acct-debit', paidOnUtc: '2026-09-15T10:30' });
  }

  beforeEach(() => {
    localStorage.clear();
    getStatement = jasmine.createSpy('getStatement').and.returnValue(of(unpaidStatement));
    payStatement = jasmine
      .createSpy('payStatement')
      .and.returnValue(of<PayStatementResult>({ statementId: 'st-1' }));
    TestBed.configureTestingModule({
      imports: [StatementPage],
      providers: [
        provideZonelessChangeDetection(),
        { provide: FinancingService, useValue: { getStatement, payStatement } },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ id: 'st-1' })) } }
      ]
    });
    const registry: InstrumentRegistryService = TestBed.inject(InstrumentRegistryService);
    registry.add({ id: 'acct-debit', type: 'debit', name: 'Checking' });
    registry.add({ id: 'card-credit', type: 'credit', name: 'Visa' });
  });

  afterEach(() => localStorage.clear());

  it('loads the statement named by the route param', () => {
    setup();
    expect(view.loadStatus()).toBe('loading');
    fixture.detectChanges();
    expect(getStatement).toHaveBeenCalledWith('st-1');
    expect(view.loadStatus()).toBe('ready');
    expect(view.statement()?.statementId).toBe('st-1');
  });
  it('renders the cycle, amount due and installments as "N of M"', () => {
    setup();
    fixture.detectChanges();
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('2026-9');
    expect(text).toContain('$');
    expect(text).toContain('1 of 3');
  });
  it('shows the reversal-credit note', () => {
    setup();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('netted');
  });
  it('keeps the pay form invalid until an account and date are chosen', () => {
    setup();
    fixture.detectChanges();
    expect(view.form.valid).toBe(false);
    fillPayForm();
    expect(view.form.valid).toBe(true);
  });
  it('records the payment then refetches the statement', () => {
    setup();
    fixture.detectChanges();
    fillPayForm();
    view.onSubmit();
    expect(payStatement).toHaveBeenCalledTimes(1);
    const [id, body]: [string, PayStatement] = payStatement.calls.mostRecent().args;
    expect(id).toBe('st-1');
    expect(body.bankAccountId).toBe('acct-debit');
    expect(body.paidOnUtc).toBe(new Date('2026-09-15T10:30').toISOString());
    expect(view.payStatus()).toBe('paid');
    expect(getStatement).toHaveBeenCalledTimes(2);
  });
  it('surfaces an AppError when the payment fails', () => {
    const appError: AppError = {
      code: 'Financing.AlreadyPaid',
      title: 'Already paid',
      detail: 'x',
      status: 409,
      metadata: {},
    };
    payStatement.and.returnValue(throwError(() => appError));
    setup();
    fixture.detectChanges();
    fillPayForm();
    view.onSubmit();
    expect(view.payError()).toEqual(appError);
    expect(view.payStatus()).toBe('error');
    expect(getStatement).toHaveBeenCalledTimes(1);
  });
  it('hides the pay form when the statement is already paid', () => {
    getStatement.and.returnValue(of(paidStatement));
    setup();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.statement__pay')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Paid');
  });
});

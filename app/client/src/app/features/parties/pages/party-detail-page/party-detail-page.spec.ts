import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { formatMoney } from '../../../../core/money/money';
import { AppError } from '../../../../core/types/app-error';
import { CurrencyCode } from '../../../../core/types/currency-code';
import { Money } from '../../../../core/types/money';
import { InstrumentsService } from '../../../instruments/instruments-service';
import { Instrument } from '../../../instruments/types/instrument';
import { ReportsService } from '../../../reports/reports-service';
import { PartyTimelineRow } from '../../../reports/types/party-timeline-row';
import { CurrentAccountBalance } from '../../types/current-account-balance';
import { FuturePartyShare } from '../../types/future-party-share';
import { LoanResult } from '../../types/loan-result';
import { RecordLoan } from '../../types/record-loan';
import { SettleCurrentAccount } from '../../types/settle-current-account';
import { SettlementResult } from '../../types/settlement-result';
import { PartiesService } from '../../parties-service';
import { PartyDetailPage } from './party-detail-page';

type PartyDetailView = {
  form: FormGroup<{
    amount: FormControl<number | null>;
    currency: FormControl<CurrencyCode>;
    bankAccountId: FormControl<string>;
    settledOnUtc: FormControl<string>;
  }>;
  balance: () => CurrentAccountBalance | null;
  timeline: () => PartyTimelineRow[];
  futureShares: () => FuturePartyShare[];
  balanceStatus: () => 'loading' | 'ready' | 'error';
  timelineStatus: () => 'loading' | 'ready' | 'error';
  futureSharesStatus: () => 'loading' | 'ready' | 'error';
  settleStatus: () => 'idle' | 'settling' | 'settled' | 'error';
  settleError: () => AppError | null;
  onSubmit: () => void;
  loanForm: FormGroup<{
    amount: FormControl<number | null>;
    currency: FormControl<CurrencyCode>;
    sourceAccountId: FormControl<string>;
    lentOn: FormControl<string>;
    description: FormControl<string>;
  }>;
  loanStatus: () => 'idle' | 'saving' | 'saved' | 'error';
  loanError: () => AppError | null;
  onLoanSubmit: () => void;
};

const money = (value: number): Money => value as Money;

const balance: CurrentAccountBalance = {
  partyId: 'p1',
  name: 'Alice',
  balances: [{ currencyCode: 'ARS', balanceMinorUnits: money(250000) }]
};

const twoCurrencyBalance: CurrentAccountBalance = {
  partyId: 'p1',
  name: 'Alice',
  balances: [
    { currencyCode: 'ARS', balanceMinorUnits: money(250000) },
    { currencyCode: 'USD', balanceMinorUnits: money(5000) }
  ]
};

const timelineRows: PartyTimelineRow[] = [{
    transactionId: 'tx-1',
    movementOnUtc: '2026-09-01T20:00:00.000Z',
    description: 'Shared expense',
    deltaMinorUnits: money(250000),
    runningBalanceMinorUnits: money(250000),
    currencyCode: 'ARS'
  }];

// The API's future-shares rows carry the DUE cycle
const futureShareRows: FuturePartyShare[] = [
  {
    cycleYear: 2026,
    cycleMonth: 10,
    shareMinorUnits: money(33333),
    currencyCode: 'ARS',
    sourceLabel: 'Visa — Shared laptop'
  },
  {
    cycleYear: 2026,
    cycleMonth: 11,
    shareMinorUnits: money(33333),
    currencyCode: 'ARS',
    sourceLabel: 'Visa — Shared laptop'
  }
];

describe('PartyDetailPage', () => {
  let fixture: ComponentFixture<PartyDetailPage>;
  let view: PartyDetailView;
  let getBalance: jasmine.Spy<(id: string) => Observable<CurrentAccountBalance>>;
  let partyTimeline: jasmine.Spy<(id: string) => Observable<PartyTimelineRow[]>>;
  let futureShares: jasmine.Spy<(id: string) => Observable<FuturePartyShare[]>>;
  let recordLoan: jasmine.Spy<(id: string, body: RecordLoan) => Observable<LoanResult>>;
  let settle: jasmine.Spy<(id: string, body: SettleCurrentAccount) => Observable<SettlementResult>>;

  function setup(): void {
    fixture = TestBed.createComponent(PartyDetailPage);
    view = fixture.componentInstance as unknown as PartyDetailView;
    fixture.detectChanges();
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function fillSettleForm(): void {
    view.form.setValue({
      amount: 2500,
      currency: 'ARS',
      bankAccountId: 'acct-debit',
      settledOnUtc: '2026-09-15T10:30'
    });
  }

  const instruments: Instrument[] = [
    { id: 'acct-debit', type: 'debit', name: 'Checking', cutoffDate: null, nextClosingDate: null }
  ];

  beforeEach(() => {
    getBalance = jasmine.createSpy('getBalance').and.returnValue(of(balance));
    partyTimeline = jasmine.createSpy('partyTimeline').and.returnValue(of(timelineRows));
    futureShares = jasmine.createSpy('futureShares').and.returnValue(of<FuturePartyShare[]>([]));
    recordLoan = jasmine.createSpy('recordLoan').and.returnValue(of<LoanResult>({ ledgerTransactionId: 'tx-2' }));
    settle = jasmine
      .createSpy('settle')
      .and.returnValue(of<SettlementResult>({ ledgerTransactionId: 'tx-1' }));
    TestBed.configureTestingModule({
      imports: [PartyDetailPage],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: PartiesService, useValue: { getBalance, futureShares, settle, recordLoan } },
        { provide: ReportsService, useValue: { partyTimeline } },
        { provide: InstrumentsService, useValue: { list: () => of<Instrument[]>(instruments) } },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ id: 'p1' })) } }
      ]
    });
  });

  it('loads the balance and timeline named by the route param', () => {
    setup();
    expect(getBalance).toHaveBeenCalledWith('p1');
    expect(partyTimeline).toHaveBeenCalledWith('p1');
    expect(view.balanceStatus()).toBe('ready');
    expect(view.timelineStatus()).toBe('ready');
    expect(view.balance()?.name).toBe('Alice');
    expect(text()).toContain('Shared expense');
  });
  it('links to Load an Expense with this party prefilled in the split', () => {
    setup();
    const link: HTMLAnchorElement = fixture.nativeElement.querySelector('a[href^="/financing/load-expense"]');
    expect(link.getAttribute('href')).toBe('/financing/load-expense?party=p1');
    expect(link.textContent).toContain('Split an expense with Alice');
  });
  it('navigates to the id-driven reverse route when a timeline Reverse button is clicked', () => {
    setup();
    const navigate: jasmine.Spy = spyOn(TestBed.inject(Router), 'navigate');
    const button: HTMLButtonElement =
      fixture.nativeElement.querySelector('app-timeline-table tbody tr button');
    button.click();
    expect(navigate).toHaveBeenCalledWith(['ledger', 'transactions', 'tx-1', 'reverse']);
  });
  it('keeps the settlement form invalid until amount, account and date are chosen', () => {
    setup();
    expect(view.form.valid).toBe(false);
    fillSettleForm();
    expect(view.form.valid).toBe(true);
  });
  it('submits a minor-units settlement then re-fetches balance and timeline', () => {
    setup();
    getBalance.calls.reset();
    partyTimeline.calls.reset();
    fillSettleForm();
    view.onSubmit();
    expect(settle).toHaveBeenCalledTimes(1);
    const [id, body]: [string, SettleCurrentAccount] = settle.calls.mostRecent().args;
    expect(id).toBe('p1');
    expect(body.amountMinorUnits).toBe(money(250000));
    expect(body.currencyCode).toBe('ARS');
    expect(body.bankAccountId).toBe('acct-debit');
    expect(body.settledOnUtc).toBe(new Date('2026-09-15T10:30').toISOString());
    expect(view.settleStatus()).toBe('settled');
    expect(getBalance).toHaveBeenCalledTimes(1);
    expect(partyTimeline).toHaveBeenCalledTimes(1);
  });
  it('renders one balance line per currency the party owes', () => {
    getBalance.and.returnValue(of(twoCurrencyBalance));
    setup();
    expect(text()).toContain(formatMoney(money(250000), 'ARS'));
    expect(text()).toContain(formatMoney(money(5000), 'USD'));
  });
  it('offers only the currencies the party actually owes on the settle form', () => {
    getBalance.and.returnValue(of(twoCurrencyBalance));
    setup();
    const options: NodeListOf<HTMLOptionElement> = fixture.nativeElement.querySelectorAll('#currency option');
    expect(options.length).toBe(2);
    expect(Array.from(options).map((option: HTMLOptionElement) => option.value)).toEqual(['ARS', 'USD']);
  });
  it('sends the chosen settlement currency', () => {
    getBalance.and.returnValue(of(twoCurrencyBalance));
    setup();
    view.form.setValue({
      amount: 50,
      currency: 'USD',
      bankAccountId: 'acct-debit',
      settledOnUtc: '2026-09-15T10:30'
    });
    view.onSubmit();
    const [, body]: [string, SettleCurrentAccount] = settle.calls.mostRecent().args;
    expect(body.currencyCode).toBe('USD');
  });
  it('renders each scheduled share under its due month, verbatim from the API', () => {
    futureShares.and.returnValue(of<FuturePartyShare[]>(futureShareRows));
    setup();
    expect(futureShares).toHaveBeenCalledWith('p1');
    expect(view.futureSharesStatus()).toBe('ready');
    expect(view.futureShares().length).toBe(2);
    expect(text()).toContain('Scheduled');
    // cycleMonth 10 / 11 are the due cycle already — no client-side shift
    expect(text()).toContain('Oct 2026');
    expect(text()).toContain('Nov 2026');
    expect(text()).toContain('Visa — Shared laptop');
  });
  it('shows the empty note when the party has no scheduled shares', () => {
    setup();
    expect(view.futureShares().length).toBe(0);
    expect(text()).toContain('Nothing scheduled');
  });
  it('keeps the loan form invalid until amount, account, date and a one-line description are set', () => {
    setup();
    expect(view.loanForm.valid).toBe(false);
    const valid = { amount: 100, currency: 'ARS' as CurrencyCode, sourceAccountId: 'acct-debit', lentOn: '2026-09-15', description: 'Rent help' };
    view.loanForm.setValue(valid);
    expect(view.loanForm.valid).toBe(true);
    view.loanForm.patchValue({ amount: 0 });
    expect(view.loanForm.valid).toBe(false);
    view.loanForm.patchValue({ amount: 100, description: '' });
    expect(view.loanForm.valid).toBe(false);
    view.loanForm.patchValue({ description: 'x'.repeat(121) });
    expect(view.loanForm.valid).toBe(false);
    view.loanForm.patchValue({ description: 'two\nlines' });
    expect(view.loanForm.valid).toBe(false);
    view.loanForm.patchValue({ description: 'ok', lentOn: '2999-01-01' });
    expect(view.loanForm.valid).toBe(false);
  });
  it('submits a minor-units loan then re-fetches balance and timeline', () => {
    setup();
    getBalance.calls.reset();
    partyTimeline.calls.reset();
    view.loanForm.setValue({ amount: 25, currency: 'USD', sourceAccountId: 'acct-debit', lentOn: '2026-09-15', description: ' Rent help ' });
    view.onLoanSubmit();
    const [id, body]: [string, RecordLoan] = recordLoan.calls.mostRecent().args;
    expect(id).toBe('p1');
    expect(body).toEqual({ amountMinorUnits: money(2500), currencyCode: 'USD', sourceAccountId: 'acct-debit', lentOn: '2026-09-15', description: 'Rent help' });
    expect(view.loanStatus()).toBe('saved');
    expect(getBalance).toHaveBeenCalledTimes(1);
    expect(partyTimeline).toHaveBeenCalledTimes(1);
  });
  it('shows a message keyed off the AppError code when the loan is rejected', () => {
    const appError: AppError = { code: 'Parties.LoanDateInFuture', title: 'x', detail: 'x', status: 422, metadata: {} };
    recordLoan.and.returnValue(throwError(() => appError));
    setup();
    view.loanForm.setValue({ amount: 25, currency: 'ARS', sourceAccountId: 'acct-debit', lentOn: '2026-09-15', description: 'Loan' });
    view.onLoanSubmit();
    fixture.detectChanges();
    expect(view.loanStatus()).toBe('error');
    expect(text()).toContain('The loan date cannot be in the future.');
  });
  it('renders the Record a loan form', () => {
    setup();
    expect(text()).toContain('Record a loan');
  });
  it('renders settleErrorText keyed off the AppError code on a 409', () => {
    const appError: AppError = {
      code: 'Parties.SettlementExceedsBalance',
      title: 'Conflict',
      detail: 'x',
      status: 409,
      metadata: {}
    };
    settle.and.returnValue(throwError(() => appError));
    setup();
    fillSettleForm();
    view.onSubmit();
    fixture.detectChanges();
    expect(view.settleStatus()).toBe('error');
    expect(view.settleError()).toEqual(appError);
    expect(text()).toContain('The amount is more than what this party owes.');
  });
});

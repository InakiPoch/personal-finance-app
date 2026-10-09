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
import { LedgerService } from '../../../ledger/ledger-service';
import { Instrument } from '../../../instruments/types/instrument';
import { ReportsService } from '../../../reports/reports-service';
import { PartyTimelineRow } from '../../../reports/types/party-timeline-row';
import { PartyTimelineSide } from '../../../reports/types/party-timeline-side';
import { CurrentAccountBalance } from '../../types/current-account-balance';
import { FuturePartyShare } from '../../types/future-party-share';
import { BorrowingResult } from '../../types/borrowing-result';
import { LoanResult } from '../../types/loan-result';
import { RecordRepayment } from '../../types/record-repayment';
import { RepaymentResult } from '../../types/repayment-result';
import { RecordBorrowing } from '../../types/record-borrowing';
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
  toggleScheduled: () => void;
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
  borrowForm: FormGroup<{
    amount: FormControl<number | null>;
    currency: FormControl<CurrencyCode>;
    destinationAccountId: FormControl<string>;
    borrowedOn: FormControl<string>;
    description: FormControl<string>;
  }>;
  borrowStatus: () => 'idle' | 'saving' | 'saved' | 'error';
  borrowError: () => AppError | null;
  onBorrowSubmit: () => void;
  repayForm: FormGroup<{
    amount: FormControl<number | null>;
    currency: FormControl<CurrencyCode>;
    sourceAccountId: FormControl<string>;
    paidOn: FormControl<string>;
  }>;
  repayStatus: () => 'idle' | 'saving' | 'saved' | 'error';
  repayError: () => AppError | null;
  onRepaySubmit: () => void;
  side: () => PartyTimelineSide;
  setSide: (side: PartyTimelineSide) => void;
};

const money = (value: number): Money => value as Money;

const balance: CurrentAccountBalance = {
  partyId: 'p1',
  name: 'Alice',
  balances: [{ currencyCode: 'ARS', balanceMinorUnits: money(250000) }],
  payableBalances: []
};

const twoCurrencyBalance: CurrentAccountBalance = {
  partyId: 'p1',
  name: 'Alice',
  balances: [
    { currencyCode: 'ARS', balanceMinorUnits: money(250000) },
    { currencyCode: 'USD', balanceMinorUnits: money(5000) }
  ],
  payableBalances: []
};

const bothSidesBalance: CurrentAccountBalance = {
  partyId: 'p1',
  name: 'Alice',
  balances: [{ currencyCode: 'ARS', balanceMinorUnits: money(250000) }],
  payableBalances: [{ currencyCode: 'USD', balanceMinorUnits: money(9000) }]
};

const payableRows: PartyTimelineRow[] = [{
    transactionId: 'tx-b1',
    movementOnUtc: '2026-09-02T20:00:00.000Z',
    description: 'Borrowed from Alice',
    deltaMinorUnits: money(9000),
    runningBalanceMinorUnits: money(9000),
    currencyCode: 'USD'
  }];

const purchaseRows: PartyTimelineRow[] = [{
    transactionId: 'tx-pp1',
    movementOnUtc: '2026-09-03T00:00:00.000Z',
    description: 'Paid by Alice: Dinner',
    deltaMinorUnits: money(12000),
    runningBalanceMinorUnits: money(12000),
    currencyCode: 'ARS',
    purchaseId: 'pu-1'
  }];

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
  let partyTimeline: jasmine.Spy<(id: string, side: PartyTimelineSide) => Observable<PartyTimelineRow[]>>;
  let recordBorrowing: jasmine.Spy<(id: string, body: RecordBorrowing) => Observable<BorrowingResult>>;
  let futureShares: jasmine.Spy<(id: string, side: PartyTimelineSide) => Observable<FuturePartyShare[]>>;
  let repay: jasmine.Spy<(id: string, body: RecordRepayment) => Observable<RepaymentResult>>;
  let recordLoan: jasmine.Spy<(id: string, body: RecordLoan) => Observable<LoanResult>>;
  let settle: jasmine.Spy<(id: string, body: SettleCurrentAccount) => Observable<SettlementResult>>;
  let undoPartyPurchase: jasmine.Spy<(id: string, purchaseId: string) => Observable<void>>;

  function setup(): void {
    fixture = TestBed.createComponent(PartyDetailPage);
    view = fixture.componentInstance as unknown as PartyDetailView;
    fixture.detectChanges();
  }

  function expandScheduled(): void {
    view.toggleScheduled();
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
    recordBorrowing = jasmine
      .createSpy('recordBorrowing')
    .and.returnValue(of<BorrowingResult>({ ledgerTransactionId: 'tx-3' }));
    repay = jasmine.createSpy('repay').and.returnValue(of<RepaymentResult>({ ledgerTransactionId: 'tx-4' }));
    settle = jasmine
      .createSpy('settle')
      .and.returnValue(of<SettlementResult>({ ledgerTransactionId: 'tx-1' }));
    undoPartyPurchase = jasmine.createSpy('undoPartyPurchase').and.returnValue(of(undefined));
    TestBed.configureTestingModule({
      imports: [PartyDetailPage],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: PartiesService, useValue: { getBalance, futureShares, settle, recordLoan, recordBorrowing, undoPartyPurchase, repay } },
        { provide: LedgerService, useValue: { listExpenseCategories: () => of<string[]>([]) } },
        { provide: ReportsService, useValue: { partyTimeline } },
        { provide: InstrumentsService, useValue: { list: () => of<Instrument[]>(instruments) } },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ id: 'p1' })) } }
      ]
    });
  });

  it('loads the balance and timeline named by the route param', () => {
    setup();
    expect(getBalance).toHaveBeenCalledWith('p1');
    expect(partyTimeline).toHaveBeenCalledWith('p1', 'receivable');
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
    expandScheduled();
    expect(futureShares).toHaveBeenCalledWith('p1', 'receivable');
    expect(view.futureSharesStatus()).toBe('ready');
    expect(view.futureShares().length).toBe(2);
    expect(text()).toContain('Scheduled');
    // cycleMonth 10 / 11 are the due cycle already — no client-side shift
    expect(text()).toContain('Oct 2026');
    expect(text()).toContain('Nov 2026');
    expect(text()).toContain('Visa — Shared laptop');
  });
  it('follows the Owed to me / I owe toggle in the Scheduled block', () => {
    const payableShares: FuturePartyShare[] = [
      { cycleYear: 2026, cycleMonth: 11, shareMinorUnits: money(5000), currencyCode: 'ARS', sourceLabel: 'Paid by Alice: Fridge (1/2)', purchaseId: 'pu-9' },
      { cycleYear: 2026, cycleMonth: 12, shareMinorUnits: money(5000), currencyCode: 'ARS', sourceLabel: 'Paid by Alice: Fridge (2/2)', purchaseId: 'pu-9' }
    ];
    futureShares.and.callFake((_id: string, side: PartyTimelineSide) => of(side === 'payable' ? payableShares : futureShareRows));
    setup();
    expandScheduled();
    expect(text()).toContain('Visa — Shared laptop');
    view.setSide('payable');
    fixture.detectChanges();
    expect(futureShares).toHaveBeenCalledWith('p1', 'payable');
    expect(text()).not.toContain('Visa — Shared laptop');
    expect(text()).toContain('Paid by Alice: Fridge (1/2)');
    expect(text()).toContain('Nov 2026');
  });
  it('undoes a whole credit purchase from its first scheduled row and refreshes', () => {
    const payableShares: FuturePartyShare[] = [
      { cycleYear: 2026, cycleMonth: 11, shareMinorUnits: money(5000), currencyCode: 'ARS', sourceLabel: 'Paid by Alice: Fridge (1/2)', purchaseId: 'pu-9' },
      { cycleYear: 2026, cycleMonth: 12, shareMinorUnits: money(5000), currencyCode: 'ARS', sourceLabel: 'Paid by Alice: Fridge (2/2)', purchaseId: 'pu-9' }
    ];
    futureShares.and.returnValue(of(payableShares));
    setup();
    view.setSide('payable');
    expandScheduled();
    fixture.detectChanges();
    const buttons: HTMLButtonElement[] = Array.from(fixture.nativeElement.querySelectorAll('#scheduled-body button'));
    expect(buttons.length).toBe(1);
    expect(buttons[0].textContent).toContain('Undo purchase');
    futureShares.calls.reset();
    buttons[0].click();
    expect(undoPartyPurchase).toHaveBeenCalledWith('p1', 'pu-9');
    expect(futureShares).toHaveBeenCalledTimes(1);
  });
  it('shows the empty note when the party has no scheduled shares', () => {
    setup();
    expandScheduled();
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
    expect(body).toEqual({ amountMinorUnits: money(2500), currencyCode: 'USD', sourceAccountId: 'acct-debit', lentOn: '2026-09-15', description: 'Rent help', today: new Date().toLocaleDateString('sv-SE') });
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
  it('renders the Money borrowed form next to Money lent', () => {
    setup();
    expect(text()).toContain('Money borrowed');
    expect(text()).toContain('Money lent');
  });
  it('keeps the borrow form invalid until amount, account, date and a one-line description are set', () => {
    setup();
    expect(view.borrowForm.valid).toBe(false);
    view.borrowForm.setValue({ amount: 100, currency: 'ARS', destinationAccountId: 'acct-debit', borrowedOn: '2026-09-15', description: 'Rent gap' });
    expect(view.borrowForm.valid).toBe(true);
    view.borrowForm.patchValue({ amount: 0 });
    expect(view.borrowForm.valid).toBe(false);
    view.borrowForm.patchValue({ amount: 100, description: 'two\nlines' });
    expect(view.borrowForm.valid).toBe(false);
    view.borrowForm.patchValue({ description: 'ok', borrowedOn: '2999-01-01' });
    expect(view.borrowForm.valid).toBe(false);
  });
  it('submits a minor-units borrowing then re-fetches balance and the current timeline side', () => {
    setup();
    getBalance.calls.reset();
    partyTimeline.calls.reset();
    view.borrowForm.setValue({ amount: 90, currency: 'USD', destinationAccountId: 'acct-debit', borrowedOn: '2026-09-15', description: ' Gear ' });
    view.onBorrowSubmit();
    const [id, body]: [string, RecordBorrowing] = recordBorrowing.calls.mostRecent().args;
    expect(id).toBe('p1');
    expect(body).toEqual({ amountMinorUnits: money(9000), currencyCode: 'USD', destinationAccountId: 'acct-debit', borrowedOn: '2026-09-15', description: 'Gear', today: new Date().toLocaleDateString('sv-SE') });
    expect(view.borrowStatus()).toBe('saved');
    expect(getBalance).toHaveBeenCalledTimes(1);
    expect(partyTimeline).toHaveBeenCalledTimes(1);
  });
  it('shows a message keyed off the AppError code when the borrowing is rejected', () => {
    const appError: AppError = { code: 'Parties.BorrowingDateInFuture', title: 'x', detail: 'x', status: 422, metadata: {} };
    recordBorrowing.and.returnValue(throwError(() => appError));
    setup();
    view.borrowForm.setValue({ amount: 25, currency: 'ARS', destinationAccountId: 'acct-debit', borrowedOn: '2026-09-15', description: 'Loan' });
    view.onBorrowSubmit();
    fixture.detectChanges();
    expect(view.borrowStatus()).toBe('error');
    expect(text()).toContain('The borrowing date cannot be in the future.');
  });
  it('renders the Paid by form for this party', () => {
    setup();
    expect(text()).toContain('Paid by Alice');
  });
  it('re-fetches balance and timeline when a purchase is recorded', () => {
    setup();
    getBalance.calls.reset();
    partyTimeline.calls.reset();
    (view as unknown as { onPurchaseRecorded: () => void }).onPurchaseRecorded();
    expect(getBalance).toHaveBeenCalledTimes(1);
    expect(partyTimeline).toHaveBeenCalledTimes(1);
  });
  it('undoes a purchase as a whole from the I owe timeline and refreshes', () => {
    partyTimeline.and.callFake((_id: string, side: PartyTimelineSide) => of(side === 'payable' ? purchaseRows : timelineRows));
    setup();
    view.setSide('payable');
    fixture.detectChanges();
    getBalance.calls.reset();
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('app-timeline-table tbody tr button');
    expect(button.textContent).toContain('Undo purchase');
    button.click();
    expect(undoPartyPurchase).toHaveBeenCalledWith('p1', 'pu-1');
    expect(getBalance).toHaveBeenCalledTimes(1);
  });
  it('tells the user when a purchase cannot be undone', () => {
    const appError: AppError = { code: 'Ledger.TransactionAlreadyReversed', title: 'x', detail: 'x', status: 409, metadata: {} };
    undoPartyPurchase.and.returnValue(throwError(() => appError));
    partyTimeline.and.callFake((_id: string, side: PartyTimelineSide) => of(side === 'payable' ? purchaseRows : timelineRows));
    setup();
    view.setSide('payable');
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('app-timeline-table tbody tr button') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(text()).toContain('The purchase could not be undone');
  });
  it('shows both what they owe you and what you owe them in the balance', () => {
    getBalance.and.returnValue(of(bothSidesBalance));
    setup();
    expect(text()).toContain('What this party owes you in ARS.');
    expect(text()).toContain(formatMoney(money(9000), 'USD'));
    expect(text()).toContain('What you owe this party in USD.');
  });
  it('defaults the timeline to Owed to me and switches to I owe on the toggle', () => {
    partyTimeline.and.callFake((_id: string, side: PartyTimelineSide) => of(side === 'payable' ? payableRows : timelineRows));
    setup();
    expect(view.side()).toBe('receivable');
    expect(text()).toContain('Shared expense');
    const labels: HTMLElement[] = Array.from(fixture.nativeElement.querySelectorAll('input[name="timelineSide"]'))
    .map((radio: unknown) => (radio as HTMLElement).closest('label') as HTMLElement);
    expect(labels.map((label: HTMLElement) => label.textContent?.trim())).toEqual(['Owed to me', 'I owe']);
    (fixture.nativeElement.querySelectorAll('input[name="timelineSide"]')[1] as HTMLInputElement).click();
    fixture.detectChanges();
    expect(partyTimeline).toHaveBeenCalledWith('p1', 'payable');
    expect(view.side()).toBe('payable');
    expect(text()).toContain('Borrowed from Alice');
    expect(text()).not.toContain('Shared expense');
  });
  it('renders the Money lent form', () => {
    setup();
    expect(text()).toContain('Money lent');
  });
  it('renders the Pay back form next to Record settlement', () => {
    setup();
    expect(text()).toContain('Pay back');
    expect(text()).toContain('Record settlement');
  });
  it('keeps the repay form invalid until amount, account and a non-future date are set', () => {
    setup();
    expect(view.repayForm.valid).toBe(false);
    view.repayForm.setValue({ amount: 100, currency: 'ARS', sourceAccountId: 'acct-debit', paidOn: '2026-09-15' });
    expect(view.repayForm.valid).toBe(true);
    view.repayForm.patchValue({ amount: 0 });
    expect(view.repayForm.valid).toBe(false);
    view.repayForm.patchValue({ amount: 100, paidOn: '2999-01-01' });
    expect(view.repayForm.valid).toBe(false);
  });
  it('offers only the currencies I owe on the Pay back form', () => {
    getBalance.and.returnValue(of(bothSidesBalance));
    setup();
    const options: NodeListOf<HTMLOptionElement> = fixture.nativeElement.querySelectorAll('#repayCurrency option');
    expect(Array.from(options).map((option: HTMLOptionElement) => option.value)).toEqual(['USD']);
  });
  it('submits a minor-units repayment then re-fetches balance and timeline', () => {
    setup();
    getBalance.calls.reset();
    partyTimeline.calls.reset();
    view.repayForm.setValue({ amount: 90, currency: 'USD', sourceAccountId: 'acct-debit', paidOn: '2026-09-15' });
    view.onRepaySubmit();
    const [id, body]: [string, RecordRepayment] = repay.calls.mostRecent().args;
    expect(id).toBe('p1');
    expect(body).toEqual({ amountMinorUnits: money(9000), currencyCode: 'USD', sourceAccountId: 'acct-debit', paidOn: '2026-09-15', today: new Date().toLocaleDateString('sv-SE') });
    expect(view.repayStatus()).toBe('saved');
    expect(getBalance).toHaveBeenCalledTimes(1);
    expect(partyTimeline).toHaveBeenCalledTimes(1);
  });
  it('shows a message keyed off the AppError code when the repayment exceeds what I owe', () => {
    const appError: AppError = { code: 'Parties.RepaymentExceedsBalance', title: 'x', detail: 'x', status: 409, metadata: {} };
    repay.and.returnValue(throwError(() => appError));
    setup();
    view.repayForm.setValue({ amount: 25, currency: 'ARS', sourceAccountId: 'acct-debit', paidOn: '2026-09-15' });
    view.onRepaySubmit();
    fixture.detectChanges();
    expect(view.repayStatus()).toBe('error');
    expect(text()).toContain('You cannot pay back more than you owe.');
  });
  it('renders a Reverse button on the I owe timeline', () => {
    partyTimeline.and.callFake((_id: string, side: PartyTimelineSide) => of(side === 'payable' ? payableRows : timelineRows));
    setup();
    view.setSide('payable');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('app-timeline-table tbody tr button')).not.toBeNull();
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

import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { AppError } from '../../../../core/types/app-error';
import { Money } from '../../../../core/types/money';
import { InstrumentsService } from '../../../instruments/instruments-service';
import { Instrument } from '../../../instruments/types/instrument';
import { ReportsService } from '../../../reports/reports-service';
import { PartyTimelineRow } from '../../../reports/types/party-timeline-row';
import { CurrentAccountBalance } from '../../types/current-account-balance';
import { SettleCurrentAccount } from '../../types/settle-current-account';
import { SettlementResult } from '../../types/settlement-result';
import { PartiesService } from '../../parties-service';
import { PartyDetailPage } from './party-detail-page';

type PartyDetailView = {
  form: FormGroup<{
    amount: FormControl<number | null>;
    bankAccountId: FormControl<string>;
    settledOnUtc: FormControl<string>;
  }>;
  balance: () => CurrentAccountBalance | null;
  timeline: () => PartyTimelineRow[];
  balanceStatus: () => 'loading' | 'ready' | 'error';
  timelineStatus: () => 'loading' | 'ready' | 'error';
  settleStatus: () => 'idle' | 'settling' | 'settled' | 'error';
  settleError: () => AppError | null;
  onSubmit: () => void;
};

const money = (value: number): Money => value as Money;

const balance: CurrentAccountBalance = {
  partyId: 'p1',
  name: 'Alice',
  balanceMinorUnits: money(250000)
};

const timelineRows: PartyTimelineRow[] = [{
    movementOnUtc: '2026-09-01T20:00:00.000Z',
    description: 'Dinner split',
    deltaMinorUnits: money(250000),
    runningBalanceMinorUnits: money(250000),
    currencyCode: 'ARS'
  }];

describe('PartyDetailPage', () => {
  let fixture: ComponentFixture<PartyDetailPage>;
  let view: PartyDetailView;
  let getBalance: jasmine.Spy<(id: string) => Observable<CurrentAccountBalance>>;
  let partyTimeline: jasmine.Spy<(id: string) => Observable<PartyTimelineRow[]>>;
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
      bankAccountId: 'acct-debit',
      settledOnUtc: '2026-09-15T10:30'
    });
  }

  const instruments: Instrument[] = [
    { id: 'acct-debit', type: 'debit', name: 'Checking', cutoffDate: null }
  ];

  beforeEach(() => {
    getBalance = jasmine.createSpy('getBalance').and.returnValue(of(balance));
    partyTimeline = jasmine.createSpy('partyTimeline').and.returnValue(of(timelineRows));
    settle = jasmine
      .createSpy('settle')
      .and.returnValue(of<SettlementResult>({ ledgerTransactionId: 'tx-1' }));
    TestBed.configureTestingModule({
      imports: [PartyDetailPage],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: PartiesService, useValue: { getBalance, settle } },
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
    expect(text()).toContain('Dinner split');
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
    expect(body.bankAccountId).toBe('acct-debit');
    expect(body.settledOnUtc).toBe(new Date('2026-09-15T10:30').toISOString());
    expect(view.settleStatus()).toBe('settled');
    expect(getBalance).toHaveBeenCalledTimes(1);
    expect(partyTimeline).toHaveBeenCalledTimes(1);
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

import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { AppError } from '../../../../core/types/app-error';
import { Money } from '../../../../core/types/money';
import { ReportsService } from '../../../reports/reports-service';
import { PartyDebtRow } from '../../../reports/types/party-debt-row';
import { CreateParty } from '../../types/create-party';
import { Party } from '../../types/party';
import { PartyResult } from '../../types/party-result';
import { PendingSharesByPartyRow } from '../../types/pending-shares-by-party-row';
import { PartiesService } from '../../parties-service';
import { PartiesPage } from './parties-page';

type PartyListRow = {
  partyId: string;
  partyName: string;
  netBalanceMinorUnits: Money;
  scheduledCount: number;
};

type PartiesView = {
  form: FormGroup<{ name: FormControl<string> }>;
  listStatus: () => 'loading' | 'ready' | 'error';
  submitStatus: () => 'idle' | 'submitting' | 'error';
  submitError: () => AppError | null;
  createdPartyId: () => string | null;
  parties: () => PartyListRow[];
  balanceHint: (row: PartyListRow) => string;
  onSubmit: () => void;
};

const roster: Party[] = [
  { id: 'p1', name: 'Alice' },
  { id: 'p2', name: 'Bob' }
];
const debtRows: PartyDebtRow[] = [
  { partyId: 'p1', partyName: 'Alice', netBalanceMinorUnits: 250000 as Money, currencyCode: 'ARS' }
];

describe('PartiesPage', () => {
  let fixture: ComponentFixture<PartiesPage>;
  let view: PartiesView;
  let list: jasmine.Spy<() => Observable<Party[]>>;
  let debtSummary: jasmine.Spy<() => Observable<PartyDebtRow[]>>;
  let pendingShares: jasmine.Spy<() => Observable<PendingSharesByPartyRow[]>>;
  let create: jasmine.Spy<(body: CreateParty) => Observable<PartyResult>>;

  function setup(): void {
    fixture = TestBed.createComponent(PartiesPage);
    view = fixture.componentInstance as unknown as PartiesView;
    fixture.detectChanges();
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  beforeEach(() => {
    list = jasmine.createSpy('list').and.returnValue(of<Party[]>(roster));
    debtSummary = jasmine.createSpy('debtSummary').and.returnValue(of<PartyDebtRow[]>(debtRows));
    pendingShares = jasmine.createSpy('pendingShares').and.returnValue(of<PendingSharesByPartyRow[]>([]));
    create = jasmine.createSpy('create').and.returnValue(of<PartyResult>({ id: 'p9' }));
    TestBed.configureTestingModule({
      imports: [PartiesPage],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: PartiesService, useValue: { list, create, pendingShares } },
        { provide: ReportsService, useValue: { debtSummary } }
      ]
    });
  });

  it('creates', () => {
    setup();
    expect(fixture.componentInstance).toBeTruthy();
  });
  it('lists every registered party, merging balances from the debt summary', () => {
    setup();
    expect(list).toHaveBeenCalledTimes(1);
    expect(debtSummary).toHaveBeenCalledTimes(1);
    expect(pendingShares).toHaveBeenCalledTimes(1);
    expect(view.listStatus()).toBe('ready');
    expect(view.parties()).toEqual([
      { partyId: 'p1', partyName: 'Alice', netBalanceMinorUnits: 250000 as Money, scheduledCount: 0 },
      { partyId: 'p2', partyName: 'Bob', netBalanceMinorUnits: 0 as Money, scheduledCount: 0 }
    ]);
    expect(text()).toContain('Alice');
    expect(text()).toContain('Bob');
  });
  it('renders a party with no balance and no schedule as settled at zero', () => {
    setup();
    const bob = view.parties().find((row) => row.partyId === 'p2') as PartyListRow;
    expect(bob.netBalanceMinorUnits).toBe(0 as Money);
    expect(bob.scheduledCount).toBe(0);
    expect(view.balanceHint(bob)).toBe('Settled up');
  });
  it('labels a $0 party with pending installment shares as scheduled, not settled', () => {
    pendingShares.and.returnValue(
      of<PendingSharesByPartyRow[]>([
        { partyId: 'p2', scheduledCount: 3, scheduledTotalMinorUnits: 450000 as Money, currencyCode: 'ARS' }
      ])
    );
    setup();
    const bob = view.parties().find((row) => row.partyId === 'p2') as PartyListRow;
    expect(bob.netBalanceMinorUnits).toBe(0 as Money);
    expect(bob.scheduledCount).toBe(3);
    expect(view.balanceHint(bob)).toBe('Nothing owed yet · 3 scheduled');
    expect(text()).toContain('3 scheduled');
  });
  it('keeps a party with a real posted balance on its owe hint even when installments are scheduled', () => {
    pendingShares.and.returnValue(
      of<PendingSharesByPartyRow[]>([
        { partyId: 'p1', scheduledCount: 2, scheduledTotalMinorUnits: 120000 as Money, currencyCode: 'ARS' }
      ])
    );
    setup();
    const alice = view.parties().find((row) => row.partyId === 'p1') as PartyListRow;
    expect(alice.scheduledCount).toBe(2);
    expect(view.balanceHint(alice)).toBe('They owe you');
  });
  it('shows the empty state when no party is registered', () => {
    list.and.returnValue(of<Party[]>([]));
    debtSummary.and.returnValue(of<PartyDebtRow[]>([]));
    setup();
    expect(text()).toContain('No parties yet.');
  });
  it('goes to the error state when the roster fails to load', () => {
    list.and.returnValue(throwError(() => new Error('boom')));
    setup();
    expect(view.listStatus()).toBe('error');
  });
  it('blocks submit while the name is missing', () => {
    setup();
    view.form.setValue({ name: '' });
    view.onSubmit();
    expect(create).not.toHaveBeenCalled();
  });
  it('trims the name, shows the confirmation and re-fetches the list', () => {
    setup();
    list.calls.reset();
    debtSummary.calls.reset();
    pendingShares.calls.reset();
    view.form.setValue({ name: '  Charlie  ' });
    view.onSubmit();
    expect(create).toHaveBeenCalledWith({ name: 'Charlie' });
    expect(view.createdPartyId()).toBe('p9');
    expect(list).toHaveBeenCalledTimes(1);
    expect(debtSummary).toHaveBeenCalledTimes(1);
    expect(pendingShares).toHaveBeenCalledTimes(1);
    expect(view.submitStatus()).toBe('idle');
  });
  it('renders submitErrorText keyed off the AppError code on a 422', () => {
    const appError: AppError = {
      code: 'Parties.InvalidName',
      title: 'Unprocessable entity',
      detail: 'x',
      status: 422,
      metadata: {}
    };
    create.and.returnValue(throwError(() => appError));
    setup();
    view.form.setValue({ name: 'Bob' });
    view.onSubmit();
    fixture.detectChanges();
    expect(view.submitStatus()).toBe('error');
    expect(view.submitError()).toEqual(appError);
    expect(text()).toContain('Enter a name for the party.');
  });
});

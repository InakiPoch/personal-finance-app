import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormArray, FormControl, FormGroup } from '@angular/forms';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { AppError } from '../../../../core/types/app-error';
import { Money } from '../../../../core/types/money';
import { InstrumentsService } from '../../../instruments/instruments-service';
import { Instrument } from '../../../instruments/types/instrument';
import { ReportsService } from '../../../reports/reports-service';
import { PartyDebtRow } from '../../../reports/types/party-debt-row';
import { RegisterSharedExpense } from '../../types/register-shared-expense';
import { SharedExpenseResult } from '../../types/shared-expense-result';
import { PartiesService } from '../../parties-service';
import { SharedExpensePage } from './shared-expense-page';

type ParticipantRow = FormGroup<{
  partyId: FormControl<string>;
  weight: FormControl<number | null>;
}>;

type SharedExpenseView = {
  form: FormGroup<{
    description: FormControl<string>;
    total: FormControl<number | null>;
    expenseAccountId: FormControl<string>;
    fundingAccountId: FormControl<string>;
    incurredOnUtc: FormControl<string>;
    participants: FormArray<ParticipantRow>;
  }>;
  partiesStatus: () => 'loading' | 'ready' | 'error';
  submitStatus: () => 'idle' | 'submitting' | 'confirmed' | 'error';
  submitError: () => AppError | null;
  splitReferenceId: () => string | null;
  prefilledPartyId: () => string | null;
  addParticipant: () => void;
  removeParticipant: (index: number) => void;
  onSubmit: () => void;
};

const debtRows: PartyDebtRow[] = [
  { partyId: 'p1', partyName: 'Alice', netBalanceMinorUnits: 0 as Money, currencyCode: 'ARS' },
  { partyId: 'p2', partyName: 'Bob', netBalanceMinorUnits: 0 as Money, currencyCode: 'ARS' }
];

const instruments: Instrument[] = [
  { id: 'acct-1', type: 'debit', name: 'Checking', cutoffDate: null }
];

describe('SharedExpensePage', () => {
  let fixture: ComponentFixture<SharedExpensePage>;
  let view: SharedExpenseView;
  let debtSummary: jasmine.Spy<() => Observable<PartyDebtRow[]>>;
  let registerSharedExpense: jasmine.Spy<(body: RegisterSharedExpense) => Observable<SharedExpenseResult>>;
  let queryParams: Record<string, string>;

  function setup(): void {
    TestBed.configureTestingModule({
      imports: [SharedExpensePage],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: PartiesService, useValue: { registerSharedExpense } },
        { provide: ReportsService, useValue: { debtSummary } },
        { provide: InstrumentsService, useValue: { list: () => of<Instrument[]>(instruments) } },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } }
        }
      ]
    });
    fixture = TestBed.createComponent(SharedExpensePage);
    view = fixture.componentInstance as unknown as SharedExpenseView;
    fixture.detectChanges();
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function fillHeaderFields(): void {
    view.form.patchValue({
      description: '  Dinner  ',
      total: 9000,
      expenseAccountId: '  exp-uuid  ',
      fundingAccountId: 'acct-1',
      incurredOnUtc: '2026-09-01T20:00'
    });
  }

  beforeEach(() => {
    queryParams = {};
    debtSummary = jasmine.createSpy('debtSummary').and.returnValue(of<PartyDebtRow[]>(debtRows));
    registerSharedExpense = jasmine
      .createSpy('registerSharedExpense')
      .and.returnValue(of<SharedExpenseResult>({ splitReferenceId: 'split-1' }));
  });

  it('loads the parties list on init and offers them as participant options', () => {
    setup();
    expect(debtSummary).toHaveBeenCalledTimes(1);
    expect(view.partiesStatus()).toBe('ready');
    view.addParticipant();
    fixture.detectChanges();
    expect(text()).toContain('Alice');
    expect(text()).toContain('Bob');
  });
  it('adds and removes participant rows', () => {
    setup();
    expect(view.form.controls.participants.length).toBe(0);
    view.addParticipant();
    view.addParticipant();
    expect(view.form.controls.participants.length).toBe(2);
    view.removeParticipant(0);
    expect(view.form.controls.participants.length).toBe(1);
  });
  it('prefills the first participant from the ?party= query param', () => {
    queryParams = { party: 'p2' };
    setup();
    expect(view.prefilledPartyId()).toBe('p2');
    expect(view.form.controls.participants.length).toBe(1);
    expect(view.form.controls.participants.at(0).controls.partyId.value).toBe('p2');
  });
  it('submits a minor-units, ISO-instant body with the participants array', () => {
    setup();
    view.addParticipant();
    view.form.controls.participants.at(0).setValue({ partyId: 'p1', weight: 2 });
    fillHeaderFields();
    view.onSubmit();
    expect(registerSharedExpense).toHaveBeenCalledTimes(1);
    const body: RegisterSharedExpense = registerSharedExpense.calls.mostRecent().args[0];
    expect(body).toEqual({
      description: 'Dinner',
      totalMinorUnits: 900000 as Money,
      expenseAccountId: 'exp-uuid',
      fundingAccountId: 'acct-1',
      incurredOnUtc: new Date('2026-09-01T20:00').toISOString(),
      participants: [{ partyId: 'p1', weight: 2 }]
    });
    expect(view.submitStatus()).toBe('confirmed');
    expect(view.splitReferenceId()).toBe('split-1');
  });
  it('renders submitErrorText keyed off the AppError code on a 422', () => {
    const appError: AppError = {
      code: 'Parties.InvalidParticipants',
      title: 'Unprocessable entity',
      detail: 'x',
      status: 422,
      metadata: {},
    };
    registerSharedExpense.and.returnValue(throwError(() => appError));
    setup();
    view.addParticipant();
    view.form.controls.participants.at(0).setValue({ partyId: 'p1', weight: 1 });
    fillHeaderFields();
    view.onSubmit();
    fixture.detectChanges();
    expect(view.submitStatus()).toBe('error');
    expect(view.submitError()).toEqual(appError);
    expect(text()).toContain('Add at least one participant with a positive weight.');
  });
});

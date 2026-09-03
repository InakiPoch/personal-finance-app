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
import { PartyResult } from '../../types/party-result';
import { PartiesService } from '../../parties-service';
import { PartiesPage } from './parties-page';

type PartiesView = {
  form: FormGroup<{ name: FormControl<string> }>;
  listStatus: () => 'loading' | 'ready' | 'error';
  submitStatus: () => 'idle' | 'submitting' | 'error';
  submitError: () => AppError | null;
  createdPartyId: () => string | null;
  parties: () => PartyDebtRow[];
  onSubmit: () => void;
};

const debtRow: PartyDebtRow = {
  partyId: 'p1',
  partyName: 'Alice',
  netBalanceMinorUnits: 250000 as Money,
  currencyCode: 'ARS'
};

describe('PartiesPage', () => {
  let fixture: ComponentFixture<PartiesPage>;
  let view: PartiesView;
  let debtSummary: jasmine.Spy<() => Observable<PartyDebtRow[]>>;
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
    debtSummary = jasmine.createSpy('debtSummary').and.returnValue(of<PartyDebtRow[]>([debtRow]));
    create = jasmine.createSpy('create').and.returnValue(of<PartyResult>({ id: 'p9' }));
    TestBed.configureTestingModule({
      imports: [PartiesPage],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: PartiesService, useValue: { create } },
        { provide: ReportsService, useValue: { debtSummary } }
      ]
    });
  });

  it('creates', () => {
    setup();
    expect(fixture.componentInstance).toBeTruthy();
  });
  it('loads and renders the debt summary on init', () => {
    setup();
    expect(debtSummary).toHaveBeenCalledTimes(1);
    expect(view.listStatus()).toBe('ready');
    expect(view.parties()).toEqual([debtRow]);
    expect(text()).toContain('Alice');
  });
  it('shows the empty state when no party has movements', () => {
    debtSummary.and.returnValue(of<PartyDebtRow[]>([]));
    setup();
    expect(text()).toContain('No parties with movements yet.');
  });
  it('blocks submit while the name is missing', () => {
    setup();
    view.form.setValue({ name: '' });
    view.onSubmit();
    expect(create).not.toHaveBeenCalled();
  });
  it('trims the name, shows the confirmation and re-fetches the list', () => {
    setup();
    debtSummary.calls.reset();
    view.form.setValue({ name: '  Bob  ' });
    view.onSubmit();
    expect(create).toHaveBeenCalledWith({ name: 'Bob' });
    expect(view.createdPartyId()).toBe('p9');
    expect(debtSummary).toHaveBeenCalledTimes(1);
    expect(view.submitStatus()).toBe('idle');
    fixture.detectChanges();
    expect(text()).toContain('New parties appear in the list');
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

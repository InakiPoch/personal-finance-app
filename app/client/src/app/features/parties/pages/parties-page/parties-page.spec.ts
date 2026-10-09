import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { formatMoney } from '../../../../core/money/money';
import { AppError } from '../../../../core/types/app-error';
import { Money } from '../../../../core/types/money';
import { CreateParty } from '../../types/create-party';
import { PartySummary } from '../../types/party-summary';
import { PartyResult } from '../../types/party-result';
import { PartiesService } from '../../parties-service';
import { PartiesPage } from './parties-page';

type PartiesView = {
  form: FormGroup<{ name: FormControl<string> }>;
  listStatus: () => 'loading' | 'ready' | 'error';
  submitStatus: () => 'idle' | 'submitting' | 'error';
  submitError: () => AppError | null;
  createdPartyId: () => string | null;
  parties: () => PartySummary[];
  balanceHint: (row: PartySummary) => string;
  onSubmit: () => void;
};

function summary(id: string, name: string, extra: Partial<PartySummary> = {}): PartySummary {
  return {
    id,
    name,
    owedToYou: [],
    youOwe: [],
    scheduledToYouCount: 0,
    scheduledYouOweCount: 0,
    settledUp: true,
    ...extra
  };
}

const alice: PartySummary = summary('p1', 'Alice', {
  owedToYou: [{ currencyCode: 'ARS', balanceMinorUnits: 250000 as Money }],
  settledUp: false
});
const bob: PartySummary = summary('p2', 'Bob');

describe('PartiesPage', () => {
  let fixture: ComponentFixture<PartiesPage>;
  let view: PartiesView;
  let listSummaries: jasmine.Spy<() => Observable<PartySummary[]>>;
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
    listSummaries = jasmine.createSpy('listSummaries').and.returnValue(of<PartySummary[]>([alice, bob]));
    create = jasmine.createSpy('create').and.returnValue(of<PartyResult>({ id: 'p9' }));
    TestBed.configureTestingModule({
      imports: [PartiesPage],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: PartiesService, useValue: { listSummaries, create } }
      ]
    });
  });

  it('creates', () => {
    setup();
    expect(fixture.componentInstance).toBeTruthy();
  });
  it('lists every registered party from a single request', () => {
    setup();
    expect(listSummaries).toHaveBeenCalledTimes(1);
    expect(view.listStatus()).toBe('ready');
    expect(view.parties()).toEqual([alice, bob]);
    expect(text()).toContain('Alice');
    expect(text()).toContain('Bob');
  });
  it('shows a party with nothing in either direction as settled up', () => {
    setup();
    expect(view.balanceHint(bob)).toBe('Settled up');
  });
  it('shows both sides for a party that owes you and that you owe, per currency, never netted', () => {
    const carla: PartySummary = summary('p3', 'Carla', {
      owedToYou: [{ currencyCode: 'ARS', balanceMinorUnits: 250000 as Money }],
      youOwe: [{ currencyCode: 'USD', balanceMinorUnits: 5000 as Money }],
      settledUp: false
    });
    listSummaries.and.returnValue(of<PartySummary[]>([carla]));
    setup();
    expect(text()).toContain('Owes you ' + formatMoney(250000 as Money, 'ARS'));
    expect(text()).toContain('You owe ' + formatMoney(5000 as Money, 'USD'));
    expect(view.balanceHint(carla)).toBe('');
  });
  it('never calls a party I owe money to settled up', () => {
    const dan: PartySummary = summary('p4', 'Dan', {
      youOwe: [{ currencyCode: 'ARS', balanceMinorUnits: 9000 as Money }],
      settledUp: false
    });
    listSummaries.and.returnValue(of<PartySummary[]>([dan]));
    setup();
    expect(view.balanceHint(dan)).not.toBe('Settled up');
    expect(text()).toContain('You owe ' + formatMoney(9000 as Money, 'ARS'));
  });
  it('labels a party with only installments I owe as scheduled, not settled', () => {
    const eve: PartySummary = summary('p5', 'Eve', { scheduledYouOweCount: 3, settledUp: false });
    listSummaries.and.returnValue(of<PartySummary[]>([eve]));
    setup();
    expect(view.balanceHint(eve)).toBe('Nothing owed yet · 3 scheduled');
    expect(text()).toContain('3 scheduled');
  });
  it('keeps the totals separated per currency and per side', () => {
    listSummaries.and.returnValue(
      of<PartySummary[]>([
        alice,
        summary('p2', 'Bob', {
          owedToYou: [{ currencyCode: 'USD', balanceMinorUnits: 5000 as Money }],
          youOwe: [{ currencyCode: 'ARS', balanceMinorUnits: 700 as Money }],
          settledUp: false
        })
      ])
    );
    setup();
    expect(text()).toContain(formatMoney(250000 as Money, 'ARS'));
    expect(text()).toContain(formatMoney(5000 as Money, 'USD'));
    expect(text()).toContain('You owe ' + formatMoney(700 as Money, 'ARS') + ' back.');
  });
  it('shows the empty state when no party is registered', () => {
    listSummaries.and.returnValue(of<PartySummary[]>([]));
    setup();
    expect(text()).toContain('No parties yet.');
  });
  it('goes to the error state when the roster fails to load', () => {
    listSummaries.and.returnValue(throwError(() => new Error('boom')));
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
    listSummaries.calls.reset();
    view.form.setValue({ name: '  Charlie  ' });
    view.onSubmit();
    expect(create).toHaveBeenCalledWith({ name: 'Charlie' });
    expect(view.createdPartyId()).toBe('p9');
    expect(listSummaries).toHaveBeenCalledTimes(1);
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

import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormArray, FormControl, FormGroup } from '@angular/forms';
import { provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { AppError } from '../../../../core/types/app-error';
import { CreateCreditor } from '../../types/create-creditor';
import { Creditor } from '../../types/creditor';
import { CreditorCreated } from '../../types/creditor-created';
import { CreditorsService } from '../../creditors-service';
import { CreditorsPage } from './creditors-page';

type AccountRow = FormGroup<{
  label: FormControl<string>;
  identifier: FormControl<string>;
}>;

type CreditorsView = {
  form: FormGroup<{
    name: FormControl<string>;
    accounts: FormArray<AccountRow>;
  }>;
  listStatus: () => 'loading' | 'ready' | 'error';
  submitStatus: () => 'idle' | 'submitting' | 'error';
  submitError: () => AppError | null;
  createdCreditorId: () => string | null;
  creditors: () => Creditor[];
  addAccountRow: () => void;
  removeAccountRow: (index: number) => void;
  onSubmit: () => void;
};

const juan: Creditor = {
  id: 'c1',
  name: 'Juan',
  accounts: [{ id: 'a1', label: 'Galicia', identifier: 'CBU1' }]
};

describe('CreditorsPage', () => {
  let fixture: ComponentFixture<CreditorsPage>;
  let view: CreditorsView;
  let list: jasmine.Spy<() => Observable<Creditor[]>>;
  let create: jasmine.Spy<(body: CreateCreditor) => Observable<CreditorCreated>>;

  function setup(): void {
    fixture = TestBed.createComponent(CreditorsPage);
    view = fixture.componentInstance as unknown as CreditorsView;
    fixture.detectChanges();
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  beforeEach(() => {
    list = jasmine.createSpy('list').and.returnValue(of<Creditor[]>([juan]));
    create = jasmine.createSpy('create').and.returnValue(of<CreditorCreated>({ creditorId: 'c9' }));
    TestBed.configureTestingModule({
      imports: [CreditorsPage],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: CreditorsService, useValue: { list, create } }
      ]
    });
  });

  it('creates', () => {
    setup();
    expect(fixture.componentInstance).toBeTruthy();
  });
  it('loads and renders the register on init', () => {
    setup();
    expect(list).toHaveBeenCalledTimes(1);
    expect(view.listStatus()).toBe('ready');
    expect(view.creditors()).toEqual([juan]);
    expect(text()).toContain('Galicia');
  });
  it('shows the empty state when no creditor is registered', () => {
    list.and.returnValue(of<Creditor[]>([]));
    setup();
    expect(text()).toContain('Nothing registered yet');
  });
  it('starts with a single blank account row and supports adding and removing rows', () => {
    setup();
    expect(view.form.controls.accounts.length).toBe(1);
    view.addAccountRow();
    expect(view.form.controls.accounts.length).toBe(2);
    view.removeAccountRow(0);
    expect(view.form.controls.accounts.length).toBe(1);
  });
  it('blocks submit while the default account row is left blank', () => {
    setup();
    view.form.controls.name.setValue('Juan');
    view.onSubmit();
    expect(create).not.toHaveBeenCalled();
  });
  it('allows submitting with zero accounts once the default row is removed', () => {
    setup();
    view.removeAccountRow(0);
    view.form.controls.name.setValue('Ana');
    view.onSubmit();
    expect(create).toHaveBeenCalledWith({ name: 'Ana', accounts: [] });
  });
  it('trims the name and account fields, then re-fetches the list', () => {
    setup();
    list.calls.reset();
    view.form.controls.name.setValue('  Juan  ');
    view.form.controls.accounts.at(0).patchValue({ label: '  Galicia  ', identifier: ' CBU1 ' });
    view.onSubmit();
    expect(create).toHaveBeenCalledWith({
      name: 'Juan',
      accounts: [{ label: 'Galicia', identifier: 'CBU1' }]
    });
    expect(view.createdCreditorId()).toBe('c9');
    expect(list).toHaveBeenCalledTimes(1);
    expect(view.submitStatus()).toBe('idle');
  });
  it('submits a row with a label but a blank identifier as identifier: null', () => {
    setup();
    view.form.controls.name.setValue('Ana');
    view.form.controls.accounts.at(0).patchValue({ label: 'Galicia', identifier: '   ' });
    view.onSubmit();
    expect(create).toHaveBeenCalledWith({
      name: 'Ana',
      accounts: [{ label: 'Galicia', identifier: null }]
    });
  });
  it('renders submitErrorText keyed off the AppError code on a 422', () => {
    const appError: AppError = {
      code: 'Financing.InvalidCreditorName',
      title: 'Unprocessable entity',
      detail: 'x',
      status: 422,
      metadata: {}
    };
    create.and.returnValue(throwError(() => appError));
    setup();
    view.form.controls.name.setValue('Juan');
    view.removeAccountRow(0);
    view.onSubmit();
    fixture.detectChanges();
    expect(view.submitStatus()).toBe('error');
    expect(view.submitError()).toEqual(appError);
    expect(text()).toContain('Enter a name for the creditor.');
  });
});

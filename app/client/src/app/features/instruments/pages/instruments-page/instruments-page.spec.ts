import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { Observable, of, throwError } from 'rxjs';
import { InstrumentRegistryService } from '../../../../core/registry/instrument-registry-service';
import { AppError } from '../../../../core/types/app-error';
import { InstrumentType } from '../../../../core/types/instrument-type';
import { InstrumentsService } from '../../instruments-service';
import { CreateInstrument } from '../../types/create-instrument';
import { InstrumentCreated } from '../../types/instrument-created';
import { InstrumentsPage } from './instruments-page';

type InstrumentsView = {
  form: FormGroup<{
    type: FormControl<InstrumentType>;
    name: FormControl<string>;
    cutoffDate: FormControl<number | null>;
  }>;
  submitStatus: () => 'idle' | 'submitting' | 'error';
  submitError: () => AppError | null;
  onSubmit: () => void;
};

describe('InstrumentsPage', () => {
  let fixture: ComponentFixture<InstrumentsPage>;
  let view: InstrumentsView;
  let registry: InstrumentRegistryService;
  let create: jasmine.Spy<(body: CreateInstrument) => Observable<InstrumentCreated>>;

  beforeEach(() => {
    localStorage.clear();
    create = jasmine
      .createSpy('create')
      .and.returnValue(of<InstrumentCreated>({ id: 'inst-1', type: 'credit' }));
    TestBed.configureTestingModule({
      imports: [InstrumentsPage],
      providers: [
        provideZonelessChangeDetection(),
        { provide: InstrumentsService, useValue: { create } },
      ],
    });
    fixture = TestBed.createComponent(InstrumentsPage);
    view = fixture.componentInstance as unknown as InstrumentsView;
    registry = TestBed.inject(InstrumentRegistryService);
    fixture.detectChanges();
  });

  afterEach(() => localStorage.clear());

  it('creates', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
  it('is invalid without a name', () => {
    view.form.setValue({ type: 'debit', name: '', cutoffDate: null });
    expect(view.form.valid).toBe(false);
    view.form.controls.name.setValue('Checking');
    expect(view.form.valid).toBe(true);
  });
  it('requires a 1–31 cutoff day only for credit instruments', () => {
    view.form.setValue({ type: 'cash', name: 'Wallet', cutoffDate: null });
    expect(view.form.valid).toBe(true);
    view.form.controls.type.setValue('credit');
    expect(view.form.controls.cutoffDate.hasError('creditCutoff')).toBe(true);
    expect(view.form.valid).toBe(false);
    view.form.controls.cutoffDate.setValue(40);
    expect(view.form.valid).toBe(false);
    view.form.controls.cutoffDate.setValue(15);
    expect(view.form.controls.cutoffDate.hasError('creditCutoff')).toBe(false);
    expect(view.form.valid).toBe(true);
  });
  it('submits the instrument, trims the name and appends it to the registry', () => {
    view.form.setValue({ type: 'credit', name: '  Visa  ', cutoffDate: 20 });
    view.onSubmit();
    expect(create).toHaveBeenCalledWith({ type: 'credit', name: 'Visa', cutoffDate: 20 });
    expect(registry.instruments()).toEqual([
      { id: 'inst-1', type: 'credit', name: 'Visa', cutoffDate: 20 },
    ]);
    expect(view.form.controls.name.value).toBe('');
    expect(view.submitStatus()).toBe('idle');
  });
  it('omits cutoffDate from the payload for non-credit instruments', () => {
    create.and.returnValue(of<InstrumentCreated>({ id: 'inst-2', type: 'debit' }));
    view.form.setValue({ type: 'debit', name: 'Checking', cutoffDate: null });
    view.onSubmit();
    expect(create).toHaveBeenCalledWith({ type: 'debit', name: 'Checking' });
    expect(registry.instruments()).toEqual([{ id: 'inst-2', type: 'debit', name: 'Checking' }]);
  });

  it('surfaces an AppError and does not touch the registry when create fails', () => {
    const appError: AppError = {
      code: 'Instruments.UnknownType',
      title: 'Bad request',
      detail: 'x',
      status: 400,
      metadata: {},
    };
    create.and.returnValue(throwError(() => appError));
    view.form.setValue({ type: 'debit', name: 'Checking', cutoffDate: null });
    view.onSubmit();
    expect(view.submitError()).toEqual(appError);
    expect(view.submitStatus()).toBe('error');
    expect(registry.instruments()).toEqual([]);
  });
});

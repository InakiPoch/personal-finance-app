import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { Observable, of, throwError } from 'rxjs';
import { AppError } from '../../../../core/types/app-error';
import { InstrumentType } from '../../../../core/types/instrument-type';
import { InstrumentsService } from '../../instruments-service';
import { CreateInstrument } from '../../types/create-instrument';
import { Instrument } from '../../types/instrument';
import { InstrumentCreated } from '../../types/instrument-created';
import { InstrumentsPage } from './instruments-page';

type InstrumentsView = {
  form: FormGroup<{
    type: FormControl<InstrumentType>;
    name: FormControl<string>;
    cutoffDate: FormControl<number | null>;
  }>;
  instruments: () => Instrument[];
  submitStatus: () => 'idle' | 'submitting' | 'error';
  submitError: () => AppError | null;
  onSubmit: () => void;
};

describe('InstrumentsPage', () => {
  let fixture: ComponentFixture<InstrumentsPage>;
  let view: InstrumentsView;
  let create: jasmine.Spy<(body: CreateInstrument) => Observable<InstrumentCreated>>;
  let list: jasmine.Spy<() => Observable<Instrument[]>>;

  const visa: Instrument = { id: 'inst-1', type: 'credit', name: 'Visa', cutoffDate: 20 };

  beforeEach(() => {
    create = jasmine
      .createSpy('create')
      .and.returnValue(of<InstrumentCreated>({ id: 'inst-1', type: 'credit' }));
    list = jasmine.createSpy('list').and.returnValue(of<Instrument[]>([]));
    TestBed.configureTestingModule({
      imports: [InstrumentsPage],
      providers: [
        provideZonelessChangeDetection(),
        { provide: InstrumentsService, useValue: { create, list } },
      ],
    });
    fixture = TestBed.createComponent(InstrumentsPage);
    view = fixture.componentInstance as unknown as InstrumentsView;
    fixture.detectChanges();
  });

  it('creates', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
  it('loads the instrument list from the API on init', () => {
    expect(list).toHaveBeenCalledTimes(1);
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
  it('submits the instrument, trims the name and re-fetches the list from the API', () => {
    list.calls.reset();
    list.and.returnValue(of<Instrument[]>([visa]));
    view.form.setValue({ type: 'credit', name: '  Visa  ', cutoffDate: 20 });
    view.onSubmit();
    expect(create).toHaveBeenCalledWith({ type: 'credit', name: 'Visa', cutoffDate: 20 });
    expect(list).toHaveBeenCalledTimes(1);
    expect(view.instruments()).toEqual([visa]);
    expect(view.form.controls.name.value).toBe('');
    expect(view.submitStatus()).toBe('idle');
  });
  it('omits cutoffDate from the payload for non-credit instruments', () => {
    create.and.returnValue(of<InstrumentCreated>({ id: 'inst-2', type: 'debit' }));
    list.calls.reset();
    view.form.setValue({ type: 'debit', name: 'Checking', cutoffDate: null });
    view.onSubmit();
    expect(create).toHaveBeenCalledWith({ type: 'debit', name: 'Checking' });
    expect(list).toHaveBeenCalledTimes(1);
  });
  it('surfaces an AppError and does not re-fetch when create fails', () => {
    const appError: AppError = {
      code: 'Instruments.UnknownType',
      title: 'Bad request',
      detail: 'x',
      status: 400,
      metadata: {},
    };
    create.and.returnValue(throwError(() => appError));
    list.calls.reset();
    view.form.setValue({ type: 'debit', name: 'Checking', cutoffDate: null });
    view.onSubmit();
    expect(view.submitError()).toEqual(appError);
    expect(view.submitStatus()).toBe('error');
    expect(list).not.toHaveBeenCalled();
  });
});

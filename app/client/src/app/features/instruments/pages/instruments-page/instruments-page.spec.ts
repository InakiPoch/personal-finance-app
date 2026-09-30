import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { By } from '@angular/platform-browser';
import { Observable, of, throwError } from 'rxjs';
import { AppError } from '../../../../core/types/app-error';
import { InstrumentType } from '../../../../core/types/instrument-type';
import { CardClosingSchedule } from '../../components/card-closing-schedule/card-closing-schedule';
import { InstrumentsService } from '../../instruments-service';
import { ClosingScheduleRow } from '../../types/closing-schedule-row';
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
  let closingSchedule: jasmine.Spy<(id: string) => Observable<ClosingScheduleRow[]>>;
  let setClosingDate: jasmine.Spy<
    (id: string, year: number, month: number, day: number) => Observable<void>
  >;
  let clearClosingDate: jasmine.Spy<(id: string, year: number, month: number) => Observable<void>>;

  const visa: Instrument = { id: 'inst-1', type: 'credit', name: 'Visa', cutoffDate: 20, nextClosingDate: '2026-10-20' };

  const debit: Instrument = { id: 'inst-2', type: 'debit', name: 'Checking', cutoffDate: null, nextClosingDate: null };
  const schedule: ClosingScheduleRow[] = [
    { year: 2026, month: 10, closingDate: '2026-10-24', isOverride: true, isLocked: false },
  ];

  beforeEach(() => {
    create = jasmine
      .createSpy('create')
      .and.returnValue(of<InstrumentCreated>({ id: 'inst-1', type: 'credit' }));
    list = jasmine.createSpy('list').and.returnValue(of<Instrument[]>([]));
    closingSchedule = jasmine.createSpy('closingSchedule').and.returnValue(of(schedule));
    setClosingDate = jasmine.createSpy('setClosingDate').and.returnValue(of(undefined));
    clearClosingDate = jasmine.createSpy('clearClosingDate').and.returnValue(of(undefined));
    TestBed.configureTestingModule({
      imports: [InstrumentsPage],
      providers: [
        provideZonelessChangeDetection(),
        { provide: InstrumentsService, useValue: {
            create,
            list,
            closingSchedule,
            setClosingDate,
            clearClosingDate,
          },
        },
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

  describe('closing dates', () => {
    let host: HTMLElement;

    function boot(rows: Instrument[]): void {
      list.and.returnValue(of(rows));
      fixture = TestBed.createComponent(InstrumentsPage);
      fixture.detectChanges();
      host = fixture.nativeElement as HTMLElement;
    }

    function child(): CardClosingSchedule {
      return fixture.debugElement.query(By.directive(CardClosingSchedule)).componentInstance;
    }

    function failWith(code: string, status: number): void {
      const error: AppError = { code, title: 't', detail: 'd', status, metadata: {} };
      setClosingDate.and.returnValue(throwError(() => error));
    }

    it('renders the next closing date instead of the old cutoff', () => {
      boot([visa, debit]);
      expect(host.textContent).toContain('Next closing: Oct 20');
      expect(host.textContent).not.toContain('closes 20');
    });
    it('shows a dash when a card has no next closing', () => {
      boot([{ ...visa, nextClosingDate: null }]);
      expect(host.textContent).toContain('Next closing: —');
    });
    it('labels the create field "Usual closing day"', () => {
      boot([]);
      (host.querySelectorAll('input[type="radio"]')[1] as HTMLInputElement).click();
      fixture.detectChanges();
      expect(host.querySelector('label[for="cutoffDate"]')?.textContent?.trim()).toBe('Usual closing day');
    });
    it('requests the closing schedule for credit cards only', () => {
      boot([visa, debit]);
      expect(closingSchedule).toHaveBeenCalledTimes(1);
      expect(closingSchedule).toHaveBeenCalledWith('inst-1');
    });
    it('sets a month override then reloads', () => {
      boot([visa]);
      list.calls.reset();
      closingSchedule.calls.reset();
      child().closingSet.emit({ year: 2026, month: 10, day: 24 });
      expect(setClosingDate).toHaveBeenCalledWith('inst-1', 2026, 10, 24);
      expect(list).toHaveBeenCalledTimes(1);
      expect(closingSchedule).toHaveBeenCalledOnceWith('inst-1');
    });
    it('resets a month override then reloads', () => {
      boot([visa]);
      list.calls.reset();
      closingSchedule.calls.reset();
      child().closingReset.emit({ year: 2026, month: 10 });
      expect(clearClosingDate).toHaveBeenCalledWith('inst-1', 2026, 10);
      expect(list).toHaveBeenCalledTimes(1);
      expect(closingSchedule).toHaveBeenCalledOnceWith('inst-1');
    });

    const cases: [string, number, string][] = [
      [
        'Financing.ClosingChangeMovesChargedPurchase',
        409,
        "Some purchases in that month are already on a card bill, so the closing date can't move them. Set this month's closing date only.",
      ],
      [
        'Financing.ClosingMonthLocked',
        409,
        "That month's card bill is already out; its closing date can't change.",
      ],
      ['Financing.InvalidClosingDay', 422, 'Pick a valid closing day for that month.'],
      ['Http.ServerError', 500, 'The closing date could not be saved.'],
    ];
    cases.forEach(([code, status, sentence]: [string, number, string]) => {
      it(`maps ${code} (${status}) to its sentence and skips the reload`, () => {
        boot([visa]);
        list.calls.reset();
        failWith(code, status);
        child().closingSet.emit({ year: 2026, month: 10, day: 24 });
        fixture.detectChanges();
        expect(host.querySelector('[role="alert"]')?.textContent?.trim()).toBe(sentence);
        expect(list).not.toHaveBeenCalled();
      });
    });
  });
});

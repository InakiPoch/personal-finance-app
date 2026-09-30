import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ClosingScheduleRow } from '../../types/closing-schedule-row';
import { CardClosingSchedule } from './card-closing-schedule';

describe('CardClosingSchedule', () => {
  let fixture: ComponentFixture<CardClosingSchedule>;
  let host: HTMLElement;

  const schedule: ClosingScheduleRow[] = [
    { year: 2026, month: 10, closingDate: '2026-10-24', isOverride: true, isLocked: false },
    { year: 2026, month: 11, closingDate: '2026-11-20', isOverride: false, isLocked: false },
  ];

  function init(inputs: Record<string, unknown> = {}): void {
    fixture = TestBed.createComponent(CardClosingSchedule);
    fixture.componentRef.setInput('cardId', 'card-1');
    fixture.componentRef.setInput('cardName', 'Visa');
    fixture.componentRef.setInput('schedule', schedule);
    Object.entries(inputs).forEach(([key, value]: [string, unknown]) =>
      fixture.componentRef.setInput(key, value),
    );
    fixture.detectChanges();
    host = fixture.nativeElement as HTMLElement;
  }

  function button(label: string): HTMLButtonElement {
    return host.querySelector(`button[aria-label="${label}"]`) as HTMLButtonElement;
  }

  function saveButton(): HTMLButtonElement {
    return Array.from(host.querySelectorAll('button')).find(
      (b: HTMLButtonElement) => b.textContent?.trim() === 'Save',
    ) as HTMLButtonElement;
  }

  function type(input: HTMLInputElement, value: string): void {
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CardClosingSchedule],
      providers: [provideZonelessChangeDetection()],
    });
  });

  it('renders each formatted closing date', () => {
    init();
    const text: string = host.textContent ?? '';
    expect(text).toContain('Oct 24');
    expect(text).toContain('Nov 20');
  });
  it('tags and offers reset only for overrides', () => {
    init();
    expect(host.querySelectorAll('li')[0].textContent).toContain('(set)');
    expect(host.querySelectorAll('li')[1].textContent).not.toContain('(set)');
    expect(button('Reset closing date Oct 24 for Visa')).toBeTruthy();
    expect(button('Reset closing date Nov 20 for Visa')).toBeNull();
  });
  it('emits closingSet with year, month and day when a row date is edited', () => {
    init();
    const emitted: { year: number; month: number; day: number }[] = [];
    fixture.componentInstance.closingSet.subscribe((c) => emitted.push(c));
    button('Edit closing date Nov 20 for Visa').click();
    fixture.detectChanges();
    type(host.querySelector('input[type="date"]') as HTMLInputElement, '2026-11-22');
    saveButton().click();
    expect(emitted).toEqual([{ year: 2026, month: 11, day: 22 }]);
  });
  it('bounds the date input to that month', () => {
    init();
    button('Edit closing date Nov 20 for Visa').click();
    fixture.detectChanges();
    const input = host.querySelector('input[type="date"]') as HTMLInputElement;
    expect(input.min).toBe('2026-11-01');
    expect(input.max).toBe('2026-11-30');
  });
  it('does not emit a date outside the month', () => {
    init();
    const emitted: unknown[] = [];
    fixture.componentInstance.closingSet.subscribe((c) => emitted.push(c));
    button('Edit closing date Nov 20 for Visa').click();
    fixture.detectChanges();
    type(host.querySelector('input[type="date"]') as HTMLInputElement, '2026-12-01');
    saveButton().click();
    expect(emitted).toEqual([]);
  });
  it('emits closingReset for an override', () => {
    init();
    const emitted: { year: number; month: number }[] = [];
    fixture.componentInstance.closingReset.subscribe((c) => emitted.push(c));
    button('Reset closing date Oct 24 for Visa').click();
    expect(emitted).toEqual([{ year: 2026, month: 10 }]);
  });
  it('header Edit edits the first schedule row and emits closingSet for it', () => {
    init();
    const emitted: { year: number; month: number; day: number }[] = [];
    fixture.componentInstance.closingSet.subscribe((c) => emitted.push(c));
    button('Edit next closing date for Visa').click();
    fixture.detectChanges();
    const inputs: NodeListOf<HTMLInputElement> = host.querySelectorAll('input[type="date"]');
    expect(inputs.length).toBe(1);
    expect(inputs[0].min).toBe('2026-10-01');
    expect(inputs[0].max).toBe('2026-10-31');
    type(inputs[0], '2026-10-26');
    saveButton().click();
    fixture.detectChanges();
    expect(emitted).toEqual([{ year: 2026, month: 10, day: 26 }]);
    expect(host.querySelector('input[type="date"]')).toBeNull();
  });
  it('header Cancel restores the next closing text', () => {
    init({ nextClosingText: 'Oct 24' });
    button('Edit next closing date for Visa').click();
    fixture.detectChanges();
    expect(host.textContent).not.toContain('Next closing: Oct 24');
    button('Cancel editing next closing date for Visa').click();
    fixture.detectChanges();
    expect(host.textContent).toContain('Next closing: Oct 24');
  });
  it('hides the header Edit when the schedule is empty', () => {
    init({ schedule: [], nextClosingText: '—' });
    expect(button('Edit next closing date for Visa')).toBeNull();
    expect(host.textContent).toContain('Next closing: —');
  });
  it('renders the type label in the header', () => {
    init({ typeLabel: 'Credit card' });
    expect(host.textContent).toContain('Credit card');
  });
  it('renders the schedule inside details only when non-empty', () => {
    init();
    expect(host.querySelector('details')?.contains(button('Edit closing date Oct 24 for Visa'))).toBe(true);
    init({ schedule: [] });
    expect(host.querySelector('details')).toBeNull();
  });
  it('renders errorText in an alert', () => {
    init({ errorText: 'Nope.' });
    expect(host.querySelector('[role="alert"]')?.textContent?.trim()).toBe('Nope.');
  });
  it('disables actions while busy', () => {
    init({ busy: true });
    expect(button('Edit closing date Oct 24 for Visa').disabled).toBe(true);
    expect(button('Reset closing date Oct 24 for Visa').disabled).toBe(true);
  });
});

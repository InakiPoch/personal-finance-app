import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Money } from '../../../../core/types/money';
import { PayCreditorInstallment } from '../../types/pay-creditor-installment';
import { CreditorPayDialog } from './creditor-pay-dialog';

describe('CreditorPayDialog', () => {
  let fixture: ComponentFixture<CreditorPayDialog>;

  const money = (value: number): Money => value as Money;

  beforeEach(() => {
    if(!HTMLDialogElement.prototype.showModal) {
      spyOn(HTMLDialogElement.prototype, 'showModal').and.callFake(function(this: HTMLDialogElement): void {
        this.setAttribute('open', '');
      });
      spyOn(HTMLDialogElement.prototype, 'close').and.callFake(function(this: HTMLDialogElement): void {
        this.removeAttribute('open');
      });
    }
    TestBed.configureTestingModule({
      imports: [CreditorPayDialog],
      providers: [provideZonelessChangeDetection()]
    });
    fixture = TestBed.createComponent(CreditorPayDialog);
    fixture.componentRef.setInput('open', true);
    fixture.componentRef.setInput('title', 'Cuota 1/3 · Sofa');
    fixture.componentRef.setInput('remainingMinorUnits', money(400000));
    fixture.componentRef.setInput('currency', 'ARS');
    fixture.detectChanges();
  });

  function radioByValue(value: string): HTMLInputElement {
    return fixture.nativeElement.querySelector(`input[type="radio"][value="${value}"]`);
  }

  function amountInput(): HTMLInputElement {
    return fixture.nativeElement.querySelector('#pay-dialog-amount');
  }

  function confirmButton(): HTMLButtonElement {
    return fixture.nativeElement.querySelector('button[type="submit"]');
  }

  function cancelButton(): HTMLButtonElement {
    const all: NodeListOf<HTMLButtonElement> = fixture.nativeElement.querySelectorAll('button');
    return Array.from(all).find((button: HTMLButtonElement) => button.textContent?.trim() === 'Cancel') as HTMLButtonElement;
  }

  it('emits confirm with a null amount when "Pay in full" is submitted', () => {
    let emitted: PayCreditorInstallment | undefined;
    fixture.componentInstance.confirm.subscribe((body: PayCreditorInstallment) => (emitted = body));
    confirmButton().click();
    expect(emitted).toEqual({ amountMinorUnits: null });
  });

  it('emits confirm with the minor-unit amount when a custom amount is submitted', () => {
    radioByValue('custom').click();
    fixture.detectChanges();
    const input: HTMLInputElement = amountInput();
    input.value = '150.50';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    let emitted: PayCreditorInstallment | undefined;
    fixture.componentInstance.confirm.subscribe((body: PayCreditorInstallment) => (emitted = body));
    confirmButton().click();
    expect(emitted).toEqual({ amountMinorUnits: money(15050) });
  });

  it('disables Confirm and shows an error for a custom amount over the remaining balance', () => {
    radioByValue('custom').click();
    fixture.detectChanges();
    const input: HTMLInputElement = amountInput();
    input.value = '500000';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(confirmButton().disabled).toBe(true);
    expect(fixture.nativeElement.textContent).toContain("Can't exceed");
  });

  it('disables Confirm for a zero custom amount', () => {
    radioByValue('custom').click();
    fixture.detectChanges();
    const input: HTMLInputElement = amountInput();
    input.value = '0';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(confirmButton().disabled).toBe(true);
  });

  it('disables Confirm for a custom amount with three decimal places', () => {
    radioByValue('custom').click();
    fixture.detectChanges();
    const input: HTMLInputElement = amountInput();
    input.value = '10.123';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(confirmButton().disabled).toBe(true);
  });

  it('emits cancel when the native dialog fires its cancel event (Esc)', () => {
    let cancelled: boolean = false;
    fixture.componentInstance.cancel.subscribe(() => (cancelled = true));
    const dialog: HTMLDialogElement = fixture.nativeElement.querySelector('dialog');
    dialog.dispatchEvent(new Event('cancel'));
    expect(cancelled).toBe(true);
  });

  it('emits cancel when the Cancel button is clicked', () => {
    let cancelled: boolean = false;
    fixture.componentInstance.cancel.subscribe(() => (cancelled = true));
    cancelButton().click();
    expect(cancelled).toBe(true);
  });

  it('swaps to expense-mode copy and adds the waterfall hint under the custom amount', () => {
    fixture.componentRef.setInput('mode', 'expense');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Pay the whole expense');
    expect(fixture.nativeElement.textContent).toContain('Pay part of it');
    radioByValue('custom').click();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Fills cuotas in order; the last one may be partly paid.');
  });

  it('keeps installment-mode copy by default', () => {
    expect(fixture.nativeElement.textContent).toContain('Pay in full');
    expect(fixture.nativeElement.textContent).toContain('Pay a custom amount');
    radioByValue('custom').click();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Fills cuotas in order');
  });
});

import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Money } from '../../../../core/types/money';
import { Instrument } from '../../../instruments/types/instrument';
import { CreditorPayDialogConfirm } from '../../types/creditor-pay-dialog-confirm';
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
    fixture.componentRef.setInput('title', 'Installment 1/3 · Sofa');
    fixture.componentRef.setInput('remainingMinorUnits', money(400000));
    fixture.componentRef.setInput('currency', 'ARS');
    fixture.detectChanges();
  });

  function radioByValue(value: string): HTMLInputElement {
    return fixture.nativeElement.querySelector(`input[type="radio"][value="${value}"]`);
  }

  function partyRadio(partyName: string): HTMLInputElement {
    const labels: NodeListOf<HTMLLabelElement> = fixture.nativeElement.querySelectorAll('label');
    const label: HTMLLabelElement = Array.from(labels).find((candidate: HTMLLabelElement) =>
      candidate.textContent?.includes(`Pay ${partyName}'s part`)
    ) as HTMLLabelElement;
    return label.querySelector('input[type="radio"]') as HTMLInputElement;
  }

  function bankAccountSelect(): HTMLSelectElement | null {
    return fixture.nativeElement.querySelector('#pay-dialog-bank-account');
  }

  function amountInput(): HTMLInputElement {
    return fixture.nativeElement.querySelector('#pay-dialog-amount');
  }

  function currencySelect(): HTMLSelectElement | null {
    return fixture.nativeElement.querySelector('#pay-dialog-currency');
  }

  function confirmButton(): HTMLButtonElement {
    return fixture.nativeElement.querySelector('button[type="submit"]');
  }

  function cancelButton(): HTMLButtonElement {
    const all: NodeListOf<HTMLButtonElement> = fixture.nativeElement.querySelectorAll('button');
    return Array.from(all).find((button: HTMLButtonElement) => button.textContent?.trim() === 'Cancel') as HTMLButtonElement;
  }

  it('emits confirm with a null amount when "Pay in full" is submitted', () => {
    let emitted: CreditorPayDialogConfirm | undefined;
    fixture.componentInstance.confirm.subscribe((body: CreditorPayDialogConfirm) => (emitted = body));
    confirmButton().click();
    expect(emitted).toEqual({ kind: 'own', amountMinorUnits: null, currencyCode: null });
  });
  it('emits confirm with the minor-unit amount when a custom amount is submitted', () => {
    radioByValue('custom').click();
    fixture.detectChanges();
    const input: HTMLInputElement = amountInput();
    input.value = '150.50';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    let emitted: CreditorPayDialogConfirm | undefined;
    fixture.componentInstance.confirm.subscribe((body: CreditorPayDialogConfirm) => (emitted = body));
    confirmButton().click();
    expect(emitted).toEqual({ kind: 'own', amountMinorUnits: money(15050), currencyCode: null });
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
    expect(fixture.nativeElement.textContent).toContain('Fills installments in order; the last one may be partly paid.');
  });
  it('keeps installment-mode copy by default', () => {
    expect(fixture.nativeElement.textContent).toContain('Pay in full');
    expect(fixture.nativeElement.textContent).toContain('Pay a custom amount');
    radioByValue('custom').click();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Fills installments in order');
  });
  it('shows the full-debt hint and hides the currency select with a single currency', () => {
    fixture.componentRef.setInput('mode', 'full-debt');
    fixture.componentRef.setInput('currencies', [{ currencyCode: 'ARS', outstandingMinorUnits: money(200000) }]);
    fixture.detectChanges();
    radioByValue('custom').click();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Fills the oldest installments first, across all purchases.');
    expect(currencySelect()).toBeNull();
  });
  it('shows the currency select in full-debt mode with two currencies, listing every total in "Pay in full"', () => {
    fixture.componentRef.setInput('mode', 'full-debt');
    fixture.componentRef.setInput('currencies', [
      { currencyCode: 'ARS', outstandingMinorUnits: money(200000) },
      { currencyCode: 'USD', outstandingMinorUnits: money(50000) }
    ]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain(' + ');
    radioByValue('custom').click();
    fixture.detectChanges();
    expect(currencySelect()).toBeTruthy();
  });
  it('switching currency in full-debt mode changes the max used to validate the custom amount', () => {
    fixture.componentRef.setInput('mode', 'full-debt');
    fixture.componentRef.setInput('currencies', [
      { currencyCode: 'ARS', outstandingMinorUnits: money(200000) },
      { currencyCode: 'USD', outstandingMinorUnits: money(50000) }
    ]);
    fixture.detectChanges();
    radioByValue('custom').click();
    fixture.detectChanges();
    const input: HTMLInputElement = amountInput();
    input.value = '600';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(confirmButton().disabled).toBe(false);
    const select: HTMLSelectElement = currencySelect()!;
    select.value = 'USD';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(confirmButton().disabled).toBe(true);
    expect(fixture.nativeElement.textContent).toContain("Can't exceed");
  });
  it('emits confirm with the selected currency in full-debt custom mode', () => {
    fixture.componentRef.setInput('mode', 'full-debt');
    fixture.componentRef.setInput('currencies', [
      { currencyCode: 'ARS', outstandingMinorUnits: money(200000) },
      { currencyCode: 'USD', outstandingMinorUnits: money(50000) }
    ]);
    fixture.detectChanges();
    radioByValue('custom').click();
    fixture.detectChanges();
    const select: HTMLSelectElement = currencySelect()!;
    select.value = 'USD';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    const input: HTMLInputElement = amountInput();
    input.value = '100';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    let emitted: CreditorPayDialogConfirm | undefined;
    fixture.componentInstance.confirm.subscribe((body: CreditorPayDialogConfirm) => (emitted = body));
    confirmButton().click();
    expect(emitted).toEqual({ kind: 'own', amountMinorUnits: money(10000), currencyCode: 'USD' });
  });
  it('renders a radio per unpaid party share with its amount', () => {
    fixture.componentRef.setInput('partyShares', [
      { partyId: 'party-1', partyName: 'Nora', shareMinorUnits: money(200000), isPaid: false },
      { partyId: 'party-2', partyName: 'Omar', shareMinorUnits: money(150000), isPaid: false }
    ]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain("Pay Nora's part");
    expect(fixture.nativeElement.textContent).toContain("Pay Omar's part");
    expect(partyRadio('Nora')).toBeTruthy();
    expect(partyRadio('Omar')).toBeTruthy();
  });
  it('disables a party radio and explains why when the share exceeds what remains', () => {
    fixture.componentRef.setInput('remainingMinorUnits', money(100000));
    fixture.componentRef.setInput('partyShares', [
      { partyId: 'party-1', partyName: 'Nora', shareMinorUnits: money(200000), isPaid: false }
    ]);
    fixture.detectChanges();
    const radio: HTMLInputElement = partyRadio('Nora');
    expect(radio.getAttribute('aria-disabled')).toBe('true');
    expect(fixture.nativeElement.textContent).toContain('is more than the');
    radio.click();
    fixture.detectChanges();
    expect(bankAccountSelect()).toBeNull();
  });
  it('requires a bank account once a party share is selected, and disables Confirm until one is chosen', () => {
    fixture.componentRef.setInput('partyShares', [
      { partyId: 'party-1', partyName: 'Nora', shareMinorUnits: money(200000), isPaid: false }
    ]);
    const bankAccounts: Instrument[] = [{ id: 'bank-1', type: 'debit', name: 'Galicia', cutoffDate: null, nextClosingDate: null }];
    fixture.componentRef.setInput('bankAccounts', bankAccounts);
    fixture.detectChanges();
    expect(bankAccountSelect()).toBeNull();
    partyRadio('Nora').click();
    fixture.detectChanges();
    expect(bankAccountSelect()).toBeTruthy();
    expect(confirmButton().disabled).toBe(true);
    const select: HTMLSelectElement = bankAccountSelect()!;
    select.value = 'bank-1';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(confirmButton().disabled).toBe(false);
  });
  it('emits the party union with the chosen bank account when a party share is confirmed', () => {
    fixture.componentRef.setInput('partyShares', [
      { partyId: 'party-1', partyName: 'Nora', shareMinorUnits: money(200000), isPaid: false }
    ]);
    const bankAccounts: Instrument[] = [{ id: 'bank-1', type: 'debit', name: 'Galicia', cutoffDate: null, nextClosingDate: null }];
    fixture.componentRef.setInput('bankAccounts', bankAccounts);
    fixture.detectChanges();
    partyRadio('Nora').click();
    fixture.detectChanges();
    const select: HTMLSelectElement = bankAccountSelect()!;
    select.value = 'bank-1';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    let emitted: CreditorPayDialogConfirm | undefined;
    fixture.componentInstance.confirm.subscribe((body: CreditorPayDialogConfirm) => (emitted = body));
    confirmButton().click();
    expect(emitted).toEqual({ kind: 'party', partyId: 'party-1', bankAccountId: 'bank-1' });
  });
});

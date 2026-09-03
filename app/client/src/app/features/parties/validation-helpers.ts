import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export const positiveAmount: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value: unknown = control.value;
  const valid: boolean = typeof value === 'number' && Number.isFinite(value) && value > 0;
  return valid ? null : { positiveAmount: true };
};

export const atMostTwoDecimals: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value: unknown = control.value;
  if(typeof value !== 'number' || !Number.isFinite(value)) {
    return null;
  }
  const text: string = Math.abs(value).toString();
  if(text.includes('e')) {
    return { atMostTwoDecimals: true };
  }
  const fraction: string = text.split('.')[1] ?? '';
  return fraction.length > 2 ? { atMostTwoDecimals: true } : null;
};

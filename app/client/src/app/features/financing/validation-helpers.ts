import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

const ISO_DATE: RegExp = /^\d{4}-\d{2}-\d{2}$/;

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

export const positiveInteger: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value: unknown = control.value;
  const valid: boolean = typeof value === 'number' && Number.isInteger(value) && value >= 1;
  return valid ? null : { positiveInteger: true };
};

export const isoDate: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value: unknown = control.value;
  return typeof value === 'string' && ISO_DATE.test(value) ? null : { isoDate: true };
};

export const notFuture: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value: unknown = control.value;
  if(typeof value !== 'string' || !ISO_DATE.test(value)) {
    return null;
  }
  const today: string = new Date().toISOString().slice(0, 10);
  return value > today ? { notFuture: true } : null;
};

export const noBlank: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value: unknown = control.value;
  const valid: boolean = typeof value === 'string' && value.trim().length > 0;
  return valid ? null : { noBlank: true };
};

export const noNewline: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value: unknown = control.value;
  const valid: boolean = typeof value !== 'string' || (!value.includes('\n') && !value.includes('\r'));
  return valid ? null : { noNewline: true };
};

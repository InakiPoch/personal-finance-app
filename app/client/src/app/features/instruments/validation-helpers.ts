import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Group-level validator: when `type` is `credit`, `cutoffDate` must be an integer 1–31.
 * Sets and clears the `creditCutoff` error on the `cutoffDate` control itself, which carries no other validators.
 */
export const creditRequiresCutoff: ValidatorFn = (group: AbstractControl): ValidationErrors | null => {
  const cutoffControl: AbstractControl | null = group.get('cutoffDate');
  if(cutoffControl === null) {
    return null;
  }
  const isCredit: boolean = group.get('type')?.value === 'credit';
  const value: unknown = cutoffControl.value;
  const cutoffValid: boolean = typeof value === 'number' && Number.isInteger(value) && value >= 1 && value <= 31;
  if(isCredit && !cutoffValid) {
    cutoffControl.setErrors({ creditCutoff: true });
    return { creditCutoff: true };
  }
  cutoffControl.setErrors(null);
  return null;
};

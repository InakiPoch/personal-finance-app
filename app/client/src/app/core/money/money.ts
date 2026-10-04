import { CurrencyCode } from '../types/currency-code';
import { Money } from '../types/money';

const arsFormatter: Intl.NumberFormat = new Intl.NumberFormat('es-AR', {
  style: 'currency',
  currency: 'ARS',
  minimumFractionDigits: 2
});

const usdFormatter: Intl.NumberFormat = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 2
});

/** Wrap an integer count of minor units (cents) as `Money`. */
export function fromMinorUnits(minor: number): Money {
  if(!Number.isInteger(minor)) {
    throw new RangeError(`fromMinorUnits expects an integer, received ${minor}`);
  }
  return minor as Money;
}

/**
 * Convert a major-unit amount (e.g. 12.34 pesos) to `Money` without float arithmetic.
 */
export function toMinorUnits(major: number): Money {
  if(!Number.isFinite(major)) {
    throw new RangeError(`toMinorUnits expects a finite number, received ${major}`);
  }
  const text: string = Math.abs(major).toString();
  if(text.includes('e')) {
    throw new RangeError(`toMinorUnits does not accept exponential notation: ${major}`);
  }
  const [whole, fraction = '']: string[] = text.split('.');
  if(fraction.length > 2) {
    throw new RangeError(`toMinorUnits accepts at most two fractional digits, received ${major}`);
  }
  const sign: number = major < 0 ? -1 : 1;
  const minor: number = Number(whole) * 100 + Number((fraction + '00').slice(0, 2));
  return (sign * minor) as Money;
}

export function formatMoney(value: Money, code: CurrencyCode): string {
  return code === 'USD'
    ? usdFormatter.format(value / 100).replace('$', '$ ')
    : arsFormatter.format(value / 100);
}

export function formatArs(value: Money): string {
  return formatMoney(value, 'ARS');
}

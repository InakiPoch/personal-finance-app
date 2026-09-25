import { Money } from '../types/money';
import { formatArs, formatMoney, fromMinorUnits, toMinorUnits } from './money';

/** Strip every non-digit so currency-string assertions survive locale/ICU differences. */
function digitsOf(value: string): string {
  return value.replace(/\D/g, '');
}

describe('money', () => {
  describe('fromMinorUnits', () => {
    it('wraps an integer count of cents unchanged', () => {
      expect(fromMinorUnits(1234)).toBe(1234 as Money);
      expect(fromMinorUnits(0)).toBe(0 as Money);
      expect(fromMinorUnits(-500)).toBe(-500 as Money);
    });
    it('throws on a non-integer', () => {
      expect(() => fromMinorUnits(12.34)).toThrowError(RangeError);
      expect(() => fromMinorUnits(Number.NaN)).toThrowError(RangeError);
    });
  });

  describe('toMinorUnits', () => {
    it('converts whole amounts', () => {
      expect(toMinorUnits(10)).toBe(1000 as Money);
      expect(toMinorUnits(0)).toBe(0 as Money);
    });
    it('converts amounts with one or two decimals', () => {
      expect(toMinorUnits(12.34)).toBe(1234 as Money);
      expect(toMinorUnits(2.5)).toBe(250 as Money);
    });
    it('does not drift where `major * 100` would need rounding', () => {
      expect(toMinorUnits(10.1)).toBe(1010 as Money);
    });
    it('handles negative amounts, sign applied after decomposition', () => {
      expect(toMinorUnits(-5)).toBe(-500 as Money);
      expect(toMinorUnits(-5.5)).toBe(-550 as Money);
    });
    it('rejects an imprecise arithmetic result rather than truncating it', () => {
      expect(() => toMinorUnits(0.1 + 0.2)).toThrowError(RangeError);
    });
    it('rejects more than two fractional digits', () => {
      expect(() => toMinorUnits(1.005)).toThrowError(RangeError);
    });
    it('rejects non-finite input', () => {
      expect(() => toMinorUnits(Number.NaN)).toThrowError(RangeError);
      expect(() => toMinorUnits(Number.POSITIVE_INFINITY)).toThrowError(RangeError);
    });
    it('rejects exponential notation', () => {
      expect(() => toMinorUnits(1e21)).toThrowError(RangeError);
      expect(() => toMinorUnits(1e-7)).toThrowError(RangeError);
    });
    it('round-trips with fromMinorUnits', () => {
      expect(fromMinorUnits(toMinorUnits(12.34))).toBe(1234 as Money);
      expect(toMinorUnits(1234 / 100)).toBe(fromMinorUnits(1234));
    });
  });

  describe('formatArs', () => {
    it('returns a currency string carrying the amount digits and symbol', () => {
      const formatted: string = formatArs(123456789 as Money);
      expect(typeof formatted).toBe('string');
      expect(digitsOf(formatted)).toBe('123456789');
      expect(formatted).toMatch(/\$|ARS/);
    });
    it('keeps a minus sign for negative amounts', () => {
      const formatted: string = formatArs(-1234 as Money);
      expect(digitsOf(formatted)).toBe('1234');
      expect(formatted).toContain('-');
    });
    it('always shows two fraction digits', () => {
      expect(digitsOf(formatArs(500 as Money))).toBe('500');
    });
  });

  describe('formatMoney', () => {
    it('formats USD with the dollar amount and a US$/$ symbol', () => {
      const formatted: string = formatMoney(123456789 as Money, 'USD');
      expect(typeof formatted).toBe('string');
      expect(digitsOf(formatted)).toBe('123456789');
      expect(formatted).toMatch(/\$/);
    });
    it('keeps a minus sign for a negative USD amount', () => {
      const formatted: string = formatMoney(-1234 as Money, 'USD');
      expect(digitsOf(formatted)).toBe('1234');
      expect(formatted).toContain('-');
    });
    it('always shows two fraction digits for USD', () => {
      expect(digitsOf(formatMoney(500 as Money, 'USD'))).toBe('500');
    });
    it('routes ARS through the same output as formatArs', () => {
      expect(formatMoney(123456 as Money, 'ARS')).toBe(formatArs(123456 as Money));
    });
  });
});

import { daysInMonth, formatClosingDate, toIsoDate } from './closing-date-helpers';

describe('closing-date-helpers', () => {
  describe('formatClosingDate', () => {
    it('formats an ISO date as a short month and day', () => {
      expect(formatClosingDate('2026-10-24')).toBe('Oct 24');
    });
    it('does not shift across month or year boundaries', () => {
      expect(formatClosingDate('2026-11-01')).toBe('Nov 1');
      expect(formatClosingDate('2026-12-31')).toBe('Dec 31');
      expect(formatClosingDate('2027-01-01')).toBe('Jan 1');
    });
  });

  describe('daysInMonth', () => {
    it('handles February in leap and non-leap years', () => {
      expect(daysInMonth(2024, 2)).toBe(29);
      expect(daysInMonth(2026, 2)).toBe(28);
    });
    it('handles 30 and 31 day months', () => {
      expect(daysInMonth(2026, 4)).toBe(30);
      expect(daysInMonth(2026, 10)).toBe(31);
      expect(daysInMonth(2026, 12)).toBe(31);
    });
  });

  describe('toIsoDate', () => {
    it('zero-pads month and day', () => {
      expect(toIsoDate(2026, 3, 5)).toBe('2026-03-05');
      expect(toIsoDate(2026, 12, 31)).toBe('2026-12-31');
    });
  });
});

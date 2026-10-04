import { IsoDate } from '../../core/types/iso-date';

const shortDate: Intl.DateTimeFormat = new Intl.DateTimeFormat('en-US', {
  month: 'short',
  day: 'numeric',
  timeZone: 'UTC',
});

/** Formats an `IsoDate` (`2026-10-24`) as "Oct 24" without any timezone shift. */
export function formatClosingDate(date: IsoDate): string {
  const [year, month, day]: number[] = date.split('-').map(Number);
  return shortDate.format(new Date(Date.UTC(year, month - 1, day)));
}

/** Last day of a 1-based month, for bounding the inline date input. */
export function daysInMonth(year: number, month: number): number {
  return new Date(Date.UTC(year, month, 0)).getUTCDate();
}

/** `YYYY-MM-DD` for a year, 1-based month and day. */
export function toIsoDate(year: number, month: number, day: number): IsoDate {
  return `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}

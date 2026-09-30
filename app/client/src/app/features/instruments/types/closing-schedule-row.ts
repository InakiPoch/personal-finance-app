import { IsoDate } from '../../../core/types/iso-date';

/** One open billing month of a credit card's closing schedule (`GET .../closing-dates`). */
export type ClosingScheduleRow = {
  year: number;
  month: number;
  closingDate: IsoDate;
  isOverride: boolean;
  isLocked: boolean;
};

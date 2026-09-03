import { Money } from '../../../core/types/money';

export type CardFutureScheduleRow = {
  planId: string;
  installmentId: string;
  sequence: number;
  cycleYear: number;
  cycleMonth: number;
  amountMinorUnits: Money;
};

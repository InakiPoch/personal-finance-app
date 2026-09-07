import { IsoDate } from '../../../core/types/iso-date';
import { IsoInstant } from '../../../core/types/iso-instant';
import { Money } from '../../../core/types/money';

export type MonthlyStatementInstallment = {
  planId: string;
  installmentId: string;
  sequence: number;
  installmentCount: number;
  purchaseDate: IsoDate;
  cycleYear: number;
  cycleMonth: number;
  amountMinorUnits: Money;
  isReversed: boolean;
  reversalTransactionId: string | null;
  isPaid: boolean;
  paidOnUtc: IsoInstant | null;
};

import { IsoDate } from '../../../core/types/iso-date';
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
};

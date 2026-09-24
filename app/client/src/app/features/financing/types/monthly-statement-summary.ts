import { CurrencyCode } from '../../../core/types/currency-code';
import { IsoInstant } from '../../../core/types/iso-instant';
import { Money } from '../../../core/types/money';

export type MonthlyStatementSummary = {
  statementId: string;
  cardId: string;
  cardName: string;
  cycleYear: number;
  cycleMonth: number;
  amountDueMinorUnits: Money;
  isPaid: boolean;
  paidOnUtc: IsoInstant | null;
  currencyCode: CurrencyCode;
};

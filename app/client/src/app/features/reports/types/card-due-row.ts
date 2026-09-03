import { CurrencyCode } from '../../../core/types/currency-code';
import { Money } from '../../../core/types/money';

export type CardDueRow = {
  bucket: 'Accrued' | 'Future';
  card: string;
  cycleYear: number | null;
  cycleMonth: number | null;
  amountMinorUnits: Money;
  currencyCode: CurrencyCode;
  cardId: string | null;
};

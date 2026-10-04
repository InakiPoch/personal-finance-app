import { CurrencyCode } from '../../../core/types/currency-code';
import { IsoDate } from '../../../core/types/iso-date';
import { Money } from '../../../core/types/money';

export type RecordIncome = {
  amountMinorUnits: Money;
  targetAccountId: string;
  receivedOn: IsoDate;
  description: string;
  currencyCode: CurrencyCode;
};

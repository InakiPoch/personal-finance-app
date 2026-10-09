import { CurrencyCode } from '../../../core/types/currency-code';
import { IsoDate } from '../../../core/types/iso-date';
import { Money } from '../../../core/types/money';

/** `POST /v1/parties/{id}/borrowings` request body — money borrowed from a party into a Bank/Cash account. */
export type RecordBorrowing = {
  amountMinorUnits: Money;
  currencyCode: CurrencyCode;
  destinationAccountId: string;
  borrowedOn: IsoDate;
  description: string;
  today: IsoDate;
};

import { CurrencyCode } from '../../../core/types/currency-code';
import { IsoDate } from '../../../core/types/iso-date';
import { Money } from '../../../core/types/money';

/** `POST /v1/parties/{id}/loans` request body — money lent to a party from a Bank/Cash account. */
export type RecordLoan = {
  amountMinorUnits: Money;
  currencyCode: CurrencyCode;
  sourceAccountId: string;
  lentOn: IsoDate;
  description: string;
  /** The caller's local date; the API uses it for the "not in the future" check. */
  today: IsoDate;
};

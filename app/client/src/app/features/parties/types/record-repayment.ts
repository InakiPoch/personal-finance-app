import { CurrencyCode } from '../../../core/types/currency-code';
import { IsoDate } from '../../../core/types/iso-date';
import { Money } from '../../../core/types/money';

/** `POST /v1/parties/{id}/repayments` request body — paying a party back from a Bank/Cash account. */
export type RecordRepayment = {
  amountMinorUnits: Money;
  currencyCode: CurrencyCode;
  sourceAccountId: string;
  paidOn: IsoDate;
  today: IsoDate;
};

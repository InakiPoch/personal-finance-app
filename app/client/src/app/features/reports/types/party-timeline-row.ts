import { CurrencyCode } from '../../../core/types/currency-code';
import { IsoInstant } from '../../../core/types/iso-instant';
import { Money } from '../../../core/types/money';

/** One row of `GET /v1/reports/parties/{id}/timeline` — a currency-aware current-account movement. */
export type PartyTimelineRow = {
  movementOnUtc: IsoInstant;
  description: string;
  deltaMinorUnits: Money;
  runningBalanceMinorUnits: Money;
  currencyCode: CurrencyCode;
};

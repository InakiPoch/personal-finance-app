import { CurrencyCode } from '../../../core/types/currency-code';
import { Money } from '../../../core/types/money';

/** One row of `GET /v1/parties/{id}/future-shares` — a not-yet-accrued monthly installment share. */
export type FuturePartyShare = {
  cycleYear: number;
  cycleMonth: number;
  shareMinorUnits: Money;
  currencyCode: CurrencyCode;
  sourceLabel: string;
  /** Set on the I-owe side: the party purchase this installment belongs to. */
  purchaseId?: string | null;
};

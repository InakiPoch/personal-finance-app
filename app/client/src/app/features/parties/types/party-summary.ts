import { PartyCurrencyBalance } from './party-currency-balance';
import { Party } from './party';

/** `GET /v1/parties` row: both sides per currency, never netted. */
export type PartySummary = Party & {
  owedToYou: PartyCurrencyBalance[];
  youOwe: PartyCurrencyBalance[];
  scheduledToYouCount: number;
  scheduledYouOweCount: number;
  settledUp: boolean;
};

import { PartyCurrencyBalance } from './party-currency-balance';

/** `GET /v1/parties/{id}/balance` — the running current-account balance for one party, per currency. */
export type CurrentAccountBalance = {
  partyId: string;
  name: string;
  balances: PartyCurrencyBalance[];
};

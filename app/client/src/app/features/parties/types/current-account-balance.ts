import { PartyCurrencyBalance } from './party-currency-balance';

/** `GET /v1/parties/{id}/balance` — one party's balance per currency: `balances` is what they owe you, `payableBalances` what you owe them (never netted). */
export type CurrentAccountBalance = {
  partyId: string;
  name: string;
  balances: PartyCurrencyBalance[];
  payableBalances: PartyCurrencyBalance[];
};

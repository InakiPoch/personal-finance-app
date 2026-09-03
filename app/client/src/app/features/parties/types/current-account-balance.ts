import { Money } from '../../../core/types/money';

/** `GET /v1/parties/{id}/balance` — the running current-account balance for one party. */
export type CurrentAccountBalance = {
  partyId: string;
  name: string;
  balanceMinorUnits: Money;
};

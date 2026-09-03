import { IsoInstant } from '../../../core/types/iso-instant';
import { Money } from '../../../core/types/money';

/** `POST /v1/parties/{id}/settlements` request body — settles part or all of a current-account balance. */
export type SettleCurrentAccount = {
  amountMinorUnits: Money;
  bankAccountId: string;
  settledOnUtc: IsoInstant;
};

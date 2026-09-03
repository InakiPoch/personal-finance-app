import { IsoInstant } from '../../../core/types/iso-instant';
import { Money } from '../../../core/types/money';

/** One row of `GET /v1/parties/{id}/timeline` — a current-account movement and its running balance. */
export type CurrentAccountTimelineRow = {
  movementOnUtc: IsoInstant;
  description: string;
  deltaMinorUnits: Money;
  runningBalanceMinorUnits: Money;
};

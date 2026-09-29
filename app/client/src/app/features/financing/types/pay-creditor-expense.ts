import { Money } from '../../../core/types/money';

/** Body for `POST /v1/financing/creditor-purchases/{planId}/pay`. `null` pays whatever remains. */
export type PayCreditorExpense = {
  amountMinorUnits: Money | null;
};

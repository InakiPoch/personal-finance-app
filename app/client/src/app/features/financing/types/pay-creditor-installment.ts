import { Money } from '../../../core/types/money';

/** Body for `POST /v1/financing/creditor-installments/{id}/pay`. `null` pays whatever remains. */
export type PayCreditorInstallment = {
  amountMinorUnits: Money | null;
};

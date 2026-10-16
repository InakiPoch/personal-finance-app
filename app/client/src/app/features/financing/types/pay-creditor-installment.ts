import { Money } from '../../../core/types/money';

/** Body for `POST /v1/financing/creditor-installments/{id}/pay`. `null` pays whatever remains; the Bank/Cash account is debited. */
export type PayCreditorInstallment = {
  bankAccountId: string;
  amountMinorUnits: Money | null;
};

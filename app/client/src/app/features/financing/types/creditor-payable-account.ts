import { Money } from '../../../core/types/money';

export type CreditorPayableAccount = {
  accountId: string;
  label: string;
  outstandingMinorUnits: Money;
};

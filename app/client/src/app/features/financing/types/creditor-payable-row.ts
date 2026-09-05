import { IsoDate } from '../../../core/types/iso-date';
import { Money } from '../../../core/types/money';
import { CreditorPayableAccount } from './creditor-payable-account';

export type CreditorPayableRow = {
  creditorId: string;
  creditorName: string;
  outstandingMinorUnits: Money;
  nextDueDate: IsoDate | null;
  accounts: CreditorPayableAccount[];
};

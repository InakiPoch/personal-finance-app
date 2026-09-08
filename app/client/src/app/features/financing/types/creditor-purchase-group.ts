import { IsoDate } from '../../../core/types/iso-date';
import { Money } from '../../../core/types/money';
import { CreditorInstallmentRow } from './creditor-installment-row';

export type CreditorPurchaseGroup = {
  planId: string;
  description: string;
  purchaseDate: IsoDate;
  totalMinorUnits: Money;
  outstandingMinorUnits: Money;
  installments: CreditorInstallmentRow[];
};

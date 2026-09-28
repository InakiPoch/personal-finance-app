import { Money } from '../../../core/types/money';
import { CreditorInstallmentPartyShare } from './creditor-installment-party-share';

export type CreditorInstallmentRow = {
  installmentId: string;
  sequence: number;
  installmentCount: number;
  amountMinorUnits: Money;
  dueYear: number;
  dueMonth: number;
  isPaid: boolean;
  isReversed: boolean;
  status: 'overdue' | 'due' | 'future' | 'paid' | 'reversed';
  paidMinorUnits: Money;
  remainingMinorUnits: Money;
  hasPayments: boolean;
  partyShares: CreditorInstallmentPartyShare[];
};

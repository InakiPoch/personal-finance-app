import { Money } from '../../../core/types/money';

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
};

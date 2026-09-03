import { IsoInstant } from '../../../core/types/iso-instant';
import { Money } from '../../../core/types/money';
import { SharedExpenseParticipant } from './shared-expense-participant';

export type RegisterSharedExpense = {
  description: string;
  totalMinorUnits: Money;
  expenseAccountId: string;
  fundingAccountId: string;
  incurredOnUtc: IsoInstant;
  participants: SharedExpenseParticipant[];
};

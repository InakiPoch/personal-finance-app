import { IsoDate } from '../../../core/types/iso-date';
import { Money } from '../../../core/types/money';
import { SplitParticipant } from './split-participant';

export type CreatePaymentPlan = {
  amountMinorUnits: Money;
  cardId?: string;
  installmentCount: number;
  purchaseDate: IsoDate;
  description: string;
  split?: SplitParticipant[];
  creditorId?: string;
  creditorAccountId?: string;
  bankAccountId?: string;
};

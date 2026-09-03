import { IsoDate } from '../../../core/types/iso-date';
import { Money } from '../../../core/types/money';
import { Frequency } from './frequency';

export type ActiveSubscription = {
  subscriptionId: string;
  name: string;
  amountMinorUnits: Money;
  category: string;
  frequency: Frequency;
  anchorDay: number;
  nextDueDate: IsoDate;
};

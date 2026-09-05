import { IsoDate } from '../../../core/types/iso-date';
import { Money } from '../../../core/types/money';

export type RecentPurchaseRow = {
  planId: string;
  description: string;
  cardName: string;
  purchaseDate: IsoDate;
  totalMinorUnits: Money;
  installmentCount: number;
  isCreditorPayment: boolean;
};

import { IsoDate } from '../../../core/types/iso-date';
import { Money } from '../../../core/types/money';

export type CardPurchaseRow = {
  planId: string;
  description: string;
  totalMinorUnits: Money;
  installmentCount: number;
  outstandingCount: number;
  purchaseDate: IsoDate;
};

import { Money } from '../../../core/types/money';
import { Direction } from './direction';

export type TransactionLine = {
  accountId: string;
  direction: Direction;
  amountMinorUnits: Money;
};

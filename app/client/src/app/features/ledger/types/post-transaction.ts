import { IsoInstant } from '../../../core/types/iso-instant';
import { TransactionLine } from './transaction-line';

export type PostTransaction = {
  lines: TransactionLine[];
  postedOnUtc: IsoInstant;
  splitReferenceId?: string;
  installmentReferenceId?: string;
  description?: string;
};

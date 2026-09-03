import { IsoInstant } from '../../../core/types/iso-instant';

export type PayStatement = {
  bankAccountId: string;
  paidOnUtc: IsoInstant;
};

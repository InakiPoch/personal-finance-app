import { IsoInstant } from '../../../core/types/iso-instant';

export type PayInstallment = {
  bankAccountId: string;
  paidOnUtc: IsoInstant;
};

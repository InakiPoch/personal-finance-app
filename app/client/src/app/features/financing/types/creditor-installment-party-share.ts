import { Money } from '../../../core/types/money';

export type CreditorInstallmentPartyShare = {
  partyId: string;
  partyName: string;
  shareMinorUnits: Money;
  isPaid: boolean;
};

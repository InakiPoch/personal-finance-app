import { CurrencyCode } from '../../../core/types/currency-code';
import { Money } from '../../../core/types/money';

export type CreditorPayDialogConfirm =
  | { kind: 'own'; amountMinorUnits: Money | null; currencyCode: CurrencyCode | null; bankAccountId: string }
  | { kind: 'party'; partyId: string; bankAccountId: string };

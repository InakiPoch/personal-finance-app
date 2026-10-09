import { CurrencyCode } from '../../../core/types/currency-code';
import { IsoDate } from '../../../core/types/iso-date';
import { Money } from '../../../core/types/money';
import { PartyPurchaseKind } from './party-purchase-kind';

/** `POST /v1/parties/{id}/purchases` request body — my share of a purchase the party paid. */
export type RecordPartyPurchase = {
  shareMinorUnits: Money;
  currencyCode: CurrencyCode;
  description: string;
  categoryName: string;
  purchaseDate: IsoDate;
  kind: PartyPurchaseKind;
  today: IsoDate;
};

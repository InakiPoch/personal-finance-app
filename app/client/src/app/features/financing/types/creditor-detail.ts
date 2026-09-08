import { CreditorPurchaseGroup } from './creditor-purchase-group';

export type CreditorDetail = {
  creditorId: string;
  creditorName: string;
  purchases: CreditorPurchaseGroup[];
};

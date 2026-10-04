/** Badge label for a feed row — exactly the strings the API sends. */
export type TransactionKind =
  | 'Card installment'
  | 'Card credit'
  | 'Card bill payment'
  | 'Subscription'
  | 'Shared expense'
  | 'Income'
  | 'Expense'
  | 'Party payment'
  | 'Undo entry'
  | 'Other';

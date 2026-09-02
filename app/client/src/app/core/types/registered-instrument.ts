export type RegisteredInstrument = {
  id: string;
  type: 'debit' | 'credit' | 'cash';
  name: string;
  cutoffDate?: number;
};

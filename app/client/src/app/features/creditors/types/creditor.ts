export type CreditorAccount = {
  id: string;
  label: string;
  identifier: string | null;
};

export type Creditor = {
  id: string;
  name: string;
  accounts: CreditorAccount[];
};

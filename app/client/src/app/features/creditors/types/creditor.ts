export type CreditorAccount = {
  id: string;
  label: string;
  identifier: string;
};

export type Creditor = {
  id: string;
  name: string;
  accounts: CreditorAccount[];
};

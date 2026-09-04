export type CreateCreditorAccount = {
  label: string;
  identifier: string;
};

export type CreateCreditor = {
  name: string;
  accounts: CreateCreditorAccount[];
};

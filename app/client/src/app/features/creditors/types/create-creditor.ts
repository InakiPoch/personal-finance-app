export type CreateCreditorAccount = {
  label: string;
  identifier: string | null;
};

export type CreateCreditor = {
  name: string;
  accounts: CreateCreditorAccount[];
};

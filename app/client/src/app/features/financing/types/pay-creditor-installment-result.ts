/** `POST /v1/financing/creditor-installments/{id}/pay` (and `/unpay`) response. */
export type PayCreditorInstallmentResult = {
  installmentId: string;
};

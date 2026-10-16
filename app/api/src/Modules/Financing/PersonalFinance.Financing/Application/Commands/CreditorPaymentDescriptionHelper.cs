using PersonalFinance.Financing.Domain;

namespace PersonalFinance.Financing.Application.Commands;

/// <summary>
/// Builds the ledger description shown for creditor payments.
/// </summary>
internal static class CreditorPaymentDescriptionHelper {
    public static string ForInstallment(PaymentPlan plan, Installment installment, string creditorName) {
        return $"Installment {installment.Sequence}/{plan.InstallmentCount} · {plan.Description} · {creditorName}";
    }

    public static string ForExpense(PaymentPlan plan, string creditorName) {
        return $"{plan.Description} · {creditorName}";
    }

    public static string ForFullDebt(string creditorName) {
        return $"Full debt · {creditorName}";
    }
}

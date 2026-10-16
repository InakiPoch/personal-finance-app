using System.Globalization;
using PersonalFinance.Financing.Domain;

namespace PersonalFinance.Financing.Application.Commands;

/// <summary>
/// Builds the ledger description shown for card bill and card installment payments.
/// </summary>
internal static class CardPaymentDescriptionHelper {
    public static string ForStatement(CreditCard card, MonthlyStatement statement) {
        var billingMonth = new DateTime(statement.CycleYear, statement.CycleMonth, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        return $"Card bill · {card.Name} · {billingMonth}";
    }

    public static string ForInstallment(CreditCard card, PaymentPlan plan, Installment installment) {
        return $"Installment {installment.Sequence}/{plan.InstallmentCount} · {plan.Description} · {card.Name}";
    }
}

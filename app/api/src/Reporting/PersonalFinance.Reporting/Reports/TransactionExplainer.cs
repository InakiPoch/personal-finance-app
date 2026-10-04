using System.Globalization;

namespace PersonalFinance.Reporting.Reports;

/// <summary>
/// One ledger entry of a transaction, as read from <c>vw_ledger_transaction_legs</c>.
/// </summary>
public sealed record TransactionLeg(string AccountName, string AccountKind, bool IsDebit, long AmountMinorUnits, string CurrencyCode);

/// <summary>
/// Label of the installment a transaction points at (<c>vw_installment_labels</c>).
/// </summary>
public sealed record InstallmentLabel(int Sequence, int InstallmentCount, string PlanDescription, string? CardName, bool IsPaid, bool IsReversed);

/// <summary>
/// Everything the explainer needs about ONE transaction. No IO: built by the query handler.
/// </summary>
public sealed record TransactionFacts(
    Guid Id,
    DateTimeOffset PostedOnUtc,
    string? Description,
    bool IsUndoEntry,
    bool IsUndone,
    bool HasInstallmentReference,
    bool HasSplitReference,
    bool HasSubscriptionReference,
    IReadOnlyList<TransactionLeg> Legs,
    InstallmentLabel? Installment,
    string? SubscriptionName
);

/// <summary>
/// A feed row. <c>Kind</c> is the badge text: Card installment, Card credit, Card bill payment, Subscription,
/// Shared expense, Income, Expense, Party payment, Undo entry or Other.
/// </summary>
public sealed record TransactionFeedRow(
    Guid Id,
    DateTimeOffset PostedOnUtc,
    string Kind,
    string Description,
    IReadOnlyList<string> FromAccounts,
    IReadOnlyList<string> ToAccounts,
    long AmountMinorUnits,
    string CurrencyCode,
    bool IsUndoEntry,
    bool IsUndone,
    IReadOnlyList<string> ImpactLines
);

/// <summary>
/// Pure: classifies one transaction from its legs, references and labels and writes the plain-language description and the
/// "If you reverse this" bullets. The installment branch mirrors the Ledger's <c>ReversalCalculator.Decide</c>
/// (paid and not yet reversed => credit on the next bill, otherwise the bill goes down); everything else mirrors the plain mirror entry.
/// </summary>
public static class TransactionExplainer {
    private const string kindInstallment = "Card installment";
    private const string kindCardCredit = "Card credit";
    private const string kindBillPayment = "Card bill payment";
    private const string kindSubscription = "Subscription";
    private const string kindShared = "Shared expense";
    private const string kindIncome = "Income";
    private const string kindExpense = "Expense";
    private const string kindPartyPayment = "Party payment";
    private const string kindUndo = "Undo entry";
    private const string kindOther = "Other";

    public static TransactionFeedRow Explain(TransactionFacts facts) {
        var legs = facts.Legs;
        var currency = legs.Count > 0 ? legs[0].CurrencyCode : "ARS";
        var amount = legs.Where(leg => leg.IsDebit).Sum(leg => leg.AmountMinorUnits);
        var from = legs.Where(leg => leg.IsDebit).Select(displayName).Distinct().ToList();
        var to = legs.Where(leg => !leg.IsDebit).Select(displayName).Distinct().ToList();
        if(facts.IsUndoEntry) {
            var mirrored = legs.Select(leg => leg with { IsDebit = !leg.IsDebit }).ToList();
            var (Kind, Description, Impact) = classify(facts, mirrored, currency);
            return new TransactionFeedRow(facts.Id, facts.PostedOnUtc, kindUndo, $"Undid: {Description}", from, to, amount, currency, true, facts.IsUndone, []);
        }
        var classified = classify(facts, legs, currency);
        IReadOnlyList<string> impact = facts.IsUndone ? ["Already undone."] : classified.Impact;
        return new TransactionFeedRow(facts.Id, facts.PostedOnUtc, classified.Kind, classified.Description, from, to, amount, currency, false, facts.IsUndone, impact);
    }

    private static (string Kind, string Description, IReadOnlyList<string> Impact) classify(TransactionFacts facts, IReadOnlyList<TransactionLeg> legs, string currency) {
        var debits = legs.Where(leg => leg.IsDebit).ToList();
        var credits = legs.Where(leg => !leg.IsDebit).ToList();
        var total = debits.Sum(leg => leg.AmountMinorUnits);
        var installment = facts.Installment;
        if(facts.HasInstallmentReference && installment?.CardName is { } card) {
            if(debits.Any(leg => leg.AccountKind == "CardCredit")) {
                return (kindCardCredit, $"Credit for installment {installment.Sequence} of {installment.InstallmentCount} of \"{installment.PlanDescription}\" on {card}",
                    [$"The {money(total, currency)} credit on your next {card} bill is removed."]);
            }
            var impact = new List<string> { $"Installment {installment.Sequence} of {installment.InstallmentCount} of \"{installment.PlanDescription}\" is cancelled." };
            impact.Add(installment.IsPaid && !installment.IsReversed ? $"{money(total, currency)} comes back as a credit on your next {card} bill." : $"Your {card} bill goes down by {money(total, currency)}.");
            impact.AddRange(partyLines(debits, currency));
            return (kindInstallment, $"{installment.PlanDescription} — installment {installment.Sequence} of {installment.InstallmentCount} on {card}", impact);
        }
        if(facts.HasSubscriptionReference) {
            var name = facts.SubscriptionName ?? facts.Description ?? debits.FirstOrDefault()?.AccountName ?? "Subscription";
            var impact = returnLines(credits, currency);
            impact.Add("The subscription's paid month is not rolled back; undo the payment from the Subscriptions page for that.");
            return (kindSubscription, name, impact);
        }
        if(facts.HasSplitReference) {
            var impact = returnLines(credits, currency);
            var party = partyLines(debits, currency);
            impact.AddRange(party);
            var hasMoneyBack = impact.Count > party.Count;
            var description = hasMoneyBack
                ? facts.Description ?? debits.FirstOrDefault(leg => leg.AccountKind != "Receivable")?.AccountName ?? "Shared expense"
                : $"Share owed by {string.Join(", ", debits.Where(leg => leg.AccountKind == "Receivable").Select(leg => displayName(leg).Replace(" (owes you)", "")))}";
            return (kindShared, description, impact);
        }
        if(credits.FirstOrDefault(leg => leg.AccountKind == "Income") is { } incomeLeg) {
            var impact = debits.Where(leg => isMoney(leg)).Select(leg => $"{money(leg.AmountMinorUnits, currency)} is removed from {leg.AccountName}.").ToList();
            return (kindIncome, facts.Description ?? incomeLeg.AccountName, impact);
        }
        if(debits.FirstOrDefault(leg => leg.AccountKind == "CardLiability") is { } liabilityLeg && credits.All(leg => isMoney(leg) || leg.AccountKind == "CardCredit")) {
            var card2 = cardNameOf(liabilityLeg);
            var impact = new List<string> { $"Your {card2} bill goes back up by {money(liabilityLeg.AmountMinorUnits, currency)}." };
            foreach(var leg in credits) {
                impact.Add(leg.AccountKind == "CardCredit"
                    ? $"{money(leg.AmountMinorUnits, currency)} of your {card2} credit is restored."
                    : $"{money(leg.AmountMinorUnits, currency)} returns to {leg.AccountName}.");
            }
            impact.Add("The bill stays marked as paid.");
            return (kindBillPayment, $"Paid {card2} bill", impact);
        }
        if(credits.FirstOrDefault(leg => leg.AccountKind == "Receivable") is { } receivableLeg && debits.All(isMoney)) {
            var party = displayName(receivableLeg).Replace(" (owes you)", "");
            var impact = new List<string> { $"{party} owes you {money(receivableLeg.AmountMinorUnits, currency)} again." };
            impact.AddRange(debits.Select(leg => $"{money(leg.AmountMinorUnits, currency)} is removed from {leg.AccountName}."));
            return (kindPartyPayment, $"{party} paid you", impact);
        }
        if(debits.FirstOrDefault(leg => leg.AccountKind == "Expense") is { } expenseLeg && credits.All(isMoney)) {
            return (kindExpense, facts.Description ?? expenseLeg.AccountName, returnLines(credits, currency));
        }
        return (kindOther, facts.Description ?? "Manual entry", ["Every amount in this movement is undone."]);
    }

    private static List<string> returnLines(IEnumerable<TransactionLeg> credits, string currency) {
        return credits.Where(isMoney).Select(leg => $"{money(leg.AmountMinorUnits, currency)} returns to {leg.AccountName}.").ToList();
    }

    private static List<string> partyLines(IEnumerable<TransactionLeg> debits, string currency) {
        return debits
            .Where(leg => leg.AccountKind == "Receivable")
            .Select(leg => $"{displayName(leg).Replace(" (owes you)", "")} no longer owes you {money(leg.AmountMinorUnits, currency)}.")
            .ToList();
    }

    private static bool isMoney(TransactionLeg leg) {
        return leg.AccountKind is "Bank" or "Cash";
    }

    private static string cardNameOf(TransactionLeg leg) {
        return trimSuffix(leg.AccountName, " Liability");
    }

    // Account names carry developer words ("Visa Liability", "Juan Receivable"); show the plain-language names instead.
    private static string displayName(TransactionLeg leg) {
        return leg.AccountKind switch {
            "CardLiability" => $"What you owe on {trimSuffix(leg.AccountName, " Liability")}",
            "CardPurchases" => $"{trimSuffix(leg.AccountName, " Purchases")} purchases",
            "CardCredit" => $"{trimSuffix(leg.AccountName, " Credit")} credit",
            "Receivable" => $"{trimSuffix(leg.AccountName, " Receivable")} (owes you)",
            "CreditorPayable" => $"What you owe — {trimPrefix(leg.AccountName, "Payable to creditor — ")}",
            _ => leg.AccountName
        };
    }

    private static string trimSuffix(string value, string suffix) {
        return value.EndsWith(suffix, StringComparison.Ordinal) ? value[..^suffix.Length] : value;
    }

    private static string trimPrefix(string value, string prefix) {
        return value.StartsWith(prefix, StringComparison.Ordinal) ? value[prefix.Length..] : value;
    }

    // "ARS 40.000" / "USD 12,50": Argentine style, dot thousands and comma decimals (both supported currencies have 2 decimals).
    private static string money(long minorUnits, string currency) {
        var absolute = Math.Abs(minorUnits);
        var major = (absolute / 100).ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.');
        var cents = absolute % 100;
        var sign = minorUnits < 0 ? "-" : "";
        return cents == 0 ? $"{currency} {sign}{major}" : $"{currency} {sign}{major},{cents:00}";
    }
}

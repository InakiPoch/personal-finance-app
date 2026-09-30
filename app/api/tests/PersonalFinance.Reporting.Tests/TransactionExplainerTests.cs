using PersonalFinance.Reporting.Reports;
using Xunit;

namespace PersonalFinance.Reporting.Tests;

/// <summary>
/// Pure tests for <see cref="TransactionExplainer"/>: one fact per transaction kind plus a glossary guard over all of them.
/// </summary>
public sealed class TransactionExplainerTests {
    private static readonly string[] bannedWords = ["liability", "accrual", "storno", "reversal", "receivable"];

    [Fact]
    public void An_unpaid_card_installment_says_the_bill_goes_down() {
        var row = TransactionExplainer.Explain(unpaidInstallment());
        Assert.Equal("Card installment", row.Kind);
        Assert.Equal("Notebook — installment 3 of 12 on Visa", row.Description);
        Assert.Equal(["Visa purchases"], row.FromAccounts);
        Assert.Equal(["What you owe on Visa"], row.ToAccounts);
        Assert.Equal(4_000_000, row.AmountMinorUnits);
        Assert.Equal([
            "Installment 3 of 12 of \"Notebook\" is cancelled.",
            "Your Visa bill goes down by ARS 40.000."
        ], row.ImpactLines);
    }

    [Fact]
    public void A_paid_card_installment_comes_back_as_a_credit_on_the_next_bill() {
        var row = TransactionExplainer.Explain(unpaidInstallment() with { Installment = new InstallmentLabel(3, 12, "Notebook", "Visa", true, false) });
        Assert.Equal("ARS 40.000 comes back as a credit on your next Visa bill.", row.ImpactLines[1]);
    }

    [Fact]
    public void A_paid_but_already_reversed_installment_falls_back_to_the_bill_going_down() {
        var row = TransactionExplainer.Explain(unpaidInstallment() with { Installment = new InstallmentLabel(3, 12, "Notebook", "Visa", true, true) });
        Assert.Equal("Your Visa bill goes down by ARS 40.000.", row.ImpactLines[1]);
    }

    [Fact]
    public void A_card_installment_with_a_party_share_lists_who_no_longer_owes_you() {
        var facts = unpaidInstallment() with {
            HasSplitReference = true,
            Legs = [debit("Visa Purchases", "CardPurchases", 2_000_000), debit("Juan Receivable", "Receivable", 2_000_000), credit("Visa Liability", "CardLiability", 4_000_000)]
        };
        var row = TransactionExplainer.Explain(facts);
        Assert.Contains("Juan no longer owes you ARS 20.000.", row.ImpactLines);
    }

    [Fact]
    public void A_card_credit_entry_is_explained_as_a_credit_on_the_next_bill() {
        var facts = unpaidInstallment() with { Legs = [debit("Visa Credit", "CardCredit", 4_000_000), credit("Visa Liability", "CardLiability", 4_000_000)] };
        var row = TransactionExplainer.Explain(facts);
        Assert.Equal("Card credit", row.Kind);
        Assert.Equal(["The ARS 40.000 credit on your next Visa bill is removed."], row.ImpactLines);
        Assert.Equal(["Visa credit"], row.FromAccounts);
    }

    [Fact]
    public void A_card_bill_payment_honestly_says_the_bill_stays_marked_as_paid() {
        var row = TransactionExplainer.Explain(billPayment());
        Assert.Equal("Card bill payment", row.Kind);
        Assert.Equal("Paid Visa bill", row.Description);
        Assert.Equal([
            "Your Visa bill goes back up by ARS 80.000.",
            "ARS 80.000 returns to Galicia.",
            "The bill stays marked as paid."
        ], row.ImpactLines);
    }

    [Fact]
    public void A_subscription_uses_its_name_and_says_the_paid_month_is_not_rolled_back() {
        var row = TransactionExplainer.Explain(subscription());
        Assert.Equal("Subscription", row.Kind);
        Assert.Equal("Netflix", row.Description);
        Assert.Equal("ARS 5.000 returns to Galicia.", row.ImpactLines[0]);
        Assert.Contains("paid month is not rolled back", row.ImpactLines[1]);
    }

    [Fact]
    public void A_split_debit_expense_lists_the_money_back_and_each_party() {
        var row = TransactionExplainer.Explain(sharedExpense());
        Assert.Equal("Shared expense", row.Kind);
        Assert.Equal("Dinner", row.Description);
        Assert.Equal([
            "ARS 100 returns to Galicia.",
            "Juan no longer owes you ARS 25.",
            "Ana no longer owes you ARS 25."
        ], row.ImpactLines);
        Assert.Equal(10_000, row.AmountMinorUnits);
    }

    [Fact]
    public void A_share_owed_transaction_is_named_after_the_party_and_never_says_receivable() {
        var row = TransactionExplainer.Explain(shareOwed());
        Assert.Equal("Shared expense", row.Kind);
        Assert.Equal("Share owed by Juan", row.Description);
        Assert.Equal(["Juan no longer owes you ARS 25."], row.ImpactLines);
        Assert.Equal(["Juan (owes you)"], row.FromAccounts);
    }

    [Fact]
    public void An_income_uses_its_description_and_is_removed_from_the_account() {
        var row = TransactionExplainer.Explain(income());
        Assert.Equal("Income", row.Kind);
        Assert.Equal("Salary September", row.Description);
        Assert.Equal(["Galicia"], row.FromAccounts);
        Assert.Equal(["Salary"], row.ToAccounts);
        Assert.Equal(["ARS 900.000 is removed from Galicia."], row.ImpactLines);
    }

    [Fact]
    public void An_expense_without_a_description_falls_back_to_its_category() {
        var row = TransactionExplainer.Explain(expense(null));
        Assert.Equal("Expense", row.Kind);
        Assert.Equal("Groceries", row.Description);
        Assert.Equal(["ARS 120 returns to Galicia."], row.ImpactLines);
    }

    [Fact]
    public void A_party_payment_says_the_party_owes_you_again() {
        var row = TransactionExplainer.Explain(partyPayment());
        Assert.Equal("Party payment", row.Kind);
        Assert.Equal("Juan paid you", row.Description);
        Assert.Equal(["Juan owes you ARS 25 again.", "ARS 25 is removed from Galicia."], row.ImpactLines);
    }

    [Fact]
    public void An_undo_entry_is_locked_and_described_as_undid_the_original() {
        var row = TransactionExplainer.Explain(undoEntry());
        Assert.Equal("Undo entry", row.Kind);
        Assert.True(row.IsUndoEntry);
        Assert.Empty(row.ImpactLines);
        Assert.Equal("Undid: Groceries at Coto", row.Description);
    }

    [Fact]
    public void An_already_undone_transaction_only_says_so() {
        var row = TransactionExplainer.Explain(expense("Groceries at Coto") with { IsUndone = true });
        Assert.True(row.IsUndone);
        Assert.False(row.IsUndoEntry);
        Assert.Equal(["Already undone."], row.ImpactLines);
    }

    [Fact]
    public void An_unrecognized_shape_falls_back_to_other() {
        var row = TransactionExplainer.Explain(other());
        Assert.Equal("Other", row.Kind);
        Assert.Equal("Manual entry", row.Description);
        Assert.Equal(["Every amount in this movement is undone."], row.ImpactLines);
    }

    [Fact]
    public void Usd_amounts_use_a_comma_for_decimals() {
        var facts = expense(null) with { Legs = [debit("Groceries", "Expense", 1_250, "USD"), credit("Galicia", "Bank", 1_250, "USD")] };
        var row = TransactionExplainer.Explain(facts);
        Assert.Equal("USD", row.CurrencyCode);
        Assert.Equal(["USD 12,50 returns to Galicia."], row.ImpactLines);
    }

    [Fact]
    public void Ars_amounts_use_dots_for_thousands_and_a_comma_for_decimals() {
        var facts = expense(null) with { Legs = [debit("Groceries", "Expense", 4_000_050), credit("Galicia", "Bank", 4_000_050)] };
        var row = TransactionExplainer.Explain(facts);
        Assert.Equal(["ARS 40.000,50 returns to Galicia."], row.ImpactLines);
    }

    [Fact]
    public void No_produced_text_contains_developer_words() {
        var scenarios = new List<TransactionFacts> {
            unpaidInstallment(),
            unpaidInstallment() with { Installment = new InstallmentLabel(3, 12, "Notebook", "Visa", true, false) },
            unpaidInstallment() with { Legs = [debit("Visa Credit", "CardCredit", 4_000_000), credit("Visa Liability", "CardLiability", 4_000_000)] },
            unpaidInstallment() with { HasSplitReference = true, Legs = [debit("Juan Receivable", "Receivable", 2_000_000), credit("Visa Liability", "CardLiability", 2_000_000)] },
            billPayment(), subscription(), sharedExpense(), shareOwed(), income(), expense(null), partyPayment(), undoEntry(),
            expense("Groceries") with { IsUndone = true }, other()
        };
        foreach(var scenario in scenarios) {
            var row = TransactionExplainer.Explain(scenario);
            var texts = new List<string> { row.Kind, row.Description };
            texts.AddRange(row.FromAccounts);
            texts.AddRange(row.ToAccounts);
            texts.AddRange(row.ImpactLines);
            foreach(var text in texts) {
                foreach(var word in bannedWords) {
                    Assert.DoesNotContain(word, text, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }

    private static TransactionLeg debit(string name, string kind, long amount, string currency = "ARS") {
        return new TransactionLeg(name, kind, true, amount, currency);
    }

    private static TransactionLeg credit(string name, string kind, long amount, string currency = "ARS") {
        return new TransactionLeg(name, kind, false, amount, currency);
    }

    private static TransactionFacts facts(string? description, params TransactionLeg[] legs) {
        return new TransactionFacts(Guid.NewGuid(), new DateTimeOffset(2026, 9, 28, 9, 0, 0, TimeSpan.Zero), description, false, false, false, false, false, legs, null, null);
    }

    private static TransactionFacts unpaidInstallment() {
        return facts(null, debit("Visa Purchases", "CardPurchases", 4_000_000), credit("Visa Liability", "CardLiability", 4_000_000)) with {
            HasInstallmentReference = true,
            Installment = new InstallmentLabel(3, 12, "Notebook", "Visa", false, false)
        };
    }

    private static TransactionFacts billPayment() {
        return facts(null, debit("Visa Liability", "CardLiability", 8_000_000), credit("Galicia", "Bank", 8_000_000));
    }

    private static TransactionFacts subscription() {
        return facts(null, debit("Netflix Subscription", "Expense", 500_000), credit("Galicia", "Bank", 500_000)) with {
            HasSubscriptionReference = true,
            SubscriptionName = "Netflix"
        };
    }

    private static TransactionFacts sharedExpense() {
        return facts("Dinner", debit("Dining", "Expense", 5_000), debit("Juan Receivable", "Receivable", 2_500), debit("Ana Receivable", "Receivable", 2_500), credit("Galicia", "Bank", 10_000)) with {
            HasSplitReference = true
        };
    }

    private static TransactionFacts shareOwed() {
        return facts(null, debit("Juan Receivable", "Receivable", 2_500), credit("Visa Liability", "CardLiability", 2_500)) with { HasSplitReference = true };
    }

    private static TransactionFacts income() {
        return facts("Salary September", debit("Galicia", "Bank", 90_000_000), credit("Salary", "Income", 90_000_000));
    }

    private static TransactionFacts expense(string? description) {
        return facts(description, debit("Groceries", "Expense", 12_000), credit("Galicia", "Bank", 12_000));
    }

    private static TransactionFacts partyPayment() {
        return facts(null, debit("Galicia", "Bank", 2_500), credit("Juan Receivable", "Receivable", 2_500));
    }

    private static TransactionFacts undoEntry() {
        return facts("Groceries at Coto", debit("Galicia", "Bank", 12_000), credit("Groceries", "Expense", 12_000)) with { IsUndoEntry = true };
    }

    private static TransactionFacts other() {
        return facts(null, debit("Galicia", "Bank", 1_000), credit("Mercado Pago", "Bank", 1_000));
    }
}

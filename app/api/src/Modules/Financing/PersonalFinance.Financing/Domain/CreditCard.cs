using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Domain;

internal sealed class CreditCard : AggregateRoot<Guid> {
    public string Name { get; }
    public int CutoffDay { get; }
    public Guid LiabilityAccountId { get; }
    public Guid ExpenseAccountId { get; }
    public Guid CreditAccountId { get; }
    public Money CarriedCreditBalance { get; private set; }

    private CreditCard(Guid id, string name, int cutoffDay, Guid liabilityAccountId, Guid expenseAccountId, Guid creditAccountId) : base(id) {
        Name = name;
        CutoffDay = cutoffDay;
        LiabilityAccountId = liabilityAccountId;
        ExpenseAccountId = expenseAccountId;
        CreditAccountId = creditAccountId;
        CarriedCreditBalance = Money.Zero(Currency.Reference);
    }

    public static Result<CreditCard> Create(string name, int cutoffDay, Guid liabilityAccountId, Guid expenseAccountId, Guid creditAccountId) {
        if(string.IsNullOrWhiteSpace(name)) {
            return FinancingErrors.InvalidCardName;
        }
        if(cutoffDay is < 1 or > 31) {
            return FinancingErrors.InvalidCutoffDay;
        }
        return new CreditCard(Guid.CreateVersion7(), name.Trim(), cutoffDay, liabilityAccountId, expenseAccountId, creditAccountId);
    }
}

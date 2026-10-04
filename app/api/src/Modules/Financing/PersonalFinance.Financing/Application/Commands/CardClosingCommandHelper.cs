using Microsoft.EntityFrameworkCore;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands;

/// <summary>
/// Shared flow of every closing-day edit: load the card with its overrides, guard a locked month, apply the edit,
/// re-bucket the card's plans, and persist everything in one save.
/// </summary>
internal static class CardClosingCommandHelper {
    public static async Task<Result> ApplyAsync(
        FinancingDbContext context, Guid cardId, BillingCycle? lockedCycle, Func<CreditCard, Result> edit, CancellationToken cancellationToken) {
        var card = await context.CreditCards
            .Include(candidate => candidate.ClosingOverrides)
            .FirstOrDefaultAsync(candidate => candidate.Id == cardId, cancellationToken);
        if(card is null) {
            return Result.Failure(FinancingErrors.CardNotFound);
        }
        if(lockedCycle is not null) {
            var cycle = lockedCycle;
            var isLocked = await context.MonthlyStatements.AnyAsync(
                statement => statement.CardId == cardId && statement.CycleYear == cycle.Year && statement.CycleMonth == cycle.Month,
                cancellationToken
            );
            if(isLocked) {
                return Result.Failure(FinancingErrors.ClosingMonthLocked);
            }
        }
        var edited = edit(card);
        if(edited.IsFailure) {
            return edited;
        }
        var plans = await context.PaymentPlans
            .Include(plan => plan.Installments)
            .Where(plan => plan.CardId == cardId)
            .ToListAsync(cancellationToken);
        var rebucketed = CardRebucketer.Rebucket(card, plans);
        if(rebucketed.IsFailure) {
            return rebucketed;
        }
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public static Result<BillingCycle> ParseCycle(int year, int month) {
        if(year is < 2000 or > 2100 || month is < 1 or > 12) {
            return FinancingErrors.InvalidClosingDay;
        }
        return new BillingCycle(year, month);
    }
}

using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Contracts.IntegrationEvents;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Financing.Infrastructure.Persistence.Outbox;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;

namespace PersonalFinance.Financing.Application.Commands.CreatePaymentPlan;

/// <summary>
/// Persists an installment plan and, when the purchase is shared, a durable <see cref="PaymentPlanCreatedIntegrationEvent"/>.
/// For a back-dated card purchase it also settles every already-elapsed installment synchronously — accruing each closed
/// cycle onto its <see cref="MonthlyStatement"/> and paying, from <see cref="CreatePaymentPlanCommand.BankAccountId"/>,
/// the ones whose due month is already past — with historically-dated ledger postings, so Recent Purchases and the
/// statement list read correctly the instant the plan is saved instead of a scheduler tick later. For a back-dated
/// creditor-financed purchase it stamps every already-elapsed installment as paid (display-only <c>PaidOnUtc</c>, no
/// ledger movement and no accrual — creditor debt has no ledger footprint for the holder anywhere in the system).
/// </summary>
internal sealed class CreatePaymentPlanHandler(FinancingDbContext context, FinancingOutboxWriter outboxWriter, TimeProvider timeProvider, ILedgerApi ledger) : ICommandHandler<CreatePaymentPlanCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(CreatePaymentPlanCommand command, CancellationToken cancellationToken) {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var validation = CreatePaymentPlanValidator.Validate(command, today);
        if(validation.IsFailure) {
            return validation.Error;
        }
        int? cutoffDay;
        CreditCard? card = null;
        if(command.CardId is { } cardId) {
            card = await context.CreditCards.FirstOrDefaultAsync(candidate => candidate.Id == cardId, cancellationToken);
            if(card is null) {
                return FinancingErrors.CardNotFound;
            }
            cutoffDay = card.CutoffDay;
        } else {
            var creditor = await context.Creditors
                .Include(candidate => candidate.Accounts)
                .FirstOrDefaultAsync(candidate => candidate.Id == command.CreditorId, cancellationToken);
            if(creditor is null) {
                return FinancingErrors.CreditorNotFound;
            }
            if(creditor.Accounts.All(account => account.Id != command.CreditorAccountId)) {
                return FinancingErrors.CreditorAccountMismatch;
            }
            cutoffDay = null;
        }
        var plan = PaymentPlan.Create(
            command.CardId,
            Money.FromMinorUnits(command.AmountMinorUnits, Currency.FromCode(command.CurrencyCode)),
            command.InstallmentCount,
            command.PurchaseDate,
            command.Description,
            cutoffDay,
            new PhantomPennyAllocator(),
            command.Split?.Participants.Select(participant => (participant.PartyId, participant.Weight)).ToList(),
            command.CreditorId,
            command.CreditorAccountId
        );
        if(plan.IsFailure) {
            return plan.Error;
        }
        if(card is not null && hasElapsedInstallment(plan.Value, today) && command.BankAccountId is null) {
            return FinancingErrors.BackdatedCardBankAccountRequired;
        }
        context.PaymentPlans.Add(plan.Value);
        if(card is not null) {
            var settlement = await accrueAndSettleBackdatedInstallmentsAsync(
                plan.Value, card, command.BankAccountId, today, cancellationToken);
            if(settlement.IsFailure) {
                return settlement.Error;
            }
        } else {
            var stamped = stampBackdatedCreditorInstallments(plan.Value, today);
            if(stamped.IsFailure) {
                return stamped.Error;
            }
        }
        if(command.Split is not null) {
            outboxWriter.Add(new PaymentPlanCreatedIntegrationEvent(
                Guid.CreateVersion7(),
                DateTimeOffset.UtcNow,
                plan.Value.Id,
                command.CardId,
                plan.Value.Total.MinorUnits,
                plan.Value.PurchaseDate,
                command.Split.Participants,
                plan.Value.Total.Currency.Code)
            );
        }
        await context.SaveChangesAsync(cancellationToken);
        return plan.Value.Id;
    }

    private static bool hasElapsedInstallment(PaymentPlan plan, DateOnly today) {
        var currentMonthOrdinal = ordinalOf(new BillingCycle(today.Year, today.Month));
        return plan.Installments.Any(installment => ordinalOf(installment.DueCycle) < currentMonthOrdinal);
    }

    private static int ordinalOf(BillingCycle cycle) {
        return (cycle.Year * 12) + cycle.Month;
    }

    private static DateTimeOffset clampedCutoffInstant(BillingCycle cycle, int cutoffDay) {
        var day = Math.Min(cutoffDay, DateTime.DaysInMonth(cycle.Year, cycle.Month));
        return new DateTimeOffset(new DateOnly(cycle.Year, cycle.Month, day).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }

    private static Result stampBackdatedCreditorInstallments(PaymentPlan plan, DateOnly today) {
        var currentMonthOrdinal = ordinalOf(new BillingCycle(today.Year, today.Month));
        foreach(var installment in plan.Installments.OrderBy(candidate => candidate.Sequence)) {
            if(ordinalOf(installment.DueCycle) >= currentMonthOrdinal) {
                break;
            }
            var marked = installment.MarkPaid(clampedCutoffInstant(installment.DueCycle, PaymentPlan.CreditorCutoffDay));
            if(marked.IsFailure) {
                return marked;
            }
        }
        return Result.Success();
    }

    private async Task<Result> accrueAndSettleBackdatedInstallmentsAsync(
        PaymentPlan plan, CreditCard card, Guid? bankAccountId, DateOnly today, CancellationToken cancellationToken) {
        var currentMonthOrdinal = ordinalOf(new BillingCycle(today.Year, today.Month));
        foreach(var installment in plan.Installments.OrderBy(candidate => candidate.Sequence)) {
            var closeCycle = installment.Cycle;
            if(!closeCycle.IsClosedAsOf(today, card.CutoffDay)) {
                break;
            }
            var closeInstant = clampedCutoffInstant(closeCycle, card.CutoffDay);
            var statement = await context.MonthlyStatements.FirstOrDefaultAsync(
                candidate => candidate.CardId == card.Id
                    && candidate.CycleYear == closeCycle.Year
                    && candidate.CycleMonth == closeCycle.Month
                    && candidate.Currency == installment.Amount.Currency,
                cancellationToken
            );
            if(statement is null) {
                statement = MonthlyStatement.Open(card.Id, closeCycle, installment.Amount.Currency);
                context.MonthlyStatements.Add(statement);
            }
            var accrualLines = new List<PostTransactionLine> {
                new(card.ExpenseAccountId, DebitOrCredit.Debit, installment.Amount),
                new(card.LiabilityAccountId, DebitOrCredit.Credit, installment.Amount)
            };
            var accrualPosting = await ledger.PostTransactionAsync(
                new PostTransactionCommand(
                    accrualLines,
                    closeInstant,
                    InstallmentReferenceId: installment.Id,
                    SplitReferenceId: plan.SplitReferenceId
                ),
                cancellationToken
            );
            if(accrualPosting.IsFailure) {
                return Result.Failure(accrualPosting.Error);
            }
            var accrued = statement.Accrue(installment);
            if(accrued.IsFailure) {
                return accrued;
            }
            var markedAccrued = installment.MarkAccrued(closeInstant, statement);
            if(markedAccrued.IsFailure) {
                return markedAccrued;
            }
            if(ordinalOf(installment.DueCycle) >= currentMonthOrdinal) {
                continue;
            }
            var dueInstant = clampedCutoffInstant(installment.DueCycle, card.CutoffDay);
            var paymentLines = new List<PostTransactionLine> {
                new(card.LiabilityAccountId, DebitOrCredit.Debit, installment.Amount),
                new(bankAccountId!.Value, DebitOrCredit.Credit, installment.Amount)
            };
            var paymentPosting = await ledger.PostTransactionAsync(
                new PostTransactionCommand(paymentLines, dueInstant),
                cancellationToken
            );
            if(paymentPosting.IsFailure) {
                return Result.Failure(paymentPosting.Error);
            }
            var markedPaid = installment.MarkPaid(dueInstant);
            if(markedPaid.IsFailure) {
                return markedPaid;
            }
            var persistedStatementInstallments = await context.Set<Installment>()
                .Where(candidate => candidate.StatementId == statement.Id)
                .ToListAsync(cancellationToken);
            var statementInstallments = persistedStatementInstallments
                .Concat(plan.Installments.Where(candidate => candidate.StatementId == statement.Id))
                .ToList();
            if(!statement.IsPaid && statement.IsFullyPaidBy(statementInstallments)) {
                var statementPaid = statement.MarkPaid(dueInstant);
                if(statementPaid.IsFailure) {
                    return statementPaid;
                }
            }
        }
        return Result.Success();
    }
}

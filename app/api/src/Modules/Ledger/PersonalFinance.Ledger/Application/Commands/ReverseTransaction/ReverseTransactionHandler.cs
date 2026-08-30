using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.Ledger.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Application.Commands.ReverseTransaction;

/// <summary>
/// The storno is posted <b>always</b>. When the reversed
/// transaction is an installment accrual that was already paid, a compensating
/// <c>Dr CardCredit / Cr CardLiability</c> entry is posted too and the carried credit is netted
/// against the card's next statement by Financing.
/// </summary>
internal sealed class ReverseTransactionHandler(
    LedgerDbContext context,
    TransactionWriter writer,
    IFinancingApi financing,
    ILogger<ReverseTransactionHandler> logger) : ICommandHandler<ReverseTransactionCommand, ReverseTransactionResult> {
    public async Task<Result<ReverseTransactionResult>> HandleAsync(ReverseTransactionCommand command, CancellationToken cancellationToken) {
        var validation = ReverseTransactionValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        var original = await context.Transactions
            .Include(transaction => transaction.Entries)
            .FirstOrDefaultAsync(transaction => transaction.Id == command.OriginalTransactionId, cancellationToken);
        if(original is null) {
            return LedgerErrors.OriginalTransactionNotFound;
        }
        var reversal = Transaction.Reverse(original, command.ReversedOnUtc);
        if(reversal.IsFailure) {
            return reversal.Error;
        }
        var storno = await writer.PersistAsync(reversal.Value, cancellationToken);
        if(storno.IsFailure) {
            return storno.Error;
        }
        var installmentReference = original.InstallmentReference;
        var hasSplitReference = original.SplitReference is not null;
        InstallmentStatusResponse? status = null;
        if(installmentReference is not null) {
            status = await financing.GetInstallmentStatusAsync(
                new GetInstallmentStatusQuery(installmentReference.Value), cancellationToken);
        }
        var decision = ReversalCalculator.Decide(installmentReference is not null, status, hasSplitReference);
        var compensatingEntryPosted = false;
        if(decision.PostCompensating) {
            var amount = Money.FromMinorUnits(decision.CompensatingAmount, Currency.Reference);
            var lines = new List<EntryDraft> {
                new(decision.CompensatingDebitAccountId, DebitOrCredit.Debit, amount),
                new(decision.CompensatingCreditAccountId, DebitOrCredit.Credit, amount)
            };
            var compensating = Transaction.Post(lines, command.ReversedOnUtc, splitReference: null, installmentReference: installmentReference);
            if(compensating.IsFailure) {
                return compensating.Error;
            }
            var compensatingPersisted = await writer.PersistAsync(compensating.Value, cancellationToken);
            if(compensatingPersisted.IsFailure) {
                return compensatingPersisted.Error;
            }
            compensatingEntryPosted = true;
        }
        if(decision.MarkReversed && installmentReference is not null) {
            var marked = await financing.MarkInstallmentReversedAsync(
                new MarkInstallmentReversedCommand(
                    installmentReference.Value,
                    compensatingEntryPosted,
                    decision.CompensatingAmount
                ),
                cancellationToken
            );
            if(marked.IsFailure) {
                logger.LogWarning(
                    "Reversal {ReversalId} of transaction {OriginalId} is posted, but flagging installment {InstallmentId} reversed in Financing failed ({ErrorCode}). Manual reconciliation required.",
                    storno.Value, original.Id, installmentReference.Value, marked.Error.Code);
            }
        }
        if(decision.CorrectParty) {
            // TODO(Phase 6): wire IPartiesApi.CorrectExpenseSplitAsync(original.SplitReference!.Value, decision.CompensatingAmount)
            // to net the reversed split against the third party's running balance (RNF-10). Seam only in Phase 4.
        }
        return new ReverseTransactionResult(storno.Value, compensatingEntryPosted);
    }
}

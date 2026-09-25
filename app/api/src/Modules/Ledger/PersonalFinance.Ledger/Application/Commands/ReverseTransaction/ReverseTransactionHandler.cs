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
using PersonalFinance.Parties.Contracts;
using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Application.Commands.ReverseTransaction;

internal sealed class ReverseTransactionHandler(
    LedgerDbContext context,
    TransactionWriter writer,
    IFinancingApi financing,
    IPartiesApi parties,
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
        var alreadyReversed = await context.Transactions.AnyAsync(transaction => transaction.OriginalTransactionId == original.Id, cancellationToken);
        if(alreadyReversed) {
            return LedgerErrors.TransactionAlreadyReversed;
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

        if(!decision.CorrectParty) return new ReverseTransactionResult(storno.Value, compensatingEntryPosted);
        var reversedReceivableMinorUnits = await sumReversedReceivableAsync(original, cancellationToken);
        var correction = await parties.CorrectExpenseSplitAsync(
            new CorrectExpenseSplitCommand(
                original.SplitReference!.Value,
                installmentReference?.Value,
                reversedReceivableMinorUnits,
                command.ReversedOnUtc
            ),
            cancellationToken
        );
        if(correction.IsFailure) {
            logger.LogWarning(
                "Reversal {ReversalId} of transaction {OriginalId} is posted, but correcting split {SplitReferenceId} in Parties failed ({ErrorCode}). The party balance still reflects the storno; only the split metadata is stale.",
                storno.Value, original.Id, original.SplitReference.Value, correction.Error.Code);
        }
        return new ReverseTransactionResult(storno.Value, compensatingEntryPosted);
    }

    // The party portion the storno just unwound: the debit legs of the reversed transaction that
    // landed on Receivable-kind accounts. The storno already credited these accounts back, so
    // Parties only needs this figure to correct its own split metadata (RNF-5, no ledger post).
    private async Task<long> sumReversedReceivableAsync(Transaction original, CancellationToken cancellationToken) {
        var debitedAccountIds = original.Entries
            .Where(entry => entry.Direction == DebitOrCredit.Debit)
            .Select(entry => entry.AccountId)
            .ToHashSet();
        if(debitedAccountIds.Count == 0) {
            return 0;
        }
        var receivableAccountIds = await context.Accounts
            .Where(account => debitedAccountIds.Contains(account.Id) && account.Kind == AccountKind.Receivable)
            .Select(account => account.Id)
            .ToListAsync(cancellationToken);
        if(receivableAccountIds.Count == 0) {
            return 0;
        }
        var receivableAccountIdSet = receivableAccountIds.ToHashSet();
        return original.Entries
            .Where(entry => entry.Direction == DebitOrCredit.Debit && receivableAccountIdSet.Contains(entry.AccountId))
            .Sum(entry => entry.Amount.MinorUnits);
    }
}

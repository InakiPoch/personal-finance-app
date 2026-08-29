using PersonalFinance.SharedKernel;

namespace PersonalFinance.Abstractions.Messaging;

/// <summary>
/// Handles exactly one <typeparamref name="TCommand"/>.
/// </summary>
/// <typeparam name="TCommand">The command this handler executes.</typeparam>
public interface ICommandHandler<in TCommand> where TCommand : ICommand {
    Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Handles exactly one <typeparamref name="TCommand"/> and, on success, produces a <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TCommand">The command this handler executes.</typeparam>
/// <typeparam name="TResult">The value produced on success.</typeparam>
public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult> {
    Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

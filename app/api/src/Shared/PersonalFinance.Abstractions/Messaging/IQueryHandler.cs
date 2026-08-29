namespace PersonalFinance.Abstractions.Messaging;

/// <summary>
/// Handles exactly one <typeparamref name="TQuery"/>.
/// </summary>
/// <typeparam name="TQuery">The query this handler answers.</typeparam>
/// <typeparam name="TResult">The value returned to the caller.</typeparam>
public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult> {
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}

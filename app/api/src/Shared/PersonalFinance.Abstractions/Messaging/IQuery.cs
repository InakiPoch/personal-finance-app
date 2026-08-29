namespace PersonalFinance.Abstractions.Messaging;

/// <summary>
/// Marker for a read-only request that returns <typeparamref name="TResult"/>,
/// handled by a single <see cref="IQueryHandler{TQuery, TResult}"/>.
/// </summary>
/// <typeparam name="TResult">The value returned to the caller.</typeparam>
public interface IQuery<TResult> { }

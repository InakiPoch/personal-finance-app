using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Infrastructure.Messaging;

/// <summary>
/// Sends a query to its single <see cref="IQueryHandler{TQuery, TResult}"/>, resolved from DI by
/// the query's runtime type. In-process and read-only — a handler never mutates state (CQRS).
/// </summary>
public interface IQueryBus {
    Task<TResult> AskAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
}

public sealed class QueryBus(IServiceProvider serviceProvider) : IQueryBus {
    public Task<TResult> AskAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
        var handler = serviceProvider.GetRequiredService(handlerType);
        var method = handlerType.GetMethod("HandleAsync") ?? throw new InvalidOperationException($"{handlerType} has no HandleAsync method.");
        return (Task<TResult>)(method.Invoke(handler, [query, cancellationToken]) ?? throw new InvalidOperationException($"{handlerType}.HandleAsync returned null."));
    }
}

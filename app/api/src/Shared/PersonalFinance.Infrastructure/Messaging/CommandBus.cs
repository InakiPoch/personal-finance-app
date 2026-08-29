using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Infrastructure.Messaging;

/// <summary>
/// Sends a command to its single <see cref="ICommandHandler{TCommand}"/>
/// </summary>
public interface ICommandBus {
    Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken = default);
    Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default);
}

public sealed class CommandBus(IServiceProvider serviceProvider) : ICommandBus {
    public Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(command);
        var handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());
        return (Task<Result>)InvokeHandler(handlerType, command, cancellationToken);
    }

    public Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(command);
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResult));
        return (Task<Result<TResult>>)InvokeHandler(handlerType, command, cancellationToken);
    }

    private object InvokeHandler(Type handlerType, object command, CancellationToken cancellationToken) {
        var handler = serviceProvider.GetRequiredService(handlerType);
        var method = handlerType.GetMethod("HandleAsync")
            ?? throw new InvalidOperationException($"{handlerType} has no HandleAsync method.");
        return method.Invoke(handler, [command, cancellationToken])
            ?? throw new InvalidOperationException($"{handlerType}.HandleAsync returned null.");
    }
}

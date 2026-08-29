namespace PersonalFinance.Abstractions.Messaging;

/// <summary>
/// Marker for a state-changing request handled in-process by a single
/// <see cref="ICommandHandler{TCommand}"/>.
/// </summary>
public interface ICommand { }

/// <summary>
/// Marker for a state-changing request that yields <typeparamref name="TResult"/>
/// on success, handled in-process by a single
/// <see cref="ICommandHandler{TCommand, TResult}"/>.
/// </summary>
/// <typeparam name="TResult">The value produced on success.</typeparam>
public interface ICommand<TResult> { }

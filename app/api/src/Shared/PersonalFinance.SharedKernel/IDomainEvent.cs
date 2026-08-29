namespace PersonalFinance.SharedKernel;

/// <summary>
/// A fact raised by an <see cref="AggregateRoot{TId}"/> while handling a command, dispatched
/// in-process within the same module after the unit of work commits.
/// </summary>
public interface IDomainEvent { }

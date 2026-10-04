namespace PersonalFinance.SharedKernel;

/// <summary>
///  Collects the <see cref="IDomainEvent"/>s raised while handling a command; the unit of work publishes and then clears them once it commits.
/// </summary>
/// <typeparam name="TId">The identifier type.</typeparam>
public abstract class AggregateRoot<TId>(TId id) : Entity<TId>(id) where TId : notnull {
    public IReadOnlyCollection<IDomainEvent> DomainEvents => domainEvents;

    private readonly List<IDomainEvent> domainEvents = [];

    public void ClearDomainEvents() {
        domainEvents.Clear();
    }

    protected void RaiseDomainEvent(IDomainEvent domainEvent) {
        domainEvents.Add(domainEvent);
    }
}

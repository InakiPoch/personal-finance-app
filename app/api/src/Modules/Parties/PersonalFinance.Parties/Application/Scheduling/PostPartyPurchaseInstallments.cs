using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PersonalFinance.Infrastructure.Scheduling;

namespace PersonalFinance.Parties.Application.Scheduling;

internal sealed class PostPartyPurchaseInstallments(IServiceScopeFactory scopeFactory, ILogger<PostPartyPurchaseInstallments> logger, TimeProvider timeProvider) : SchedulerBase(logger) {
    protected override TimeSpan Interval => TimeSpan.FromMinutes(1);
    protected override bool RunOnStartup => true;

    protected override async Task TickAsync(CancellationToken cancellationToken) {
        using var scope = scopeFactory.CreateScope();
        var poster = scope.ServiceProvider.GetRequiredService<PartyPurchaseInstallmentPoster>();
        await poster.PostDueAsync(DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime), null, cancellationToken);
    }
}

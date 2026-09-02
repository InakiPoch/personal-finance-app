using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Infrastructure.Outbox;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Infrastructure.DependencyInjection;

/// <summary>
/// Wires the shared substrate: options, the SQLite connection factory, the in-process buses, and the integration-event dispatcher.
/// </summary>
public static class InfrastructureServiceCollectionExtensions {
    extension(IServiceCollection services) {
        public IServiceCollection AddSharedInfrastructure(IConfiguration configuration) {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);
            services.AddOptions<SqliteOptions>()
                .Bind(configuration.GetSection(SqliteOptions.SectionName))
                .PostConfigure(options =>
                    options.ConnectionString = SqliteConnectionStringHelper.Resolve(
                        configuration.GetConnectionString(SqliteConnectionStringHelper.ConnectionName)))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
            services.TryAddScoped<ICommandBus, CommandBus>();
            services.TryAddScoped<IQueryBus, QueryBus>();
            services.TryAddScoped<IIntegrationEventDispatcher, IntegrationEventDispatcher>();
            return services;
        }

        public IServiceCollection AddOutboxProcessing() {
            ArgumentNullException.ThrowIfNull(services);
            services.AddHostedService<OutboxWorker>();
            services.AddHealthChecks().AddCheck<OutboxHealthCheck>("outbox");
            return services;
        }
    }
}

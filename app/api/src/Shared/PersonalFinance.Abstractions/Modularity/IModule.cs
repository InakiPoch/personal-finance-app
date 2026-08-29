using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PersonalFinance.Abstractions.Modularity;

/// <summary>
/// Implemented once per bounded context. The Bootstrap host discovers every
/// <see cref="IModule"/>, calls <see cref="Register"/>, then <see cref="MapEndpoints"/>.
/// </summary>
public interface IModule {
    string Name { get; }
    void Register(IServiceCollection services, IConfiguration configuration);
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}

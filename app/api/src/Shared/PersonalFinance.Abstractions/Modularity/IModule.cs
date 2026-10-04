using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PersonalFinance.Abstractions.Modularity;

/// <summary>
/// Implemented once per bounded context. The Bootstrap host discovers every
/// <see cref="IModule"/> and calls <see cref="Register"/>. HTTP routes are host-owned
/// (see <c>src/Bootstrap/PersonalFinance.Api/Endpoints/</c>), not mapped by the module.
/// </summary>
public interface IModule {
    string Name { get; }
    void Register(IServiceCollection services, IConfiguration configuration);
}

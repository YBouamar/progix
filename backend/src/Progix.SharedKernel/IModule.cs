using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Progix.SharedKernel;

/// <summary>
/// Contrat d'un module du monolithe modulaire. Chaque module enregistre ses services,
/// expose ses endpoints et initialise ses données (migrations, données de référence).
/// </summary>
public interface IModule
{
    string Name { get; }
    void AddServices(IServiceCollection services, IConfiguration configuration);
    void MapEndpoints(IEndpointRouteBuilder app);
    Task InitializeAsync(IServiceProvider services, CancellationToken ct);
}

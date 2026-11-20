using Artefacts.Application.Artefacts;
using Artefacts.Application.Media;
using Artefacts.Application.Regions;
using Microsoft.Extensions.DependencyInjection;

namespace Artefacts.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ArtefactCommandHandler>();
        services.AddScoped<ArtefactDetailQueryHandler>();
        services.AddSingleton<HistoricalDateParseHandler>();
        services.AddScoped<RegionHandler>();
        services.AddScoped<ImageHandler>();
        return services;
    }
}

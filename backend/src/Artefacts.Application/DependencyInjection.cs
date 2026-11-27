using Artefacts.Application.Artefacts;
using Artefacts.Application.Imports;
using Artefacts.Application.Media;
using Artefacts.Application.Languages;
using Artefacts.Application.Lists;
using Artefacts.Application.ObjectTypes;
using Artefacts.Application.Pages;
using Artefacts.Application.Parties;
using Artefacts.Application.Queries;
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
        services.AddScoped<ObjectTypeHandler>();
        services.AddScoped<Units.UnitOfMeasureHandler>();
        services.AddScoped<ListHandler>();
        services.AddScoped<PartyHandler>();
        services.AddScoped<LanguageHandler>();
        services.AddScoped<ImageHandler>();
        services.AddScoped<ImportHandler>();
        services.AddScoped<ImportTemplateHandler>();
        services.AddScoped<ImportDetectorHandler>();
        services.AddScoped<ImportLookupHandler>();
        services.AddScoped<StoredQueryHandler>();
        services.AddScoped<StoredPageValidator>();
        services.AddScoped<StoredPageHandler>();
        services.AddSingleton<ImportProcessor>();
        return services;
    }
}

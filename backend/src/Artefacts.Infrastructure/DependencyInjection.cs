using Artefacts.Application.Abstractions;
using Artefacts.Application.Imports;
using Artefacts.Application.Media;
using Artefacts.Infrastructure.Imports;
using Artefacts.Infrastructure.Imaging;
using Artefacts.Infrastructure.Persistence;
using Artefacts.Infrastructure.Read;
using Artefacts.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Artefacts.Infrastructure.Graph;

namespace Artefacts.Infrastructure;

public sealed class ConnectionStringOptions
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Configuration value 'ConnectionStrings:Collection' is required.")]
    public string? Collection { get; set; }
}

public static class DependencyInjection
{
    public const string ConnectionStringName = "Collection";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Validated when the host starts (not at registration), so tooling such as build-time
        // OpenAPI generation can build the host without a database.
        services.AddOptions<ConnectionStringOptions>().Bind(configuration.GetSection("ConnectionStrings"))
            .ValidateDataAnnotations().ValidateOnStart();
        string ConnectionString() => configuration.GetConnectionString(ConnectionStringName)
                                     ?? throw new InvalidOperationException(
                                         $"Configuration value 'ConnectionStrings:{ConnectionStringName}' is required.");

        services.AddDbContext<CollectionDbContext>(o => o
            .UseNpgsql(ConnectionString(), npg => npg.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention());
        // A factory as well as the scoped context: GraphQL resolvers and DataLoaders run in parallel and each takes its own.
        services.AddDbContextFactory<ReadDbContext>(o => o
            .UseNpgsql(ConnectionString())
            .UseSnakeCaseNamingConvention()
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        services.AddScoped<IArtefactRepository, ArtefactRepository>();
        services.AddScoped<IRegionRepository, RegionRepository>();
        services.AddScoped<IObjectTypeRepository, ObjectTypeRepository>();
        services.AddScoped<IReferenceEntryRepository, ReferenceEntryRepository>();
        services.AddScoped<IPartyRepository, PartyRepository>();
        services.AddScoped<IStoredQueryRepository, StoredQueryRepository>();
        services.AddScoped<ILanguageRepository, LanguageRepository>();
        services.AddSingleton<IContentLanguages, ContentLanguages>();
        services.AddScoped<IMediaAssetRepository, MediaAssetRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IArtefactListQuery, ArtefactListQuery>();
        services.AddSingleton<IFieldCatalog, FieldCatalog>();
        services.AddSingleton<Graph.IStoredQueryDocuments, Graph.DatabaseStoredQueryDocuments>();
        services.AddOptions<GraphOptions>().Bind(configuration.GetSection(GraphOptions.Section));
        services.AddSingleton<Graph.StoredQueryValidator>();
        services.AddSingleton<IStoredQueryDescriber, Graph.StoredQueryDescriber>();
        services.AddSingleton<IStoredQueryValidator>(sp => sp.GetRequiredService<Graph.StoredQueryValidator>());
        services.AddHostedService<Graph.SystemQuerySync>();
        services.AddArtefactsGraph();
        services.AddScoped<IImportRepository, ImportRepository>();
        services.AddScoped<IImportTemplateRepository, ImportTemplateRepository>();
        services.AddSingleton<IImportSignal, ImportSignal>();
        services.AddSingleton<IWorkbookReader, OpenXmlWorkbookReader>();
        services.AddSingleton<IImportReportWriter, XlsxImportReportWriter>();
        services.AddOptions<ImportOptions>().Bind(configuration.GetSection(ImportOptions.Section));
        services.AddOptions<ImportWorkerOptions>().Bind(configuration.GetSection("Imports:Worker"));
        services.AddHostedService<ImportWorker>();

        services.AddOptions<MediaOptions>().Bind(configuration.GetSection(MediaOptions.Section)).ValidateOnStart();
        services.AddOptions<StorageOptions>().Bind(configuration.GetSection(StorageOptions.Section))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<ImgproxyOptions>().Bind(configuration.GetSection(ImgproxyOptions.Section))
            .ValidateDataAnnotations().ValidateOnStart();

        // Chosen at resolution time (not registration time) so configuration overrides applied
        // after Program runs, e.g. by WebApplicationFactory in tests, are honoured.
        services.AddSingleton<FileSystemMediaStorage>();
        services.AddSingleton<S3MediaStorage>();
        services.AddSingleton<IMediaStorage>(sp =>
            sp.GetRequiredService<IOptions<StorageOptions>>().Value.Provider == StorageProvider.FileSystem
                ? sp.GetRequiredService<FileSystemMediaStorage>()
                : sp.GetRequiredService<S3MediaStorage>());

        services.AddSingleton<IImageUrlBuilder, ImgproxyUrlBuilder>();
        return services;
    }
}

using System.Text.Json.Serialization;
using Artefacts.Api;
using Artefacts.Application;
using Artefacts.Infrastructure;
using Artefacts.Infrastructure.Graph;
using Artefacts.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Build-time OpenAPI generation (GetDocument.Insider) builds the host without real configuration;
// give it placeholders so startup validation passes. Never used when the app actually runs.
if (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider")
{
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:Collection"] = "Host=openapi-generation",
        ["Storage:Provider"] = "FileSystem",
        ["Storage:FileSystem:Root"] = Path.GetTempPath(),
        ["Imgproxy:PublicBaseUrl"] = "http://openapi-generation",
        ["Imgproxy:Key"] = "00",
        ["Imgproxy:Salt"] = "00",
        ["GraphQL:SyncSystemQueries"] = "false",
    });
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<RequestLanguages>();
builder.Services.AddScoped<Artefacts.Application.Abstractions.ILanguagePreferences>(sp => sp.GetRequiredService<RequestLanguages>());

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict; // numbers are numbers in the API contract
});
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = Endpoints.MaxUploadRequestBytes);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddOpenApi(o => o.AddOperationTransformer<QueryEnumOperationTransformer>());

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseWhen(context => context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/graphql"),
    api => api.UseRequestLanguages());

app.UseStoredQueries("/graphql");

app.MapOpenApi();
if (app.Environment.IsDevelopment())
    app.MapScalarApiReference();

app.MapArtefactEndpoints();
app.MapRegionEndpoints();
app.MapObjectTypeEndpoints();
app.MapListEndpoints();
app.MapPartyEndpoints();
app.MapLanguageEndpoints();
app.MapImportEndpoints();
app.MapQueryEndpoints();
app.MapPageEndpoints();
app.MapSupportEndpoints();
app.MapGraphQL("/graphql");

// Build step (see Artefacts.Api.csproj): write the GraphQL schema as SDL, next to the OpenAPI document, and exit.
if (args is ["--export-graphql-schema", var schemaPath])
{
    var executor = await app.Services.GetRequiredService<HotChocolate.Execution.IRequestExecutorProvider>().GetExecutorAsync();
    await File.WriteAllTextAsync(schemaPath, executor.Schema.ToString() + "\n");
    return;
}

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<CollectionDbContext>().Database.MigrateAsync();
}

app.Run();

public partial class Program;

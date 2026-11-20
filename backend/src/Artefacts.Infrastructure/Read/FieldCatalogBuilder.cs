using System.Reflection;
using Artefacts.Application.Abstractions;
using Artefacts.Application.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Artefacts.Infrastructure.Read;

/// <summary>
/// Builds the field catalog from the <see cref="ReadDbContext"/> EF model plus catalog attributes, so the
/// catalog can never describe columns the read views do not expose (and vice versa).
/// </summary>
public static class FieldCatalogBuilder
{
    public static FieldCatalogDto Build(IModel model)
    {
        var entities = new List<EntityDto>();
        foreach (var entityType in model.GetEntityTypes().OrderBy(e => e.ClrType.Name))
        {
            var entityAttr = entityType.ClrType.GetCustomAttribute<CatalogEntityAttribute>()
                             ?? throw new InvalidOperationException($"{entityType.ClrType.Name} lacks [CatalogEntity].");
            var view = entityType.GetViewName()
                       ?? throw new InvalidOperationException($"{entityType.ClrType.Name} is not mapped to a view.");
            var store = StoreObjectIdentifier.View(view, entityType.GetViewSchema());

            var fields = new Dictionary<string, (CatalogFieldAttribute Attr, PropertyInfo Prop, List<string> Columns)>();
            var parts = new List<(string FieldId, string Column, PropertyInfo Prop)>();

            foreach (var property in entityType.GetProperties())
            {
                var info = property.PropertyInfo
                           ?? throw new InvalidOperationException($"{entityType.ClrType.Name}.{property.Name} is a shadow property.");
                var column = property.GetColumnName(store)!;
                var fieldAttr = info.GetCustomAttribute<CatalogFieldAttribute>();
                var partOf = info.GetCustomAttribute<PartOfAttribute>();

                if (fieldAttr is not null)
                    fields.Add(fieldAttr.Id, (fieldAttr, info, [column]));
                else if (partOf is not null)
                    parts.Add((partOf.FieldId, column, info));
                else
                    throw new InvalidOperationException(
                        $"{entityType.ClrType.Name}.{property.Name} has neither [CatalogField] nor [PartOf]; every read column must be described.");
            }

            foreach (var (fieldId, column, prop) in parts)
            {
                if (!fields.TryGetValue(fieldId, out var field))
                    throw new InvalidOperationException($"{prop.Name} is [PartOf(\"{fieldId}\")] but no such field exists.");
                field.Columns.Add(column);
            }

            entities.Add(new EntityDto(
                entityAttr.Id,
                entityAttr.Label,
                view,
                fields.Values.Select(f => new FieldDto(
                    f.Attr.Id,
                    f.Attr.Label,
                    f.Attr.Type,
                    f.Prop.IsDefined(typeof(FilterableAttribute)),
                    f.Prop.IsDefined(typeof(SortableAttribute)),
                    f.Prop.GetCustomAttribute<SensitiveAttribute>()?.Classification,
                    f.Columns)).ToList()));
        }

        return new FieldCatalogDto(entities);
    }
}

/// <summary>Builds the catalog once (the model is static for the lifetime of the process).</summary>
internal sealed class FieldCatalog(IServiceScopeFactory scopes) : IFieldCatalog
{
    private readonly Lazy<FieldCatalogDto> _catalog = new(() =>
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReadDbContext>();
        return FieldCatalogBuilder.Build(db.Model);
    });

    public FieldCatalogDto Get() => _catalog.Value;
}

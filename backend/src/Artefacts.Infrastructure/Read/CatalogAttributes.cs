using Artefacts.Application.Contracts;

namespace Artefacts.Infrastructure.Read;

/// <summary>Marks a read-model class as a catalog entity (one per read view).</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class CatalogEntityAttribute(string id, string label) : Attribute
{
    public string Id { get; } = id;
    public string Label { get; } = label;
}

/// <summary>
/// Declares a logical catalog field. The annotated property is the field's primary column; composite
/// fields (historical date, money) add their other columns with <see cref="PartOfAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CatalogFieldAttribute(string id, string label, FieldType type) : Attribute
{
    public string Id { get; } = id;
    public string Label { get; } = label;
    public FieldType Type { get; } = type;
}

/// <summary>Additional column belonging to a composite catalog field.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PartOfAttribute(string fieldId) : Attribute
{
    public string FieldId { get; } = fieldId;
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class FilterableAttribute : Attribute;

[AttributeUsage(AttributeTargets.Property)]
public sealed class SortableAttribute : Attribute;

/// <summary>
/// Classifies a field as sensitive (e.g. <c>financial</c>). Metadata only for now; the roles feature
/// enforces it in DTO serialisation and the query compiler.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SensitiveAttribute(string classification) : Attribute
{
    public string Classification { get; } = classification;
}

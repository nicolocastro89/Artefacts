using System.Reflection;
using Artefacts.Infrastructure.Read;
using HotChocolate.Data.Filters;
using HotChocolate.Data.Sorting;

namespace Artefacts.Infrastructure.Graph;

/// <summary>One group of the current search: its key and label are null for the final group of artefacts without a value.</summary>
public sealed record ArtefactGroup(string? Key, string? Label, int Count);

/// <summary>A page of artefacts, with every group of the whole search when the list is grouped.</summary>
public sealed record ArtefactPage(IReadOnlyList<Artefact> Items, int Total, int Page, int PageSize, IReadOnlyList<ArtefactGroup> Groups);

/// <summary>
/// The fields an artefact can be filtered on: exactly the properties of the artefact read view marked
/// <see cref="FilterableAttribute"/>, the same marks the field catalog is built from.
/// </summary>
public sealed class ArtefactFilterInputType : FilterInputType<ArtefactRow>
{
    protected override void Configure(IFilterInputTypeDescriptor<ArtefactRow> descriptor)
    {
        descriptor.Name("ArtefactFilterInput");
        descriptor.BindFieldsImplicitly();
        foreach (var property in Unmarked<FilterableAttribute>()) descriptor.Ignore(property.Name);
    }

    internal static IEnumerable<PropertyInfo> Unmarked<TMark>() where TMark : Attribute =>
        typeof(ArtefactRow).GetProperties().Where(p => p.GetCustomAttribute<TMark>() is null);
}

/// <summary>The fields artefacts can be sorted by: the properties marked <see cref="SortableAttribute"/>.</summary>
public sealed class ArtefactSortInputType : SortInputType<ArtefactRow>
{
    protected override void Configure(ISortInputTypeDescriptor<ArtefactRow> descriptor)
    {
        descriptor.Name("ArtefactSortInput");
        descriptor.BindFieldsImplicitly();
        foreach (var property in ArtefactFilterInputType.Unmarked<SortableAttribute>()) descriptor.Ignore(property.Name);
    }
}

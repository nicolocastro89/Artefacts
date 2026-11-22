namespace Artefacts.Application.Contracts;

/// <summary>
/// What a query builder needs to know, derived from the running GraphQL schema: the roots a query starts from, the
/// fields of each object type, the enums, and the limits queries run under.
/// </summary>
public sealed record BuilderRulesDto(
    IReadOnlyList<BuilderRootDto> Roots,
    IReadOnlyDictionary<string, BuilderTypeDto> Types,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Enums,
    BuilderLimitsDto Limits);

/// <summary>
/// A field of the query type. A root that takes a <c>where</c> argument has <see cref="Filter"/> (the fields it can be
/// filtered on and the operators of each); one that takes <c>order</c> has <see cref="Sort"/>.
/// </summary>
public sealed record BuilderRootDto(
    string Name,
    string Type,
    bool List,
    IReadOnlyList<BuilderArgumentDto> Arguments,
    IReadOnlyList<BuilderFilterFieldDto>? Filter,
    IReadOnlyList<BuilderSortFieldDto>? Sort);

/// <param name="Type">The GraphQL type, without list or non-null marks (<c>Int</c>, <c>ArtefactSort</c>).</param>
/// <param name="Kind"><c>scalar</c>, <c>enum</c> or <c>input</c>.</param>
public sealed record BuilderArgumentDto(string Name, string Type, string Kind, bool List, bool Required, string? Default);

public sealed record BuilderTypeDto(IReadOnlyList<BuilderFieldDto> Fields);

/// <param name="Type">The GraphQL type, without list or non-null marks.</param>
/// <param name="Kind"><c>object</c>, <c>scalar</c> or <c>enum</c>.</param>
/// <param name="ValueType">The value type a page binds to (text, number, image, ...); null for objects that are walked into.</param>
/// <param name="Sensitivity">The classification of a sensitive field (<c>financial</c>), else null.</param>
public sealed record BuilderFieldDto(string Name, string Type, string Kind, bool List, bool Required, string? ValueType, string? Sensitivity);

/// <param name="GraphQlType">The GraphQL scalar a condition's value has, for declaring a variable (<c>String</c>, <c>Int</c>).</param>
public sealed record BuilderFilterFieldDto(string Name, string Label, string ValueType, string GraphQlType, IReadOnlyList<BuilderOperatorDto> Operators);

/// <param name="List">Whether the operator takes a list of values (<c>in</c>, <c>nin</c>).</param>
public sealed record BuilderOperatorDto(string Name, bool List);

public sealed record BuilderSortFieldDto(string Name, string Label);

public sealed record BuilderLimitsDto(int MaxPageSize, int MaxDepth, double MaxFieldCost, double MaxTypeCost);

/// <summary>A document described without being stored: its description when valid, else its problems with positions.</summary>
public sealed record DocumentDescriptionDto(QueryDescriptionDto? Description, IReadOnlyList<QueryProblemDto> Problems);

public sealed record DescribeDocumentRequest(string? Document);

public sealed record QueryProblemDto(string Message, int? Line, int? Column);

namespace Artefacts.Application.Contracts;

/// <summary>
/// What a stored query returns and takes. <see cref="Fields"/> maps each value's path in the result data (fields joined
/// by <c>.</c>, list items as <c>[]</c>, aliases as the field name) to its value type: text, number, date,
/// historical-date, money, image, reference, boolean, map or list. The types follow the same rules as the descriptions
/// of the REST responses.
/// </summary>
public sealed record QueryDescriptionDto(
    IReadOnlyDictionary<string, string> Fields, IReadOnlyDictionary<string, QueryVariableDto> Variables);

public sealed record QueryVariableDto(string Type, bool Required);

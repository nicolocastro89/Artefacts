namespace Artefacts.Domain.Common;

/// <summary>Base type for all expected, business-rule failures raised by the domain or application layer.</summary>
public abstract class DomainException(string message) : Exception(message);

/// <summary>
/// One or more inputs violate a business rule. Keys are camelCase field paths
/// (e.g. <c>inventoryNumber</c>, <c>provenance[0].regionId</c>) so the API can attach errors to form fields.
/// </summary>
public sealed class DomainValidationException : DomainException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public DomainValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] })
    {
    }

    public DomainValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base(string.Join("; ", errors.Select(e => $"{e.Key}: {string.Join(", ", e.Value)}")))
    {
        Errors = errors;
    }

    /// <summary>Returns a copy whose field keys are prefixed, e.g. <c>period</c> → <c>provenance[0].period</c>.</summary>
    public DomainValidationException WithPrefix(string prefix) =>
        new(Errors.ToDictionary(
            e => string.IsNullOrEmpty(e.Key) ? prefix : $"{prefix}.{e.Key}",
            e => e.Value));
}

/// <summary>The requested resource does not exist.</summary>
public sealed class NotFoundException(string resource, object id)
    : DomainException($"{resource} '{id}' was not found.")
{
    public string Resource { get; } = resource;
}

/// <summary>The request is valid but conflicts with the current state (e.g. deleting a region that is in use).</summary>
public sealed class ConflictException(string code, string message) : DomainException(message)
{
    public string Code { get; } = code;
}

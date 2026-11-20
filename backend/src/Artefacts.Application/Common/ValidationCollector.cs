using Artefacts.Domain.Common;

namespace Artefacts.Application.Common;

/// <summary>
/// Collects validation errors from several domain operations so a request reports all problems at once,
/// with field keys prefixed to match the request shape (e.g. <c>provenance[1].period</c>).
/// </summary>
internal sealed class ValidationCollector
{
    private readonly Dictionary<string, string[]> _errors = new();

    public bool HasErrors => _errors.Count > 0;

    public T? Try<T>(string prefix, Func<T> action)
    {
        try
        {
            return action();
        }
        catch (DomainValidationException e)
        {
            Add(prefix.Length == 0 ? e : e.WithPrefix(prefix));
            return default;
        }
    }

    public void Try(string prefix, Action action) => Try<object?>(prefix, () =>
    {
        action();
        return null;
    });

    public void Add(string field, string message) => _errors[field] = [.. _errors.GetValueOrDefault(field) ?? [], message];

    private void Add(DomainValidationException e)
    {
        foreach (var (k, v) in e.Errors) _errors[k] = [.. _errors.GetValueOrDefault(k) ?? [], .. v];
    }

    public void ThrowIfAny()
    {
        if (HasErrors) throw new DomainValidationException(_errors);
    }
}

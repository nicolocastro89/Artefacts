namespace Artefacts.Domain.Common;

internal static class Guard
{
    /// <summary>Trims the value and returns null when it is empty.</summary>
    public static string? Optional(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (trimmed.Length > maxLength)
            throw new DomainValidationException(field, $"Must be at most {maxLength} characters.");
        return trimmed;
    }

    public static string Required(string? value, string field, int maxLength)
    {
        var trimmed = Optional(value, field, maxLength);
        return trimmed ?? throw new DomainValidationException(field, "Is required.");
    }
}

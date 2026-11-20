using Artefacts.Domain.Common;

namespace Artefacts.Domain.Finance;

/// <summary>An amount in a specific ISO 4217 currency. Classified as financially sensitive where it is exposed.</summary>
public sealed class Money : IEquatable<Money>
{
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = default!;

    // For EF Core.
    private Money()
    {
    }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <exception cref="DomainValidationException">Keyed <c>amount</c> or <c>currency</c>.</exception>
    public static Money Create(decimal amount, string? currency)
    {
        var errors = new Dictionary<string, string[]>();
        if (amount < 0)
            errors["amount"] = ["Must not be negative."];
        if (decimal.Round(amount, 4) != amount)
            errors["amount"] = ["At most 4 decimal places are allowed."];

        var code = currency?.Trim().ToUpperInvariant() ?? "";
        if (!Iso4217.IsKnown(code))
            errors["currency"] = [$"\"{currency}\" is not a recognised ISO 4217 currency code."];

        if (errors.Count > 0) throw new DomainValidationException(errors);
        return new Money(amount, code);
    }

    public bool Equals(Money? other) => other is not null && Amount == other.Amount && Currency == other.Currency;
    public override bool Equals(object? obj) => Equals(obj as Money);
    public override int GetHashCode() => HashCode.Combine(Amount, Currency);
    public override string ToString() => $"{Amount:0.##} {Currency}";
}

using Artefacts.Domain.Common;
using Artefacts.Domain.Finance;

namespace Artefacts.Domain.Tests;

public class MoneyTests
{
    [Fact]
    public void Creates_valid_amount_and_normalises_currency()
    {
        var money = Money.Create(12500.00m, "eur");
        Assert.Equal(12500.00m, money.Amount);
        Assert.Equal("EUR", money.Currency);
    }

    [Fact]
    public void Rejects_negative_amount()
    {
        var ex = Assert.Throws<DomainValidationException>(() => Money.Create(-1m, "EUR"));
        Assert.Contains("amount", ex.Errors.Keys);
    }

    [Fact]
    public void Rejects_unknown_currency()
    {
        var ex = Assert.Throws<DomainValidationException>(() => Money.Create(10m, "XYZ"));
        Assert.Contains("currency", ex.Errors.Keys);
    }

    [Fact]
    public void Rejects_more_than_four_decimals()
    {
        Assert.Throws<DomainValidationException>(() => Money.Create(1.23456m, "USD"));
    }
}

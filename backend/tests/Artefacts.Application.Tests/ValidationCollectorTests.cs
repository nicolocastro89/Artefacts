using Artefacts.Application.Common;
using Artefacts.Domain.Common;

namespace Artefacts.Application.Tests;

public sealed class ValidationCollectorTests
{
    [Fact]
    public void Collects_errors_from_several_operations_with_prefixed_keys()
    {
        var v = new ValidationCollector();

        var title = v.Try<string>("", () => throw new DomainValidationException("title", "Required."));
        v.Try("provenance[1]", () => throw new DomainValidationException("period", "Unparseable."));
        v.Try("acquisition.price", () => throw new DomainValidationException("", "Negative."));

        Assert.Null(title);
        var e = Assert.Throws<DomainValidationException>(v.ThrowIfAny);
        Assert.Equal(["Required."], e.Errors["title"]);
        Assert.Equal(["Unparseable."], e.Errors["provenance[1].period"]);
        Assert.Equal(["Negative."], e.Errors["acquisition.price"]);
    }

    [Fact]
    public void Messages_for_the_same_field_are_merged()
    {
        var v = new ValidationCollector();

        v.Add("inventoryNumber", "Required.");
        v.Try("", () => throw new DomainValidationException("inventoryNumber", "Too long."));

        var e = Assert.Throws<DomainValidationException>(v.ThrowIfAny);
        Assert.Equal(["Required.", "Too long."], e.Errors["inventoryNumber"]);
    }

    [Fact]
    public void Successful_operations_return_their_value_and_nothing_is_thrown()
    {
        var v = new ValidationCollector();

        Assert.Equal(42, v.Try("x", () => 42));
        Assert.False(v.HasErrors);
        v.ThrowIfAny();
    }
}

using Artefacts.Api.IntegrationTests.Infrastructure;
using Artefacts.Application.Abstractions;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Common;
using Artefacts.Domain.Lists;
using Artefacts.Domain.Parties;
using Microsoft.Extensions.DependencyInjection;

namespace Artefacts.Api.IntegrationTests;

/// <summary>
/// The database constraints behind the lists and parties, reached through the repositories (bypassing the handlers'
/// own checks): unique names surface as validation errors and links block deletes as conflicts.
/// </summary>
[Collection(ApiCollection.Name)]
public class ListPersistenceTests(ApiFactory factory)
{
    private async Task<Exception?> SaveAsync(Action<IServiceProvider> change)
    {
        using var scope = factory.Services.CreateScope();
        change(scope.ServiceProvider);
        return await Record.ExceptionAsync(() => scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None));
    }

    private static Dictionary<string, string?> It(string name) => new() { ["it"] = name };

    [Fact]
    public async Task Translated_names_are_unique_per_list_and_language()
    {
        var name = HttpExtensions.Unique("Safavide");
        Assert.Null(await SaveAsync(s => s.GetRequiredService<IReferenceEntryRepository>().Add(ReferenceEntry.Create(ListKind.Dynasty, It(name)))));

        var clash = await SaveAsync(s => s.GetRequiredService<IReferenceEntryRepository>().Add(ReferenceEntry.Create(ListKind.Dynasty, It(name.ToUpperInvariant()))));
        var otherList = await SaveAsync(s => s.GetRequiredService<IReferenceEntryRepository>().Add(ReferenceEntry.Create(ListKind.CollectionGroup, It(name))));
        var otherLanguage = await SaveAsync(s => s.GetRequiredService<IReferenceEntryRepository>()
            .Add(ReferenceEntry.Create(ListKind.Dynasty, new Dictionary<string, string?> { ["en"] = name })));

        Assert.Contains("names", Assert.IsType<DomainValidationException>(clash).Errors.Keys);
        Assert.Null(otherList);
        Assert.Null(otherLanguage);
    }

    [Fact]
    public async Task Single_names_and_party_names_are_unique()
    {
        var location = HttpExtensions.Unique("Beta");
        var party = HttpExtensions.Unique("Christie's");
        Assert.Null(await SaveAsync(s => s.GetRequiredService<IReferenceEntryRepository>().Add(ReferenceEntry.Create(ListKind.StorageLocation, location))));
        Assert.Null(await SaveAsync(s => s.GetRequiredService<IPartyRepository>().Add(Party.Create(party, PartyKind.Organisation))));

        var locationClash = await SaveAsync(s => s.GetRequiredService<IReferenceEntryRepository>().Add(ReferenceEntry.Create(ListKind.StorageLocation, $" {location.ToLowerInvariant()}")));
        var partyClash = await SaveAsync(s => s.GetRequiredService<IPartyRepository>().Add(Party.Create(party.ToUpperInvariant(), PartyKind.Person)));

        Assert.Contains("name", Assert.IsType<DomainValidationException>(locationClash).Errors.Keys);
        Assert.Contains("name", Assert.IsType<DomainValidationException>(partyClash).Errors.Keys);
    }

    [Fact]
    public async Task Deleting_a_linked_entry_or_party_is_a_conflict()
    {
        var client = factory.CreateClient();
        var dynasty = await client.AddListEntryAsync("dynasties", HttpExtensions.Unique("Sung"));
        var holder = await client.AddPartyAsync(HttpExtensions.Unique("Rossetti"));
        await client.CreateArtefactAsync(new ArtefactWriteRequest(HttpExtensions.Unique("INV"), "Bowl", DynastyId: dynasty.Id, HolderId: holder.Id));

        var entry = await SaveAsync(s =>
        {
            var repository = s.GetRequiredService<IReferenceEntryRepository>();
            repository.Remove(repository.GetAsync(ListKind.Dynasty, new ReferenceEntryId(dynasty.Id), CancellationToken.None).GetAwaiter().GetResult()!);
        });
        var party = await SaveAsync(s =>
        {
            var repository = s.GetRequiredService<IPartyRepository>();
            repository.Remove(repository.GetAsync(new PartyId(holder.Id), CancellationToken.None).GetAwaiter().GetResult()!);
        });

        Assert.Equal("entry_in_use", Assert.IsType<ConflictException>(entry).Code);
        Assert.Equal("party_in_use", Assert.IsType<ConflictException>(party).Code);
    }
}

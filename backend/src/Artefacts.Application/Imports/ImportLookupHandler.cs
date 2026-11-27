using System.Text.RegularExpressions;
using Artefacts.Application.Abstractions;
using Artefacts.Application.Contracts;
using Artefacts.Domain.Common;
using Artefacts.Domain.Imports;

namespace Artefacts.Application.Imports;

/// <summary>Loads the lookup tables parsing reads: the built-in ones with the entries users added or changed.</summary>
public static class LookupTableCatalogue
{
    public static async Task<LookupTables> LoadAsync(IImportLookupRepository repository, CancellationToken ct) =>
        LookupTables.BuiltIn.With((await repository.ListAsync(ct)).Select(t => ToTable(t)));

    /// <summary>The stored entries of a table; entries that no longer fit their table are left out.</summary>
    internal static LookupTable ToTable(ImportLookupTable stored)
    {
        var builtIn = LookupTables.BuiltIn.Get(stored.TableId);
        var entries = ImportJson.Read<Dictionary<string, string>>(stored.Entries)
            .Where(e => LookupTables.Problem(stored.TableId, e.Key, e.Value) is null)
            .ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);
        return new LookupTable(stored.TableId, builtIn.Label, entries);
    }
}

/// <summary>
/// The lookup tables (month names, currency words, measurement labels, spellings of the inch) with the entries users
/// added. A user entry extends a table or replaces a built-in entry with the same word; saving a table replaces all
/// of its user entries, and saving none puts the table back to the built-in entries.
/// </summary>
public sealed class ImportLookupHandler(IImportLookupRepository tables, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public const int MaxEntries = 500;

    public async Task<IReadOnlyList<ImportLookupTableDto>> ListAsync(CancellationToken ct)
    {
        var stored = (await tables.ListAsync(ct)).ToDictionary(t => t.TableId, StringComparer.Ordinal);
        return LookupTables.BuiltIn.Tables.Select(t => ToDto(t, stored.GetValueOrDefault(t.Id))).ToList();
    }

    public async Task<ImportLookupTableDto> GetAsync(string id, CancellationToken ct)
    {
        var builtIn = Known(id);
        return ToDto(builtIn, await tables.GetAsync(id, ct));
    }

    public async Task<ImportLookupTableDto> ReplaceAsync(string id, SaveImportLookupTableRequest request, CancellationToken ct)
    {
        var builtIn = Known(id);
        var entries = Validate(id, request.Entries ?? []);
        var stored = await tables.GetAsync(id, ct);
        if (entries.Count == 0)
        {
            if (stored is not null) tables.Remove(stored);
            stored = null;
        }
        else if (stored is null) tables.Add(stored = ImportLookupTable.Create(id, ImportJson.Write(entries), clock.GetUtcNow()));
        else stored.Replace(ImportJson.Write(entries), clock.GetUtcNow());

        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(builtIn, stored);
    }

    /// <summary>The entries as word to value, or the validation problems (by <c>entries[i].word</c> and <c>.value</c>) that stop them being stored.</summary>
    private static Dictionary<string, string> Validate(string tableId, IReadOnlyList<ImportLookupEntryInput> input)
    {
        var errors = new Dictionary<string, string[]>();
        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (input.Count > MaxEntries) errors["entries"] = [$"At most {MaxEntries} entries."];
        for (var i = 0; i < Math.Min(input.Count, MaxEntries); i++)
        {
            var word = input[i].Word?.Trim() ?? "";
            var value = input[i].Value?.Trim() ?? "";
            if (LookupTables.WordProblem(word) is { } wordProblem) errors[$"entries[{i}].word"] = [wordProblem];
            else if (!entries.TryAdd(word, value)) errors[$"entries[{i}].word"] = [$"\"{word}\" is already in the list."];
            if (LookupTables.Problem(tableId, word, value) is { } problem && !errors.ContainsKey($"entries[{i}].word")) errors[$"entries[{i}].value"] = [problem];
        }

        if (errors.Count > 0) throw new DomainValidationException(errors);
        return entries;
    }

    private static LookupTable Known(string id) =>
        LookupTables.BuiltIn.Tables.FirstOrDefault(t => t.Id == id) ?? throw new NotFoundException("Lookup table", id);

    private static ImportLookupTableDto ToDto(LookupTable builtIn, ImportLookupTable? stored)
    {
        var user = stored is null ? new Dictionary<string, string>() : LookupTableCatalogue.ToTable(stored).Entries.ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);
        var entries = builtIn.Entries
            .Select(e => user.TryGetValue(e.Key, out var changed)
                ? new ImportLookupEntryDto(e.Key, changed, true, e.Value)
                : new ImportLookupEntryDto(e.Key, e.Value, false, null))
            .Concat(user.Where(e => !builtIn.Entries.ContainsKey(e.Key)).Select(e => new ImportLookupEntryDto(e.Key, e.Value, true, null)))
            .OrderBy(e => e.Word, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return new ImportLookupTableDto(builtIn.Id, builtIn.Label, entries);
    }
}

public static partial class LookupTableRules
{
    [GeneratedRegex(@"^[A-Z]{3}$")]
    internal static partial Regex CurrencyCode();
}

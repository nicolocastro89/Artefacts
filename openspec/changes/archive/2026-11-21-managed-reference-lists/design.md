# Design

## Context

See proposal.md for the motivation. Today `Artefact` stores collection group, dynasty, holder and storage location as text, and `Acquisition.Source` as text. Grouping by collection group and dynasty uses normalised-text keys (`collection_group_key`, `dynasty_key`, with expression indexes), labelled with the most frequent spelling. Object types and regions are the existing pattern for managed lists:
- a names table per language (`LocalizedName`), with uniqueness per language;
- "add" returns the existing entry when a name matches;
- delete is blocked when the entry is in use;
- names are resolved in the request's language, through SQL functions in list queries (`object_type_name`, `region_name`).

The import already curates one kind of value, places. It has:
- decisions per normalised text (link, create or keep), stored on the job and copied into templates;
- exact and similar suggestions (`PlaceMatcher`);
- regions marked "create", made once at commit and recorded on the job.

Nothing is live: the single `Initial` migration is regenerated and the development data reset.

## Goals / Non-Goals

**Goals:**
- One reusable model and API for the three plain lists, so a fourth list later is configuration rather than a copy.
- Parties as their own aggregate, ready for loans to reference as borrowers.
- Import curation that generalises the place step instead of adding four more one-off steps.

**Non-Goals:**
- Folding object types and regions into the generic lists (they work; moving them is churn).
- Party details beyond name and kind (addresses, contacts, roles), and loans themselves.
- A default holder taken from the signed-in user: there are no logins yet. An empty holder means "held by the collection".
- Keeping the sheet's wording on the artefact: the user chose link-only. The wording stays visible in the import review and report.

## Decisions

### One `ReferenceEntry` aggregate with a list kind
`ReferenceEntry { Id, Kind, Names | Name }`, where `Kind` is `Dynasty`, `CollectionGroup` or `StorageLocation`.
- **Storage:**
  - Translated kinds keep names in `reference_entry_names (entry_id, kind, language, name, normalized_name)`, unique on `(kind, language, normalized_name)`. `kind` is repeated there so the index can enforce it.
  - The untranslated kind (storage location) keeps its single name on `reference_entries (name, normalized_name)`, with a partial unique index on `(kind, normalized_name)`.
- **Domain:** the domain enforces the shape by kind: translated kinds have names in one or more languages; storage locations have exactly one name.
- **API:** `/api/lists/{kind}`, with kind one of `dynasties`, `collection-groups`, `storage-locations`. It offers GET (search, with counts), POST (add, returning the existing entry when the name matches), PUT names and DELETE (409 when in use). The DTO is `ReferenceEntryDto(Id, Kind, Name, NameLanguage?, Names?, ArtefactCount)`.

*Alternatives:*
- Three copies of the object type aggregate, rejected: triple handlers, endpoints and pages.
- Storing a storage location's single name under every language, rejected: it fakes translations and breaks per-language uniqueness.

### `Party` aggregate
`Party { Id, Name, NormalizedName (unique), Kind: Person | Organisation }`, with API `/api/parties`.
- **Endpoints:** GET (search and kind filter, with `sourceCount` and `holderCount`), POST (returns the existing party when the name matches), PUT (name and kind), DELETE (409 when in use).

Parties are a separate aggregate from the reference lists because they will grow: details, and loan borrowers.

### Artefact links
- **Artefact:** `CollectionGroupId`, `DynastyId` and `StorageLocationId` (all `ReferenceEntryId`), plus `HolderId` (`PartyId`).
- **Acquisition:** `SourceId` (`PartyId`).
- **Foreign keys** use restrict delete; the unit of work maps their violations to `ConflictException` codes `entry_in_use` and `party_in_use`.
- **The write request** carries the ids.
- **The detail DTO** returns `NamedRefDto` for each link, with the name resolved in the request's language. The holder and source are `PartyRefDto(Id, Name, Kind)`.
- **Form pickers** reuse `ReferencePicker` with a source per list and parties. Adding a party inline asks for its kind; the default is organisation.

### Read model, search and grouping
- **`v_artefact`** carries the ids instead of the texts.
- **Name resolution:** a new STABLE SQL function `reference_entry_name(uuid, text[])` resolves translated names the way `object_type_name` does. Storage location and party names come from a join, since they have a single name.
- **Grouping** by collection group and dynasty groups by entry id, ordered and labelled by the resolved name. The `collection_group_key` and `dynasty_key` expression indexes and the most-frequent-spelling labelling are removed.
- **Search** adds `EXISTS` clauses on entry names in any language and on party names, replacing the text `LIKE` on the removed columns.
- **The field catalog** describes the links as reference fields with names.

### Import: one curation model for lists and parties
- **Matcher:** `PlaceMatcher` is renamed to a general `NameMatcher`, with the same normalisation, exact-first ranking, strong-only Jaro-Winkler, parts and template aliases.
- **Decisions:** `ImportDecisions` gains `Lists: [ListDecision(List, Text, Action: link | create, EntryId?, Names?, PartyKind?)]`, keyed by `(list, normalised text)`. `List` is one of `collectionGroup`, `dynasty`, `storageLocation` or `party`. Source and holder texts share the `party` decisions: "Christie's" is one decision whether it appears as a seller or a holder.
- **Evaluation:** the mapping and detectors still produce text for these fields; the row evaluation resolves each text through the decisions.
  - With no decision, an exact name match counts as the default.
  - With neither, the row is a problem naming the field and text.
  - A "create" decision counts as resolved for the review, with the id filled at commit.
  - The collection group text is the group heading label, or a mapped value where one exists.
- **Endpoints:** `GET /api/imports/{id}/lists` returns the four groups, each with its distinct texts, row counts, the decision in force, whether it was decided, and suggestions with resolved names. `PUT /api/imports/{id}/lists` merges decisions, as `PUT /places` does.
- **Commit:** list entries and parties marked "create" are made once before any row, as regions are. Each id is recorded on the job as it is created, so a restart does not create duplicates. "Add" returns the existing entry when the name already exists.
- **Wizard:** a new "Lists" step between Places and Dates. It has one section per group, each row offering:
  - the suggestions;
  - "Pick from list…", a searchable select over the whole list, which handles sources with no similar name;
  - "Create new…", with names per language prefilled in the workbook's language, or the name and kind for a party.

  There is no "keep as text" option, and an undecided count is shown per section.

*Alternative:* a separate step per list, rejected: four near-identical screens, and parties need to be decided once across source and holder.

### Templates learn from committed imports
- **On commit:** if the job has a `TemplateId`, `CommitAsync` merges the job's place, date and list decisions into the template's decisions, with the job's decision winning for the same key and others kept, in the same transaction as the state change. The mapping is untouched.
- **Saving from review:** the review step gets the same "Save as template" control as the mapping step, which saves the current mapping and decisions.

### Seed and data
- **Seed:** the seed data's group, dynasty, holder, location and source texts become list entries (with Italian and English names where the seed has them) and parties, created first and then referenced. Rerunning the seed is still safe, because "add" returns existing entries.
- **Migration:** the `Initial` migration is regenerated without the expression indexes, and with the new view and function SQL appended by hand.

## Risks / Trade-offs

- [Curating sources takes effort: the seven workbooks have dozens of distinct seller texts ("Christie's", "Christie's London", "Christie’s 2 Maggio 2019" after detection)] → sections are sorted by row count with a filter for undecided texts, similar names are suggested, decisions are per text, and templates learn from each committed import so later workbooks arrive mostly decided.
- [Link-only drops wording such as the city in "Christie's London" or the "?" in "Ottomana?"] → the user's choice. The original text stays in the import's staged rows, review and report, and the user can create a separate entry when a nuance matters.
- [A cell holding two location codes ("Beta2" and "cella 16") becomes one text to decide] → the user links it to one location or creates a combined one. Multiple locations per artefact are out of scope.
- [A generic aggregate with two shapes (translated or single name) is easy to misuse] → the domain enforces the shape by kind, and tests cover both shapes.

## Migration Plan

1. Regenerate the `Initial` migration: the new tables; artefact columns as foreign keys; the view and functions; the old text columns and expression indexes removed.
2. Reset the development data (`docker compose … down -v`), start the API and run the updated seed.
3. Re-import the workbooks through the wizard to check the curation step against real data.
4. Rollback means checking out the previous commit and resetting data, since nothing is live.

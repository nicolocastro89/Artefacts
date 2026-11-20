# Design

## Context

See proposal.md for motivation.
- **Regions and object types** are aggregates with a single `Name` plus a `NormalizedName` that carries a unique index.
- **The artefact description** is a single text column.
- **The list query** reads the `v_artefact` view, which joins `object_types.name` and `regions.name`; grouping orders by those names.
- **The detail** is built from the write model with region and object type names looked up by id.
- **The database** is one `Initial` migration that is regenerated while nothing is live; development data is reset and reseeded.
- **The frontend** sends no language today. Browsers send `Accept-Language` with every `fetch`.

## Goals / Non-Goals

**Goals:**
- One pattern for every translatable field, which later fields (title, medium, …) adopt without new infrastructure.
- Correct ordering, grouping and searching across languages in SQL, so that pagination stays right.
- No code change to add a content language.

**Non-Goals:**
- Translating the user interface (labels, messages). It stays English.
- Machine translation or translation workflow (review states, who translated what).
- Translating fields other than region names, object type names and the description.
- A per-user language setting: the browser's preference, or `lang`, is enough for now.

## Decisions

### A translation table per entity, keyed by (entity, language)
Each translatable aggregate owns a collection of per-language rows:

| Table | Key | Unique | Holds |
|---|---|---|---|
| `region_names` | (region_id, language) | (language, normalized_name) | `name`, `normalized_name` |
| `object_type_names` | (object_type_id, language) | (language, normalized_name) | `name`, `normalized_name` |
| `artefact_translations` | (artefact_id, language) | – | `description`; later translatable artefact fields become further columns here |

Every `language` column is a foreign key to `languages.code` (restrict), so a language in use cannot be removed.

In the domain these are owned collections, replaced as a whole by the aggregate (like provenance). The domain enforces "at least one name" and "blank means absent". The application checks language codes against the language list, and normalised uniqueness per language through a lookup plus the unique index as a backstop.

*Alternatives:*
- A column per language: needs a schema change per language.
- One generic `(entity, field, lang, value)` table: no foreign keys or types, and reads become pivots.
- A JSONB column per field: unique-per-language names, and ordering by name, need expression indexes and get awkward as fields multiply.

### Languages are rows
The `languages` table holds `code` (primary key: lower-case ISO 639-1, optionally with a region such as `pt-br`), `name` and `position`, the fallback order. The migration inserts `en` (1) and `it` (2). The API offers:
- `GET /api/languages`;
- `POST /api/languages` to add one, which goes last;
- `PUT /api/languages/order` to set the order;
- `DELETE /api/languages/{code}`, which returns 409 while the language is in use.

### Request language → preference list
An API service `RequestLanguages` builds the preference list once per request and passes it explicitly to handlers and queries as `IReadOnlyList<string>`:
1. take the `lang` parameter, else `Accept-Language` in q order;
2. map each code to a content language (exact match, then by primary subtag: `en-GB` → `en`);
3. drop unknown codes and duplicates;
4. append the remaining content languages in fallback order.

The language list is cached in memory and refreshed when it changes. Responses carry `Vary: Accept-Language`.

### Resolution in SQL for lists, in C# for single records
- **Lists** (artefact list, grouping labels, reference lists ordered by name) resolve names in SQL. Two `STABLE` SQL functions, `region_name(uuid, text[])` and `object_type_name(uuid, text[])`, return the name in the first language of the array that has one, using `array_position`. They are mapped as EF Core database functions on both contexts and used in projections, `ORDER BY` and `GROUP BY`. Ordering and grouping therefore stay correct across pages.
- **The view** drops `object_type_name` and `place_region_name`; it keeps the ids. The field catalog follows the view automatically: the reference fields lose their name column.
- **Single records** (artefact detail, region or object type DTOs) load the per-language rows with the aggregate. A small `Localized.Resolve(values, preferences)` returns the text and its language.
- **The description** is not in the list or the view, so only the detail resolves it.

### API shapes (no backwards compatibility needed)
- **Regions and object types:**
  - `RegionDto(id, name, nameLanguage, names: { lang: text }, parentId, parentName)`; `ObjectTypeDto` likewise, plus `artefactCount`.
  - Add: `{ names: {...}, parentId? }`.
  - Change names: `PUT /api/regions/{id}` with `{ names: {...} }`, which replaces all names, and the same for object types.
  - The "add existing" match uses any of the submitted names, compared within its language.
- **Artefacts:**
  - Write: `descriptions: { lang: text }` replaces `description`.
  - Detail: `language` (the first preference used for this request), `description`, `descriptionLanguage`, `descriptions: {...}`.
  - List items and group labels: names as text.
- **Validation keys:** `names.xx`, `descriptions.xx` and `names` (no name at all).

### Search across languages
- **Reference list search:** `EXISTS (SELECT 1 FROM region_names WHERE region_id = r.id AND normalized_name LIKE …)`.
- **Artefact list search:** keeps its fields. It does not search descriptions, as today.

### Frontend
- **Languages:** fetched once (`GET /api/languages`) and cached. The reader's language is the first entry of `navigator.languages` that maps to a content language, else the first in fallback order. The client does not send `lang`; the browser's `Accept-Language` already says the same.
- **Reference lists:** the Regions and Object types tables get one editable name column per language. Add forms have one input per language. A Languages tab lists languages in order, with move up and down, add (code and name), and delete (409 shown in the row).
- **Pickers:** search across languages; inline "Add …" creates the name in the reader's language.
- **Form:** one description textarea per language, labelled with the language name, in fallback order.
- **Detail and print:** the text block shows the resolved description. When `descriptionLanguage ≠ language`, a muted marker such as "In Italian" precedes it, also in print.

### Seed
- Regions and object types get English and Italian names, for example China/Cina and Turkey/Turchia.
- Most descriptions are English; a few are Italian only and a few have both, so fallback can be exercised from either browser language.
- Development data is reset (`docker compose down -v`), then reseeded.

## Risks / Trade-offs

- [Correlated function calls per row in large lists] → the functions are `STABLE` and index-backed through the (entity, language) keys; the collection is in the thousands. If needed later, a lateral join replaces them without API changes.
- [Duplicate names across languages for the same place ("Vienna"/"Vienna")] → allowed by design (uniqueness is per language). The UI shows each name in its language column.
- [Cached language list goes stale across instances] → there is a single API instance now; the cache refreshes on every language change in that instance. Multi-instance caching is a later concern.
- [Readers unaware of fallback] → the description shows its language when it is not the reader's. Names fall back silently, which is acceptable for short reference names.

## Migration Plan

1. Delete the migrations and regenerate `Initial`. It adds the languages table with its two rows, the three translation tables, and the two SQL functions, and recreates the view without the name columns. The expression indexes and the view are appended by hand as before.
2. Reset development data and reseed.
3. Regenerate the OpenAPI document and the frontend client.

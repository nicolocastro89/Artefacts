# Design

## Context

The catalogue is a DDD backend (Domain → Application → Infrastructure → Api) on PostgreSQL. Writes go through the `Artefact` aggregate and `CollectionDbContext`; reads go through the `v_artefact` view mapped to a no-tracking `ReadDbContext`, described by a field catalog generated from attributes on the read row. The list query (`ArtefactListQuery`) searches with `ILIKE`, sorts, and pages over the view. The frontend renders pages from JSON page definitions; the list uses `record-table`, `record-grid` and `rack-panel` widgets that share URL state. Regions are a managed aggregate with the same add/rename/delete-unless-used behaviour this change needs for object types.

The view DDL is an embedded resource (`Read/Views/v_artefact.sql`) loaded by the `ArtefactReadView` migration at run time. Editing that file in place would silently change what the old migration creates. See proposal.md for motivation.

## Goals / Non-Goals

**Goals:**
- Hold everything the existing workbooks record, without forcing staff to normalise it.
- Group the list by type, group, origin, century and dynasty with correct counts across pages.

**Non-Goals:**
- Structured exhibitions (venue, dates, catalogue number): entries are free text for now.
- Multilingual fields beyond the single English catalogue entry.
- Normalising dynasties or collection groups into reference lists.
- Hiding financial fields by permission (roles come later; the sensitivity tag is enough now).
- Faceted filtering by group value. Grouping orders and labels; filtering is a later query-engine concern.

## Decisions

### Object type is an aggregate like Region; group and dynasty stay free text
Object types are few, shared, and renamed occasionally ("Tappeti" → "Carpets"), so they get identity and a reference list (`ObjectType` aggregate, `object_types` table, unique on a normalised name, delete blocked when referenced). Collection groups mix origin ("CINA") and technique ("LAMPASSI"), and dynasties are written loosely ("Ottomana", "Selgiuchide"). Normalising them would be guesswork, so both are trimmed text, and grouping uses a case-insensitive, whitespace-collapsed key so that "Ottomana" and "ottomana " group together.
*Alternative:* one generic "tags/classifications" table. It would be more flexible, but it is harder to validate, harder to describe in the field catalog, and the semantic layer wants named fields.

### Place of production = text as written + optional region link
This follows the principle "keep what the curator wrote": `PlaceOfProduction` is a value object with `Text` and an optional `RegionId`. Grouping by origin uses the region and falls back to the "no value" group when no region is linked. Uncertain text ("Anatolia o Asia Centrale?") stays visible and unlinked until someone decides.
*Alternative:* region only. That loses the curator's uncertainty and forces a decision at import time.

### Exhibitions as an owned ordered collection
Exhibitions are an owned collection (`artefact_exhibitions`: artefact id, position, text), replaced as a whole on update like provenance. The read view exposes a count; the detail endpoint returns the list.

### Valuation reuses Money; currencies gain a withdrawn-code list
`Iso4217` gets a second static set of withdrawn codes (ITL, DEM, FRF, ESP, NLG, BEF, ATS, PTE, FIM, IEP, GRD, LUF, and others from ISO 4217 list three). `IsKnown` accepts both sets, and the API description lists them as one enumeration. Money already rejects negative amounts. Valuation is `Money?` on the aggregate. In the read view, both price and valuation amount/currency carry `[Sensitivity("financial")]`.
*Alternative:* free-text currency. That loses validation and the ability to convert later.

### Acquisition: add Lot and Note
These are two optional trimmed strings on the `Acquisition` value object. The acquisition date stays a `DateOnly?`: the import puts dates it cannot read into the note, so nothing is lost.

### One migration, one view file
Nothing is live, so there is no upgrade path to preserve: all earlier migrations are replaced by a single `Initial` migration generated from the current model. It also creates the two expression indexes and the view from the single embedded file `Read/Views/v_artefact.sql`. Development databases are reset (`docker compose down -v`, then seed). Once a database exists that must be upgraded in place, view changes will need versioned DDL files so that each migration keeps loading the definition it was written for.

### Grouping in the list query
`ArtefactListRequest` gains `Group` (`none | objectType | collectionGroup | origin | century | dynasty`, bound with the existing `QueryEnum` pattern). The view exposes a computed group key for each kind: `object_type_id`, a normalised `collection_group_key`, `place_region_id`, `century` (computed in SQL from the earliest year, or the latest year when only that is known; BCE handled as negative), and a normalised `dynasty_key`. When grouping, the query orders by `(group key IS NULL), group key` (labels for type and region via the joined names), then by the requested sort, then by inventory number. One extra `GROUP BY` query returns `[{ key, label, count }]` for the current search. The response becomes `PagedResult<ArtefactListItem>` plus `groups` (empty when ungrouped), and each item gets `groupKey` and `groupLabel`. The "no value" group always comes last, with a null key and label; the frontend labels it ("No object type", "Undated", …).
*Alternative:* group on the client. That breaks with pagination (counts and headings would only cover one page).

### Century labels
The century is an integer key computed in the view (17 for 1601–1700, −1 for 100–1 BCE). The list query formats its label ("17th century", "1st century BCE") so that every client shows the same text; SQL stays free of display text. A record dated "1600" falls in the 16th century, following the 1601–1700 convention, consistent with how the date parser computes centuries.

### Frontend
- **List:** the toolbar gains a "Group by" select (URL param `group`). `record-table` renders a full-width group heading row when `groupKey` changes and at the top of each page (the heading repeats); `record-grid` renders a heading spanning the grid. Headings show label and count from `groups`. Travelling rows and keyboard selection skip heading rows.
- **Detail and print:** the shared record body adds three field-list sections:
  - Classification: type, group;
  - Origin: place with linked region, dynasty;
  - Holding: holder, location, valuation.
  It also adds an exhibitions list widget and the catalogue entry as a text block with preserved line breaks. Empty fields are omitted by the existing field-list behaviour.
- **Form:** the fieldsets are:
  - Classification: an object type picker built on the region picker pattern, with inline add; collection group;
  - Origin: place text plus an optional region picker; dynasty;
  - Exhibitions: a repeatable text list with reorder, like provenance;
  - Catalogue entry: a textarea;
  - Holding: holder, location, valuation (money field);
  - Acquisition: gains lot and note.
- **Object types page:** the regions page becomes "Reference lists" with two tabs (Regions, Object types). Shared table, add, rename and delete behaviour, plus a usage count column.

## Risks / Trade-offs

- [Grouping with sort on large lists runs two queries] → indexes on `object_type_id`, `place_region_id`, and expression indexes on the normalised group and dynasty keys. The collection is in the thousands, not millions.
- [Free-text dynasty/group values drift ("Sung" vs "Song")] → grouping makes the drift visible. A later change can add normalisation without a data migration, because the text is kept.
- [The record body grows long on the detail page] → sections only render when they have values, and print keeps its no-split rule per section.
- [The withdrawn-currency list must be correct] → it is embedded from ISO 4217 list three, and tests cover ITL and an unknown code.

## Migration Plan

1. Replace all migrations with a regenerated `Initial` migration that creates every table, the expression indexes on the normalised collection group and dynasty, and the view.
2. Reset development databases (`docker compose -f docker/docker-compose.yml down -v`, start the stack, start the API, run the seed). The integration tests build their databases from scratch.
3. Regenerate the OpenAPI document and the frontend client. The seed fills the new fields on most sample records so the grouping is visible in development.

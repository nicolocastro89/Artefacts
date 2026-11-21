# Proposal

## Why

Dynasty, collection group, storage location, acquisition source and holder are free text today, so each artefact keeps its own spelling. The imported workbooks show what that costs: "Safavide", "safavide", "Sfavide" and "Savavide" form four groups; "Christie's", "Christie's London" and "Christie’s" are three sellers. Nothing learned in one row or one import helps the next. Making these values managed records, curated once during import and picked from a list afterwards, keeps the catalogue consistent and lets every import build on the previous ones. Sources and holders are people and organisations: the same kind of record that loans will need for borrowers, so they become one shared aggregate now.

## What Changes

- **Reference lists** for dynasties, collection groups and storage locations, managed like object types: list with usage counts, search, add (returning an existing entry with the same name), rename, delete only when unused. Dynasties and collection groups have a name per content language; storage locations have a single name, since they are codes ("MI F3", "Beta2").
- **Parties**: people and organisations (auction houses, dealers, private sellers, institutions) with a single name and a kind. An artefact's acquisition source and its holder link to a party. Parties are the records future loans will use for borrowers.
- **BREAKING:** the artefact's collection group, dynasty, storage location, acquisition source and holder become links to those records instead of text; the text values are removed. Nothing is live, so the development data is reset and the seed recreated rather than migrated.
- An empty holder means the artefact is held by the collection itself. Linking the signed-in person as the default holder waits for logins, which do not exist yet.
- Search, grouping and the read view use the linked records: grouping by dynasty or collection group groups by entry and labels it in the reader's language, and free-text search matches the names.
- **Forms and pages:** the artefact form picks each value from its list, with inline adding, as it does for object types. A reference page for each list and a page for parties.
- **Import:** a new curation step lists every distinct dynasty, collection group (the sheet's group headings), storage location, source and holder text in the workbook. Each one is matched to an entry (exact names first, then similar ones), picked from the list, or created. Every distinct value must be decided; rows with an undecided value are listed as problems. Decisions are remembered per text, like place decisions.
- **Templates learn from imports:** "Save as template" is also offered at the review step. When an import that started from a template is committed, the template is updated with that import's place, date, list and party decisions.

## Capabilities

### New Capabilities
- `reference-lists`: managed lists of dynasties, collection groups (names per language) and storage locations (single name), with usage counts and in-use protection.
- `parties`: people and organisations with a single name and a kind, used as acquisition sources and holders (and later as loan borrowers).

### Modified Capabilities
- `artefact-catalogue`: collection group, dynasty, storage location, acquisition source and holder are links to managed records; search and grouping use them.
- `read-model`: the read view carries the linked identifiers instead of the text values.
- `artefact-browsing`: the form picks the new values from lists with inline adding; reference pages for the three lists and a parties page.
- `artefact-import`: group headings and mapped values are curated against the lists and parties in a new step; undecided values make their rows problems; templates store those decisions and learn from committed imports; "Save as template" at review.

## Impact

- **Backend:** new aggregates (`ReferenceEntry` for the three lists, `Party`), repositories, handlers and endpoints. `Artefact` and `Acquisition` change to hold ids. The read view and its grouping keys change, along with the name-resolving SQL functions, the field catalog and the OpenAPI document. The import evaluation, decisions, curation endpoints and template updating change too.
- **Frontend:** a generic list page reused for the three lists, a parties page, pickers in the artefact form, the detail and print widgets, and a new wizard step.
- **Data:** the single `Initial` migration is regenerated; development data is reset; the seed creates list entries and parties and links them.
- **Not included:** contact details and roles for parties beyond name and kind, loans themselves, and logins (the default holder).

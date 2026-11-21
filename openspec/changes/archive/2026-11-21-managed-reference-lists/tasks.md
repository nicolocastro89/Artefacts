# Tasks

## 1. Domain

- [x] 1.1 Add the `ReferenceEntry` aggregate with `ListKind` (dynasty and collection group with names per language; storage location with one name), `Create`, `SetNames` and `NameIn`, enforcing the shape by kind; verify with domain tests for both shapes, blank and too-long names, and name normalisation
- [x] 1.2 Add the `Party` aggregate (name, normalised name, kind person or organisation, rename, change kind); verify with domain tests
- [x] 1.3 Change `Artefact` to hold `CollectionGroupId`, `DynastyId`, `StorageLocationId` and `HolderId`, and `Acquisition` to hold `SourceId`, removing the text fields; verify that the updated domain tests pass

## 2. Persistence and read model

- [x] 2.1 Map the new tables (`reference_entries`, `reference_entry_names` with the per-kind and per-language unique index, the partial unique index for single-name kinds, `parties`) and the artefact foreign keys with restrict delete; map unique and foreign-key violations to the `names`/`name` validation errors and the `entry_in_use`/`party_in_use` conflicts; verify with repository-level integration tests
- [x] 2.2 Update `v_artefact` to carry the ids; add the `reference_entry_name(uuid, text[])` function; remove the text grouping keys and their expression indexes; resolve names in `ArtefactListQuery` for items, search (any language, and party names) and grouping by collection group and dynasty; verify with read-side tests for grouping labels in Italian and English, and for search by dynasty, location and source names
- [x] 2.3 Regenerate the `Initial` migration with the view and function SQL appended; verify that a fresh database migrates and that the integration tests start

## 3. Application and API

- [x] 3.1 Add the list handler and `/api/lists/{kind}` endpoints (search with counts, add returning the existing entry, set names, delete with 409 when in use); verify with integration tests covering every reference-lists scenario
- [x] 3.2 Add the party handler and `/api/parties` endpoints (search, kind filter, source and holder counts, add, rename, change kind, delete with 409 when in use); verify with integration tests covering every parties scenario
- [x] 3.3 Update the artefact write request, validation of unknown ids, the detail DTO (resolved names; party name and kind) and the field catalog; verify with updated catalogue-field tests and the new unknown-dynasty, unknown-source, holder and held-by-the-collection scenarios
- [x] 3.4 Regenerate the OpenAPI document and the frontend client; verify that `npm run build` typechecks

## 4. Import

- [x] 4.1 Generalise `PlaceMatcher` to `NameMatcher` and add `ListDecision` to the import decisions; verify that the existing place-matching tests pass unchanged against the renamed matcher
- [x] 4.2 Resolve collection group (heading label or mapped value), dynasty, storage location, source and holder texts through the decisions in the row evaluation (exact match by default; undecided values as row problems naming the field and text; "create" counted as resolved); verify with unit tests for exact, linked, created and undecided values, and source and holder sharing a party decision
- [x] 4.3 Add `GET` and `PUT /api/imports/{id}/lists` (distinct texts per group with row counts, decision in force, suggestions with resolved names; merge on save; validation of entry ids, names and party kind); verify with integration tests
- [x] 4.4 Create the entries and parties marked "create" at commit, once, before rows, recording ids on the job so a restart does not duplicate them; verify with an integration test that commits rows sharing a new dynasty and a new party and finds one entry and one party linked from every row
- [x] 4.5 Update a job's template with its place, date and list decisions when it is committed (the job's decision wins per key; other decisions and the mapping are kept); verify with an integration test that a second import with the template pre-fills a decision made in the first
- [x] 4.6 Show linked entry and party names in the review fields; verify with a review integration test

## 5. Frontend

- [x] 5.1 A generic list page for dynasties, collection groups and storage locations (columns per language for translated lists, one name for locations, usage counts, search, add, rename, delete with the in-use error) and the reference tabs for them; verify with component tests for both shapes
- [x] 5.2 A parties page (name, kind, source and holder counts, filter by kind, add, rename, change kind, delete with the in-use error); verify with a component test
- [x] 5.3 Artefact form pickers for collection group, dynasty, storage location, holder and source, with inline adding (kind chosen for parties); detail and print show the names; verify with updated form and detail component tests, including inline party creation
- [x] 5.4 The import "Lists" step between Places and Dates (a section per group: suggestions, pick from list, create with names or kind, undecided counts and filter), and "Save as template" on the review step; verify with component tests for linking a variant, picking a source with no match, creating a party, and saving a template from review
- [x] 5.5 Follow DESIGN.md for the new pages and step; verify with headless captures at 1440 and 390 in light and dark with no overflow

## 6. Seed and acceptance

- [x] 6.1 Update the seed to create list entries (Italian and English names where known) and parties, and link them from the sample artefacts; verify on a reset development database that the seed runs twice without failures and that grouping by dynasty shows the expected groups
- [x] 6.2 Re-import the seven workbooks from `data/` with one template, curating the lists through the API in the acceptance script, then a second time; verify that every row is imported or listed with a reason, that no undecided list value remains, and that the second run pre-fills every decision from the updated template
- [x] 6.3 Run `dotnet test`, `npm test`, `npm run build`, `npm run lint` and `openspec validate managed-reference-lists --strict`; verify that all pass
- [x] 6.4 Create a bead for defaulting the holder to the signed-in person's party once logins exist, and verify that `bd list` shows it

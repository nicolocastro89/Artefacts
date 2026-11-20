# Tasks

## 1. Domain

- [x] 1.1 Add the `ObjectType` aggregate (id, name with trimmed, case-insensitive uniqueness key, rename) and verify with domain unit tests (create, rename, normalised key)
- [x] 1.2 Add withdrawn ISO 4217 codes to `Iso4217` (list three, including ITL, DEM, FRF, ESP, NLG, BEF) and verify `Money` accepts ITL and still rejects "XYZ" and negative amounts in unit tests
- [x] 1.3 Extend `Acquisition` with optional `Lot` and `Note` (trimmed) and verify with unit tests
- [x] 1.4 Add `PlaceOfProduction` value object (text as written, optional `RegionId`; empty text with no region becomes null) and verify with unit tests
- [x] 1.5 Extend `Artefact` with object type, collection group, place of production, dynasty, ordered exhibitions (replace as a whole, blank entries dropped), English catalogue entry, holder, storage location and valuation; verify with unit tests covering ordering, trimming and valuation rules

## 2. Persistence

- [x] 2.1 Map the new fields, the `object_types` table and the `artefact_exhibitions` owned collection in `CollectionDbContext`, replace all earlier migrations with one regenerated `Initial` migration (FKs to object types and regions, indexes on `object_type_id` and `place_region_id`, expression indexes on the normalised collection group and dynasty) and verify a fresh database migrates
- [x] 2.2 Extend the single view file `Read/Views/v_artefact.sql` with the new columns, the group keys (normalised collection group and dynasty keys, century) and the joined type and region names, created by the `Initial` migration; verify with the integration test that the view columns match `ArtefactRow` and that `SELECT * FROM v_artefact LIMIT 1` returns the new columns on a reset development database
- [x] 2.3 Add an `ObjectType` repository and include place-of-production references in the region "in use" check; verify with integration tests (delete of a region used as place of production is rejected)

## 3. Application and API

- [x] 3.1 Object type handlers and endpoints (`GET /api/object-types?search=` with artefact counts, `POST` returns existing on duplicate name, `PUT` rename with collision error, `DELETE` with 409 when in use); verify with integration tests mirroring the region tests
- [x] 3.2 Extend `ArtefactWriteRequest`, `ArtefactDetail` and validation (unknown object type or region → field error; valuation and price currency rules; exhibitions list); verify integration tests for create/update round-trips, including withdrawn currency and an unknown object type
- [x] 3.3 Extend `ArtefactRow`, the field catalog (labels, types, `financial` on valuation amount and currency) and verify the catalog test lists exactly the view's fields and tags valuation
- [x] 3.4 Widen list search to collection group, place text, dynasty, holder and storage location; verify with an integration test that searching "cella 16" finds the matching artefact
- [x] 3.5 Add grouping to the list query (`group` query parameter bound with `QueryEnum`, ordering by group then sort, `groupKey`/`groupLabel` per item, `groups` with counts for the current search, no-value group last); verify integration tests for object type and century grouping, including an undated artefact and counts that span pages
- [x] 3.6 Regenerate the OpenAPI document and verify it lists the `group` enumeration, the new fields, and the object type endpoints

## 4. Frontend

- [x] 4.1 Regenerate the API client (`npm run api:generate`) and verify `npm run build` typechecks
- [x] 4.2 Add the "Group by" control and the `group` URL parameter to the list; render group headings with counts in `record-table` and `record-grid` (repeated at the top of each page; skipped by keyboard selection and travelling rows); verify with component tests (headings, counts, URL round-trip)
- [x] 4.3 Show the new fields on the detail page and in the print record (classification, origin, holding, exhibitions list, catalogue entry with preserved line breaks; empty fields omitted); verify with a detail component test and a print preview check that the sections do not split across pages
- [x] 4.4 Add the form fieldsets (object type picker with inline add, collection group, place text plus region picker, dynasty, exhibitions list with reorder, catalogue entry, holder, location, valuation, acquisition lot and note); verify with form tests for inline object type creation and server field errors on the new fields
- [x] 4.5 Turn the regions page into "Reference lists" with Regions and Object types tabs (add, rename, delete with in-use error, usage count); verify with component tests mirroring the regions tests
- [x] 4.6 Follow DESIGN.md: group headings use the condensed label style on the plate, yellow stays reserved for selection; verify by headless captures of the grouped list at 1440 and 390 (light and dark) with no overflow

## 5. Seed and verification

- [x] 5.1 Extend the seed script with object types and with values for the new fields on most sample records (several groups, an undated record, an unlinked uncertain place, an ITL price); verify that a rerun is a no-op and that grouping the list by each option shows sensible headings
- [x] 5.2 Run `dotnet test`, `npm test`, `npm run build`, `npm run lint` and `openspec validate extend-catalogue-fields --strict`; verify all pass

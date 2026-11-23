# Tasks

## 1. Backend: units of measure

- [x] 1.1 Add the `UnitOfMeasure` aggregate (Domain) with `UnitOfMeasureId`, names via `LocalizedName`, abbreviation (≤ 20 chars, required) and factor (> 0, decimal), with domain unit tests for each validation rule
- [x] 1.2 Map `units_of_measure` and `unit_of_measure_names` in `CollectionDbContext` (unique name per language, unique normalised abbreviation) and seed mm/cm/m/in with fixed GUIDs and en+it names via `HasData`; verify with an integration test on a fresh database listing the four units in Italian
- [x] 1.3 Add the unit handler and endpoints (list ordered by factor with usage count, add, update, delete with a conflict when in use); integration tests for duplicate abbreviation, factor 0, delete in use and delete unused

## 2. Backend: measurements on artefacts

- [x] 2.1 Add the `Measurement` value object (value > 0, ≤ 4 decimals, unit required) and `ArtefactMeasurements` on `Artefact` (width, height, length, diameter), and extend `Describe`; domain tests for zero, negative, missing unit
- [x] 2.2 Map the owned measurement columns with FKs to units (`ON DELETE RESTRICT`); add `{ value, unitId }` measurement fields to the create/update/read DTOs with per-field validation (`height.unitId`) and an unknown-unit check; integration tests for the spec scenarios (text + measurements, different units, missing unit, text only)
- [x] 2.3 Extend `v_artefact.sql` with value, unit id and metres per measurement (joined factor) and `ArtefactRow`; test that the metres follow a changed factor
- [x] 2.4 Add the GraphQL `Measurement` and `UnitOfMeasure` types on `Artefact` (value, inMetres, unit with resolved name, abbreviation, factor); add the measurements to the `artefact-detail` system query; GraphQL tests for `lang=it` and for a missing measurement
- [x] 2.5 Delete `Persistence/Migrations`, regenerate `Initial`, re-append the view SQL and expression indexes, reset dev data; verify the API starts and `dotnet test` passes

## 3. Backend: import

- [x] 3.1 Make `LineRule.Target` optional (null = discard): `MappingEngine` drops matched lines and records them as discarded on the row, rejects discard with `Continues`, and `MappingSuggestion` never suggests it; unit tests for discard by position and by detector, and that an old template JSON still loads
- [x] 3.2 Add `DimensionsParser` (labels in it/en, decimal comma, written or trailing unit, unit aliases, default unit, unlabelled order, strict "unclear" rule) with table-driven unit tests covering every spec scenario plus "chiuso 12 x 8; aperto 24 x 8", "82 x 64" without an order, and repeated labels
- [x] 3.3 Add the mapping options (default unit, unlabelled order) to `ImportMapping` and templates, with the cm default suggestion; test round-trip through a template
- [x] 3.4 Add `DimensionResolution` to `ImportDecisions`, `GET`/`PUT /imports/{id}/dimensions` listing unclear texts with their rows, and template learning on commit; `RowEvaluator` fills measurements from the parse or resolution and raises a `dimensions` problem for undecided texts; integration tests for entered measurements, keep-text-only, undecided blocking, and the template pre-fill on a second import
- [x] 3.5 Include measurements and discarded lines in the review row DTOs and the committed artefact; integration test importing a workbook end to end with one discarded line and one resolved dimensions text (the report spreadsheet is unchanged: its rows carry no field values)
- [x] 3.6 Regenerate the OpenAPI document and frontend API types (`schema.d.ts`, `schema.graphql`)

## 4. Frontend

- [x] 4.1 Add the units page at `/reference/units` (list with usage, add, edit names/abbreviation/factor with the usage warning, delete unused) and its navigation item; component test for adding "Foot"
- [x] 4.2 Add measurement inputs (value + unit picker, default to last used unit or cm) to `ArtefactForm`, with field errors; component tests for entering and clearing a measurement
- [x] 4.3 Add `formatMeasurements` and show text plus measurements in the detail page (system page definition), print definition, and record/collection widgets; unit test for the format and a detail page test for the spec scenario
- [x] 4.4 Mapping step: "Discard" as a line rule target (hiding "continues" for it), the default unit picker and unlabelled order control; component test for adding a discard rule
- [x] 4.5 Add `DimensionsStep` between dates and review (measurement inputs per text, keep text only, keep all remaining as text only), shown like the dates step even when nothing is unclear; component test for resolving one text and the bulk action
- [x] 4.6 Review step: show measurements next to the dimensions text and discarded lines crossed out; component test

## 5. Acceptance

- [x] 5.1 Import a sample workbook with a template: discard a junk line by position, set cm and height × width as defaults, resolve one unclear text, commit, and check the artefacts' detail pages show text and measurements
- [x] 5.2 Run backend and frontend tests, `npm run lint` and the build; `openspec validate structured-dimensions-and-line-discard --strict`

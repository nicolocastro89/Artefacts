# Design

## Context

- `Artefact.Dimensions` is a free-text string; `Artefact.Describe(maker, creationDate, medium, dimensions)` sets it. `Money` (Finance) is the model for a validated value object with a decimal and a code, owned by the artefact.
- Translatable reference data is modelled like `ObjectType`: an aggregate with owned `LocalizedName`s mapped via `MapNames` in `CollectionDbContext`. Seed data uses EF `HasData` (only `languages` today).
- The import pipeline: `MappingEngine` assigns lines to `ImportField`s via `LineRule`s, `RowEvaluator` turns mapped values into an artefact input plus problems, and decisions (places, dates, lists) live in `ImportDecisions` on the job and in templates. Unresolved dates follow a pattern: distinct texts listed by `ImportHandler`, resolved via `PUT /imports/{id}/dates`, applied in `RowEvaluator` through `context.DateResolutions`, shown in `DatesStep.tsx`.
- The read side: `v_artefact.sql` (hand-written, appended to the single `Initial` migration), `ArtefactRow`, GraphQL `ArtefactTypes`, system queries `artefact-detail.graphql`/`artefact-list.graphql`, system page `artefact-detail.json` and the print definition `artefactPrint.ts`.
- Project practice: one `Initial` migration while nothing is live; regenerate it and reset dev data on model changes.

## Goals / Non-Goals

**Goals:**
- Measurements that convert exactly (decimal arithmetic, not floating point) and survive a unit's factor being corrected.
- Reuse the date-resolution shape for unclear dimension texts, so the wizard, API and templates stay uniform.
- Old import templates keep working unchanged.

**Non-Goals:**
- Filtering or sorting the artefact list by measurements, and query-builder rules for them (the value in metres is exposed so this can be added later).
- Converting a measurement to a different unit for display; the value is shown in the unit it was entered in.
- Dimensions of parts or states (closed/open, framed/unframed) as separate measurement sets; these stay in the text.
- Skipping whole rows, or a line in a single chosen row.

## Decisions

**Unit of measure is its own aggregate, not a `ReferenceEntry` kind.** It carries an abbreviation and a factor that the generic list entry doesn't have, and measurements reference it by `UnitOfMeasureId`. Names use the existing `LocalizedName`/`MapNames` machinery (unique per language), with a unique index on the normalised abbreviation. *Alternative:* a `ListKind.Unit` with extra nullable columns on `reference_entries`; rejected because it would make every list entry carry fields only one kind uses.

**Factor stored as `numeric` (decimal), relative to the metre.** The SI base unit for length is the metre, so mm = 0.001, cm = 0.01, in = 0.0254 (exact by definition). Only length is in scope, so there is no "dimension kind" column; if mass or volume appear later, a quantity-kind column is added then.

**Measurements store the entered value and unit; metres are computed on read.** `Measurement(decimal Value, UnitOfMeasureId Unit)` is an owned value object (columns `height_value`, `height_unit_id`, etc. for width, height, length, diameter), with a foreign key to `units_of_measure` and `ON DELETE RESTRICT`, which enforces "a unit in use can't be deleted" in the database as well as in the handler. `v_artefact` joins the units and computes `height_m = height_value * factor`, so a corrected factor is reflected immediately (spec: read-model). *Alternative:* storing the metre value too; rejected because it goes stale when a factor changes.

**Seeding with `HasData` and fixed GUIDs**, for the unit rows and their owned name rows (en + it), like `languages`. Fixed IDs keep the seed idempotent across the regenerated `Initial` migration and let tests and the import's default-unit setting refer to "cm" stably. The seeded units are ordinary data afterwards and can be edited.

**`Describe` gains a measurements argument object** (`ArtefactMeasurements` with four nullable `Measurement`s) rather than four more positional parameters; the API DTOs get `width`/`height`/`length`/`diameter` as `{ value, unitId }` objects, validated per field (`height.value`, `height.unitId`) through the existing `ValidationCollector`, so errors land next to the field in the form.

**Discard is a line rule with no target.** `LineRule.Target` becomes `ImportField?`; `null` means discard. Existing templates always serialise a target, so they deserialise unchanged. `MappingEngine` records discarded lines on the row's mapped values (as a `Discarded` list) instead of passing them to the remaining-lines target, so the review can show them crossed out. A discarding rule with `Continues` is rejected, since there is no field to continue into. *Alternative:* a sentinel `ImportField.Discard`; rejected because it would show up in every "field" list and switch that has to exclude it.

**Dimensions parsing is a pure function in Application/Imports (`DimensionsParser`).** Input: the joined dimensions text, the unit list (abbreviations, names in all languages, plus fixed aliases "inches", `"` and `''`), the mapping's default unit and unlabelled order. It tokenises into (label?, number, unit?) groups split on `x`, `×`, `*`, `;`, `,` (a comma between digits is a decimal comma) and whitespace. A trailing unit applies backwards to every preceding number without its own unit. It returns either a full assignment or "unclear". Rules from the spec: every number must land in a distinct measurement and every measurement must have a unit, otherwise the text is unclear. Being strict on purpose means nothing is guessed silently; anything unclear goes to the user.

**Mapping options** go on `ImportMapping`: `DimensionsDefaultUnit` (`UnitOfMeasureId?`) and `DimensionsUnlabelledOrder` (an ordered list of up to four measurement names, empty when not set). `MappingSuggestion` always proposes centimetre (the seeded unit) as the default, since the collection measures in centimetres, and never sets an unlabelled order. If centimetre has been deleted, mapping validation reports the missing unit and the user chooses another.

**Dimension decisions mirror date resolutions.** `DimensionResolution(string Text, bool KeepTextOnly, MeasurementInput? Width, Height, Length, Diameter)` lives in `ImportDecisions.Dimensions`, keyed by the normalised text (same normalisation as `DateKey`). New endpoints: `GET`/`PUT /imports/{id}/dimensions`. The "keep all remaining as text only" action is a frontend convenience that PUTs a resolution with `KeepTextOnly` for every undecided text. `RowEvaluator` adds a `dimensions` problem for undecided unclear texts. Templates learn these decisions on commit, like the others (the existing "Templates learn from committed imports" requirement covers decisions generally; the new requirement says the dimensions decisions are carried too). A resolution whose unit was since deleted is treated as undecided.

**Wizard order:** mapping → places → lists → dates → **dimensions** → review. Like the dates step, it is always shown and says so when every text could be read.

**Display.** A shared `formatMeasurements` in `widgets/format.ts` produces "Ø 17.6 cm · H 7 cm" (order: H, W, L, Ø), with numbers formatted via `Intl.NumberFormat`. The detail system query and page definition, the print definition, and the record/collection widgets show it after the dimensions text, the same way historical dates show their range after the text.

## Risks / Trade-offs

- [Many unclear texts in a large workbook make the dimensions step long] → the unlabelled-order option resolves the common "82 x 64 cm" case up front, and "keep all remaining as text only" lets the user move on.
- [The label "l" is ambiguous (length vs. larghezza/width in Italian usage)] → "l"/"lungh" mean length and "larg" means width, as the spec states; the review shows the result next to the text, so a wrong reading is visible before commit.
- [Changing a unit's factor silently changes every metre value] → intended, since it is used to correct a wrong factor. The units page shows the usage count next to the factor field as a warning.
- [Regenerating `Initial` resets dev data] → established practice while nothing is live; noted in the tasks.

## Migration Plan

Delete `Persistence/Migrations`, regenerate `Initial`, re-append `v_artefact.sql` and the expression indexes, reset the dev database. There is no production data to migrate, and old import templates load unchanged.

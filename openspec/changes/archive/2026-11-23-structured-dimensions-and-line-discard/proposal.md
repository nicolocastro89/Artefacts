# Proposal

## Why

An artefact's dimensions are stored only as free text ("diam 17,6 x h 7"). That text can't be compared, converted or used for display, e.g. in centimetres or inches. Staff want the text kept as written and, next to it, the actual width, height, length and diameter, each in a unit they choose. Separately, the importer guarantees that every line of a mapped cell ends up in a field. That forces junk lines, such as stray notes or repeated headings, into a "remaining lines" field. Staff want a line rule that throws such a line away in every row.

## What Changes

- **Units of measure** become a managed list. Each unit has a full name in one or more content languages, an abbreviation, and a conversion factor to the SI base unit, e.g. Millimetre, "mm", 0.001. The database is seeded with millimetre, centimetre, metre and inch. Units can be added, renamed and given a new factor. A unit in use can't be deleted.
- **Structured measurements on artefacts**: optional width, height, length and diameter. Each is a positive number with its own unit. The free-text dimensions field stays, unchanged, and is still what is most often shown. Reads return both the text and the measurements, and each measurement also carries its value in metres.
- **Edit form, detail and print**: the form gets a value and a unit picker for each measurement. The detail and print pages show the dimensions text and the measurements together.
- **Import parses dimensions when it's clear**: the dimensions text is read for labelled values ("h 7", "diam 17,6", "lungh. 20") and a written unit, or the mapping's default unit. When every number in the text can be assigned, the measurements are filled in. Unlabelled values ("82 x 64 cm") are filled in only when the mapping says what order they are in.
- **New import step "Dimensions"**: it lists every dimensions text that couldn't be read clearly, with its rows. The user either fills in the measurements, which then apply to every row with that text, or keeps the text without measurements. Rows with an undecided text are problems until it is decided. Decisions are saved with templates, like date resolutions.
- **Discard line rule**: a line rule can now have "Discard" as its target. Lines that rule matches, by content or by position, are dropped in every row. The review shows discarded lines crossed out. This relaxes the rule that every line of a mapped column must end up in a field. Skipping a whole row, or a line in only one particular row, is out of scope.

## Capabilities

### New Capabilities
- `units-of-measure`: the managed list of units (names per language, abbreviation, SI factor), its seed data, and its management page.

### Modified Capabilities
- `artefact-catalogue`: new requirement for structured measurements (width, height, length, diameter with units) alongside the dimensions text.
- `artefact-import`: line rules can discard lines; dimensions are parsed into measurements when clear; new dimensions-resolution step; templates also store the dimensions decisions, the default unit and the unlabelled order.
- `artefact-browsing`: entering measurements in the form; showing text and measurements together; unit management page.
- `read-model`: the artefact read view gains the four measurements (value, unit, value in metres).
- `graphql-queries`: artefacts expose measurements with their unit (name, abbreviation, factor) and value in metres.

## Impact

- **Domain**: new `UnitOfMeasure` aggregate; a `Measurement` value object (value, unit id); four optional measurements on `Artefact`.
- **Persistence**: new `units_of_measure` and `unit_of_measure_names` tables seeded with mm, cm, m and in; measurement columns on `artefacts`; `v_artefact` gains measurement columns. Per project practice, the single `Initial` migration is regenerated and dev data reset.
- **Application/API**: unit endpoints (list, add, update, delete), measurement fields in the artefact create/update/read DTOs; import mapping (`LineRule` target optional = discard; dimensions options on the mapping), dimensions parser, `DimensionResolution` decisions with an endpoint like the dates one.
- **GraphQL**: a `Measurement` type and a `UnitOfMeasure` type; the stored-query schema index and builder rules pick the new fields up.
- **Frontend**: units management page under Reference; measurement inputs in `ArtefactForm`; display widgets; import mapping step (Discard target, default unit, unlabelled order); new `DimensionsStep`; review shows discarded lines.
- **Templates**: existing templates stay valid. Every existing line rule has a target, and the new options default to "no default unit" and "unlabelled order not set".

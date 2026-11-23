# artefact-import Specification

## Purpose

Lets staff bring existing collection records into the catalogue from Excel workbooks, including the photographs embedded in them, with control over how cells map to fields and a clear account of every row that could not be imported.

## Requirements

### Requirement: Upload a workbook for import
The system SHALL accept an `.xlsx` workbook of up to 2 GB together with the object type its rows belong to, and start an import job that reads the workbook in the background. The job SHALL report its state (reading, awaiting mapping, ready for review, importing, finished, failed or discarded) and its progress. The system MUST reject files that are not `.xlsx` workbooks with a validation error, and SHALL report a workbook that cannot be read as a failed job with the reason.

#### Scenario: Workbook accepted
- **WHEN** a user uploads "ceramiche.xlsx" with the object type "Ceramics"
- **THEN** the system creates an import job, returns its identifier, and the job moves from reading to awaiting mapping

#### Scenario: Wrong file type
- **WHEN** a user uploads a `.csv` or `.xls` file
- **THEN** the system rejects the upload with a validation error that names the file

### Requirement: Rows, group headings and pictures are read from the sheet
The system SHALL read the first non-empty row of the chosen sheet as column headings and every following non-empty row as either a group heading or a record row. A row SHALL count as a group heading when only its first cell has a value and that value starts with a number followed by a full stop (such as "01. CINA"); the heading text after the number is the collection group text of the record rows that follow it, until the next heading, and is curated against the collection group list like the other list values. Pictures embedded in the sheet SHALL be attached to the record row in which they are anchored, in reading order (top to bottom, then left to right). A picture anchored on a row that is not a record row is attached to the nearest record row above it and reported as a warning.

#### Scenario: Group heading sets the collection group
- **WHEN** a sheet contains the heading row "01. CINA" followed by two record rows and then the heading "02. TURCHIA"
- **THEN** both record rows get the collection group text "CINA" and the group number "01"

#### Scenario: Two pictures on one row
- **WHEN** a record row has two pictures anchored on it, side by side
- **THEN** both are attached to that record, the left one first

### Requirement: Column and line mapping with saved templates
The system SHALL let a user map each column to a catalogue field or ignore it. For a column whose cells contain several lines, the user SHALL be able to map lines instead: by what a line contains (an item-numbered title such as "02. Piatto Celadon", dimensions, a historical date, an auction lot, a price with currency, a catalogue number such as "W.389", a storage location code) or by position, plus a target for all remaining lines. A line rule's target SHALL be either a field or "Discard": lines a discarding rule matches are dropped in every row and imported nowhere. Every other line of a mapped column MUST end up in a field: lines that match no rule go to the column's remaining-lines target, and the user SHALL be told before import if a column has no such target. The system SHALL suggest a mapping from the column headings and from the content of the first rows; it never suggests "Discard". A mapping SHALL be saveable as a named template, from the mapping step or from the review step, and reusable for other workbooks; a template stores the column mapping, the line rules (including discarding rules), the inventory number pattern, the dimensions options, the place decisions, the date resolutions, the dimensions decisions, and the list and party decisions.

#### Scenario: Multi-line title cell
- **WHEN** the title column holds "01. Coppa Celadon" on the first line and "diam 17,6 x h 7" on the second
- **THEN** the preview shows the title "Coppa Celadon", item number "01" and dimensions "diam 17,6 x h 7"

#### Scenario: Acquisition cell split by content
- **WHEN** the acquisition column holds "Sotheby's London", "12 06 1990" and "lot 170 £ 7.865" on three lines
- **THEN** the preview shows the source text "Sotheby's London", the date 12 June 1990, lot "170" and the price 7865 GBP

#### Scenario: Nothing is dropped
- **WHEN** a line in a mapped column matches no rule, such as "acquistato fuori asta"
- **THEN** it is placed in that column's remaining-lines field, such as the acquisition note

#### Scenario: Line discarded by position
- **WHEN** the title column has a rule "line 3 → Discard" and a row's cell holds "01. Coppa Celadon", "diam 17,6 x h 7" and "vedi foto"
- **THEN** that row is imported with the title "Coppa Celadon" and the dimensions "diam 17,6 x h 7", and "vedi foto" is not imported anywhere

#### Scenario: Line discarded by content
- **WHEN** the acquisition column has a rule "price → Discard"
- **THEN** in every row the line holding a price is not imported and the acquisition has no price

#### Scenario: Template reused
- **WHEN** a user saves the mapping of "ceramiche.xlsx" as template "Collection workbook 2024" and later uploads "tessuti.xlsx" choosing that template
- **THEN** the new import starts with the saved mapping, line rules, inventory number pattern, dimensions options, and place, date, dimensions, list and party decisions

#### Scenario: Template saved after review
- **WHEN** a user decides places, dates, dimensions and list values and then saves a template from the review step
- **THEN** the template holds those decisions

### Requirement: Inventory numbers from a pattern
The system SHALL assign each imported record an inventory number built from a user-defined pattern. The pattern can combine fixed text with the group number, the item number, the sheet row number, a three-letter prefix taken from the sheet name (so one template serves several workbooks), and the value of a mapped field (such as a detected catalogue number), with optional zero padding, and can name a mapped field to use instead when it has a value. The system MUST report, as a problem for the later row, any two rows that produce the same inventory number, and any row whose pattern produces no value.

#### Scenario: Pattern from group and item
- **WHEN** the pattern is "CER-{group:2}-{item:2}" and a row has group "1" and item "2"
- **THEN** the row's inventory number is "CER-01-02"

#### Scenario: Catalogue number preferred
- **WHEN** the pattern prefers the detected catalogue number and a row's first column contains "W.389"
- **THEN** the row's inventory number is "W.389"

#### Scenario: Repeated inventory number
- **WHEN** rows 14 and 15 both produce "CER-01-02"
- **THEN** row 14 is ready to import and row 15 is a problem that names row 14

### Requirement: Place of production matching
The system SHALL list every distinct place-of-production text in the workbook with the number of rows that use it and up to three similar regions from the region reference list. A place text that equals a region's name in any content language (ignoring case, accents and surrounding punctuation) is an exact match and is suggested first; other regions are ranked by a similarity score over their names in every language that tolerates accents, case, punctuation and spelling variants. Names the user has linked to a region before (from the place decisions of any template) also count as names of that region. For each text the user SHALL be able to link a suggested or searched region, create a new region (with editable names per language and an optional parent) and link it, or keep the text without a region. Place texts that a template has already decided SHALL be pre-filled with that decision. The place text itself is always imported exactly as written.

#### Scenario: Italian name matches exactly
- **WHEN** a workbook uses the place "Turchia" and the region "Turkey" has the Italian name "Turchia"
- **THEN** "Turkey" is suggested first, as an exact match

#### Scenario: Similar region suggested
- **WHEN** a workbook uses the place "Cina" and the region list contains "China"
- **THEN** "China" is among the suggestions for "Cina"

#### Scenario: New region created during import
- **WHEN** no suggestion fits "Anatolia occidentale" and the user chooses to create it under the parent "Anatolia"
- **THEN** the region "Anatolia occidentale" is added to the region list and linked for every row with that place

#### Scenario: Uncertain place kept unlinked
- **WHEN** the user keeps "Anatolia o Asia Centrale?" without a region
- **THEN** those rows are imported with that place text and no region

### Requirement: Resolving dates that are not understood
The system SHALL list every distinct date text that the historical date rules cannot interpret, with the rows that use it. For each, the user MAY enter an earliest and a latest year, which then apply to every row with that text. Rows whose date text stays unresolved SHALL be reported as problems and not imported.

#### Scenario: Range supplied for an unusual date
- **WHEN** the date "19 Dhu'l hijja 797" is not understood and the user enters 1395 to 1395
- **THEN** rows with that date are imported with the display text "19 Dhu'l hijja 797" and the year range 1395 to 1395

### Requirement: Review before import
The system SHALL show, before anything is saved, every record row as it will be imported (inventory number, mapped fields with the names of the linked list entries and parties, and picture thumbnails), together with counts of rows that are ready, rows whose inventory number already exists in the catalogue (to be skipped), and rows with problems, and the problems of each row. Changing the mapping, pattern, place, date, list or party decisions SHALL update the review.

#### Scenario: Counts before import
- **WHEN** a workbook has 70 record rows, of which 3 repeat an inventory number and 2 have an unresolved date
- **THEN** the review shows 65 ready rows and 5 rows with problems, each with its reason

#### Scenario: Undecided list value
- **WHEN** a row's dynasty text "Sfavide" has no decision
- **THEN** the review lists the row as a problem naming the dynasty text, until the text is linked or created

### Requirement: Background import with per-row results
The system SHALL import the ready rows in the background after the user confirms. Each row SHALL be imported on its own, so that one failing row never prevents the others: the artefact, its fields and its pictures are saved together, or not at all for that row. Rows whose inventory number already exists in the catalogue SHALL be skipped and reported as already present. The job SHALL report progress while it runs and, when finished, a report listing imported rows (with a link to each artefact), skipped rows, and problem rows with sheet row number, field, original text and reason. The report SHALL be downloadable as a spreadsheet. Pictures in formats the catalogue does not accept SHALL be reported as warnings on their row, and the row is still imported without them. An import interrupted by a restart SHALL continue from the next unfinished row.

#### Scenario: Good rows complete despite problems
- **WHEN** a user imports a workbook with 65 ready rows and 5 problem rows
- **THEN** 65 artefacts are created with their pictures and the report lists the 5 problem rows with their reasons

#### Scenario: Re-import adds only the missing rows
- **WHEN** a user fixes the 5 problem rows in the workbook and imports it again with the same template
- **THEN** the 65 existing rows are reported as already present and only the 5 fixed rows are created

#### Scenario: Unsupported picture format
- **WHEN** a row's picture is a JPEG XR image
- **THEN** the row is imported without that picture and the report lists a warning naming the picture

#### Scenario: Restart during import
- **WHEN** the system restarts while an import is running
- **THEN** the job continues after the restart from the first row not yet imported, without creating duplicates

### Requirement: Import wizard
The frontend SHALL provide an Import section that lists past and running imports with their state and counts, and a wizard that guides a user through upload, mapping (with live sample rows), inventory numbers, places, list values and parties, dates, review, and import progress, ending with the report. A user SHALL be able to save the import's mapping and decisions as a template from the mapping step and from the review step, to leave the wizard and return to an import in progress, and to discard an import that has not been committed.

#### Scenario: Return to an import
- **WHEN** a user closes the browser while an import is running and later opens the Import section
- **THEN** the import is listed with its current progress and its report once finished

#### Scenario: Discard before commit
- **WHEN** a user discards an import at the review step
- **THEN** nothing is added to the catalogue and the uploaded workbook and extracted pictures are removed

### Requirement: Curating list values and parties
The system SHALL list, for the collection group, dynasty and storage location lists and for parties (as acquisition source and as holder), every distinct text the workbook gives for that field, with the number of rows using it and up to three matching entries. A text equal to an entry's name (in any language for translated lists), ignoring case, accents and surrounding punctuation, is an exact match and is pre-filled as the decision; other entries are ranked by the same similarity used for places, and texts linked before in any template also count as names of their entry. For each text the user SHALL either link an entry (a suggestion or any entry picked from the list) or create a new one (with names per language for translated lists, and a kind for parties). Every text MUST have a decision: rows using an undecided text are reported as problems and not imported. Entries marked for creation are created once when the import is committed, before any row, and every row using the text links to them. An empty cell means no value and needs no decision.

#### Scenario: Exact match pre-filled
- **WHEN** a workbook uses the dynasty "Safavide" and the dynasty list has an entry named "Safavide"
- **THEN** the text is linked to that entry without the user deciding

#### Scenario: Variant linked to an existing entry
- **WHEN** a workbook uses the dynasty "Sfavide" and the user links it to the suggested entry "Safavide"
- **THEN** every row with "Sfavide" is imported with the dynasty "Safavide"

#### Scenario: Source picked from parties though no match
- **WHEN** a workbook uses the source text "Paris, Drouot-Richelieu Jean Dupont, Paris", no party is similar, and the user picks the party "Drouot" from the list
- **THEN** the rows with that text are imported with the source "Drouot"

#### Scenario: Party created during import
- **WHEN** the user creates the party "Rossi Antiquariato" (organisation) for the holder text "Rossi Antiquariato srl"
- **THEN** the party is added once at commit and every row with that holder text links to it

### Requirement: Templates learn from committed imports
When an import that was started from a template is committed, the system SHALL update that template with the import's place decisions, date resolutions, and list and party decisions, keeping the template's decisions for texts the import did not decide. Its mapping is updated only when the user saves the template.

#### Scenario: Decisions carried to the next workbook
- **WHEN** a user links "Sfavide" to "Safavide" in an import started from template "Collection workbook 2024" and commits it
- **THEN** a later import with that template pre-fills "Sfavide" as linked to "Safavide"

### Requirement: Catalogue entry language in the mapping
When a user maps a column to the catalogue entry, the system SHALL also record the language of that text, and SHALL import it as the artefact's catalogue entry in that language. The suggested mapping SHALL propose a language for a column from its heading (a heading in English suggests English, one in Italian suggests Italian) and English when the heading does not say; the user SHALL be able to change it. The language is saved with a template. Columns in several languages are mapped as one column per language.

#### Scenario: English column
- **WHEN** a column headed "English catalogue entry" is mapped to the catalogue entry
- **THEN** the suggested language is English and each imported artefact gets that text as its English catalogue entry

#### Scenario: Italian column chosen by the user
- **WHEN** a user maps a column to the catalogue entry and chooses Italian
- **THEN** each imported artefact gets that text as its Italian catalogue entry and no English one

#### Scenario: Two languages from two columns
- **WHEN** one column is mapped to the catalogue entry in English and another in Italian
- **THEN** each imported artefact has both catalogue entries

#### Scenario: Template keeps the language
- **WHEN** a user saves a mapping with a catalogue entry column in Italian as a template and reuses it for another workbook
- **THEN** the mapping starts with that column mapped to the Italian catalogue entry

### Requirement: Measurements read from the dimensions text
The system SHALL read each row's dimensions text for measurements. A number preceded by a label is assigned by its label: "h", "alt", "altezza", "height" to height; "l", "lungh", "lunghezza", "length" to length; "w", "larg", "larghezza", "width" to width; "diam", "diametro", "Ø", "diameter" to diameter (labels in any case, with or without a full stop). Decimal commas and points are both accepted. A unit written after a number, or once at the end of the text, applies to the numbers it follows; it is matched against the abbreviations and names of the units of measure (also "inches" and `"` for inch). When no unit is written, the mapping's default unit applies, if one is chosen. Numbers without labels, such as "82 x 64 cm", are assigned only when the mapping gives an order for unlabelled values (for example height × width × length). A text is read clearly only when every number in it is assigned to a different measurement and every measurement has a unit; then the row's measurements are filled in. Otherwise the text is unclear and goes to the dimensions step. The dimensions text itself is always imported exactly as written.

#### Scenario: Labelled values with a written unit
- **WHEN** a row's dimensions text is "diam 17,6 x h 7 cm"
- **THEN** the row is imported with the diameter 17.6 cm and the height 7 cm

#### Scenario: Default unit
- **WHEN** a row's dimensions text is "h 30 x l 45" and the mapping's default unit is centimetre
- **THEN** the row is imported with the height 30 cm and the length 45 cm

#### Scenario: Unlabelled values with an order
- **WHEN** a row's dimensions text is "82 x 64 cm" and the mapping says unlabelled values are height × width
- **THEN** the row is imported with the height 82 cm and the width 64 cm

#### Scenario: Unclear text
- **WHEN** a row's dimensions text is "chiuso 12 x 8; aperto 24 x 8"
- **THEN** the text is listed in the dimensions step and the row has no measurements until it is decided

### Requirement: Resolving dimensions that are not clear
The system SHALL list every distinct dimensions text that could not be read clearly, with the rows that use it. For each, the user SHALL either enter the measurements (any of width, height, length and diameter, each with a unit), which then apply to every row with that text, or choose to keep the text without measurements. The user SHALL be able to keep all remaining texts without measurements in one action. Rows whose dimensions text has no decision SHALL be reported as problems and not imported. Templates SHALL carry these decisions to later imports, and an import started from a template pre-fills the texts it has decided.

#### Scenario: Measurements entered for a text
- **WHEN** the text "chiuso 12 x 8; aperto 24 x 8" is unclear and the user enters a height of 12 cm and a width of 8 cm
- **THEN** every row with that text is imported with that text and those measurements

#### Scenario: Kept as text only
- **WHEN** the user keeps the text "misure varie" without measurements
- **THEN** rows with that text are imported with the dimensions text and no measurements

#### Scenario: Undecided text blocks its rows
- **WHEN** an unclear dimensions text has no decision
- **THEN** the review lists the rows using it as problems naming the text

### Requirement: Review shows measurements and discarded lines
The review SHALL show each row's measurements with their units next to its dimensions text, and SHALL show the lines that discarding rules dropped from the row, marked as not imported.

#### Scenario: Discarded line visible in the review
- **WHEN** a row's line "vedi foto" is dropped by a discarding rule
- **THEN** the row's review shows "vedi foto" crossed out with the note that it is not imported

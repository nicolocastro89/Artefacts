# Spec Delta

## Purpose

Lets staff bring existing collection records into the catalogue from Excel workbooks, including the photographs embedded in them, with control over how cells map to fields and a clear account of every row that could not be imported.

## ADDED Requirements

### Requirement: Upload a workbook for import
The system SHALL accept an `.xlsx` workbook of up to 2 GB together with the object type its rows belong to, and start an import job that reads the workbook in the background. The job SHALL report its state (reading, awaiting mapping, ready for review, importing, finished, failed or discarded) and its progress. The system MUST reject files that are not `.xlsx` workbooks with a validation error, and SHALL report a workbook that cannot be read as a failed job with the reason.

#### Scenario: Workbook accepted
- **WHEN** a user uploads "ceramiche.xlsx" with the object type "Ceramics"
- **THEN** the system creates an import job, returns its identifier, and the job moves from reading to awaiting mapping

#### Scenario: Wrong file type
- **WHEN** a user uploads a `.csv` or `.xls` file
- **THEN** the system rejects the upload with a validation error that names the file

### Requirement: Rows, group headings and pictures are read from the sheet
The system SHALL read the first non-empty row of the chosen sheet as column headings and every following non-empty row as either a group heading or a record row. A row SHALL count as a group heading when only its first cell has a value and that value starts with a number followed by a full stop (such as "01. CINA"); the heading text after the number becomes the collection group of the record rows that follow it, until the next heading. Pictures embedded in the sheet SHALL be attached to the record row in which they are anchored, in reading order (top to bottom, then left to right). A picture anchored on a row that is not a record row is attached to the nearest record row above it and reported as a warning.

#### Scenario: Group heading sets the collection group
- **WHEN** a sheet contains the heading row "01. CINA" followed by two record rows and then the heading "02. TURCHIA"
- **THEN** both record rows get the collection group "CINA" and the group number "01"

#### Scenario: Two pictures on one row
- **WHEN** a record row has two pictures anchored on it, side by side
- **THEN** both are attached to that record, the left one first

### Requirement: Column and line mapping with saved templates
The system SHALL let a user map each column to a catalogue field or ignore it. For a column whose cells contain several lines, the user SHALL be able to map lines instead: by what a line contains (an item-numbered title such as "02. Piatto Celadon", dimensions, a historical date, an auction lot, a price with currency, a catalogue number such as "W.389", a storage location code) or by position, plus a target for all remaining lines. Every line of a mapped column MUST end up in a field: lines that match no rule go to the column's remaining-lines target, and the user SHALL be told before import if a column has no such target. The system SHALL suggest a mapping from the column headings and from the content of the first rows. A mapping SHALL be saveable as a named template and reusable for other workbooks; a template stores the column mapping, the line rules, the inventory number pattern, and place decisions.

#### Scenario: Multi-line title cell
- **WHEN** the title column holds "01. Coppa Celadon" on the first line and "diam 17,6 x h 7" on the second
- **THEN** the preview shows the title "Coppa Celadon", item number "01" and dimensions "diam 17,6 x h 7"

#### Scenario: Acquisition cell split by content
- **WHEN** the acquisition column holds "Sotheby's London", "12 06 1990" and "lot 170 £ 7.865" on three lines
- **THEN** the preview shows the source "Sotheby's London", the date 12 June 1990, lot "170" and the price 7865 GBP

#### Scenario: Nothing is dropped
- **WHEN** a line in a mapped column matches no rule, such as "acquistato fuori asta"
- **THEN** it is placed in that column's remaining-lines field, such as the acquisition note

#### Scenario: Template reused
- **WHEN** a user saves the mapping of "ceramiche.xlsx" as template "Collection workbook 2024" and later uploads "tessuti.xlsx" choosing that template
- **THEN** the new import starts with the saved mapping, line rules, inventory number pattern and place decisions

### Requirement: Inventory numbers from a pattern
The system SHALL assign each imported record an inventory number built from a user-defined pattern. The pattern can combine fixed text with the group number, the item number, the sheet row number, and the value of a mapped field (such as a detected catalogue number), with optional zero padding, and can name a mapped field to use instead when it has a value. The system MUST report, as a problem for the later row, any two rows that produce the same inventory number, and any row whose pattern produces no value.

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
The system SHALL show, before anything is saved, every record row as it will be imported (inventory number, mapped fields and picture thumbnails), together with counts of rows that are ready, rows whose inventory number already exists in the catalogue (to be skipped), and rows with problems, and the problems of each row. Changing the mapping, pattern, place or date decisions SHALL update the review.

#### Scenario: Counts before import
- **WHEN** a workbook has 70 record rows, of which 3 repeat an inventory number and 2 have an unresolved date
- **THEN** the review shows 65 ready rows and 5 rows with problems, each with its reason

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
The frontend SHALL provide an Import section that lists past and running imports with their state and counts, and a wizard that guides a user through upload, mapping (with live sample rows), inventory numbers, places, dates, review, and import progress, ending with the report. A user SHALL be able to leave the wizard and return to an import in progress, and to discard an import that has not been committed.

#### Scenario: Return to an import
- **WHEN** a user closes the browser while an import is running and later opens the Import section
- **THEN** the import is listed with its current progress and its report once finished

#### Scenario: Discard before commit
- **WHEN** a user discards an import at the review step
- **THEN** nothing is added to the catalogue and the uploaded workbook and extracted pictures are removed

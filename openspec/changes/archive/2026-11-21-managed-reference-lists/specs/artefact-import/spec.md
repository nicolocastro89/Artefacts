# Spec Delta

## MODIFIED Requirements

### Requirement: Rows, group headings and pictures are read from the sheet
The system SHALL read the first non-empty row of the chosen sheet as column headings and every following non-empty row as either a group heading or a record row. A row SHALL count as a group heading when only its first cell has a value and that value starts with a number followed by a full stop (such as "01. CINA"); the heading text after the number is the collection group text of the record rows that follow it, until the next heading, and is curated against the collection group list like the other list values. Pictures embedded in the sheet SHALL be attached to the record row in which they are anchored, in reading order (top to bottom, then left to right). A picture anchored on a row that is not a record row is attached to the nearest record row above it and reported as a warning.

#### Scenario: Group heading sets the collection group
- **WHEN** a sheet contains the heading row "01. CINA" followed by two record rows and then the heading "02. TURCHIA"
- **THEN** both record rows get the collection group text "CINA" and the group number "01"

#### Scenario: Two pictures on one row
- **WHEN** a record row has two pictures anchored on it, side by side
- **THEN** both are attached to that record, the left one first

### Requirement: Column and line mapping with saved templates
The system SHALL let a user map each column to a catalogue field or ignore it. For a column whose cells contain several lines, the user SHALL be able to map lines instead: by what a line contains (an item-numbered title such as "02. Piatto Celadon", dimensions, a historical date, an auction lot, a price with currency, a catalogue number such as "W.389", a storage location code) or by position, plus a target for all remaining lines. Every line of a mapped column MUST end up in a field: lines that match no rule go to the column's remaining-lines target, and the user SHALL be told before import if a column has no such target. The system SHALL suggest a mapping from the column headings and from the content of the first rows. A mapping SHALL be saveable as a named template, from the mapping step or from the review step, and reusable for other workbooks; a template stores the column mapping, the line rules, the inventory number pattern, the place decisions, the date resolutions, and the list and party decisions.

#### Scenario: Multi-line title cell
- **WHEN** the title column holds "01. Coppa Celadon" on the first line and "diam 17,6 x h 7" on the second
- **THEN** the preview shows the title "Coppa Celadon", item number "01" and dimensions "diam 17,6 x h 7"

#### Scenario: Acquisition cell split by content
- **WHEN** the acquisition column holds "Sotheby's London", "12 06 1990" and "lot 170 £ 7.865" on three lines
- **THEN** the preview shows the source text "Sotheby's London", the date 12 June 1990, lot "170" and the price 7865 GBP

#### Scenario: Nothing is dropped
- **WHEN** a line in a mapped column matches no rule, such as "acquistato fuori asta"
- **THEN** it is placed in that column's remaining-lines field, such as the acquisition note

#### Scenario: Template reused
- **WHEN** a user saves the mapping of "ceramiche.xlsx" as template "Collection workbook 2024" and later uploads "tessuti.xlsx" choosing that template
- **THEN** the new import starts with the saved mapping, line rules, inventory number pattern, and place, date, list and party decisions

#### Scenario: Template saved after review
- **WHEN** a user decides places, dates and list values and then saves a template from the review step
- **THEN** the template holds those decisions

### Requirement: Review before import
The system SHALL show, before anything is saved, every record row as it will be imported (inventory number, mapped fields with the names of the linked list entries and parties, and picture thumbnails), together with counts of rows that are ready, rows whose inventory number already exists in the catalogue (to be skipped), and rows with problems, and the problems of each row. Changing the mapping, pattern, place, date, list or party decisions SHALL update the review.

#### Scenario: Counts before import
- **WHEN** a workbook has 70 record rows, of which 3 repeat an inventory number and 2 have an unresolved date
- **THEN** the review shows 65 ready rows and 5 rows with problems, each with its reason

#### Scenario: Undecided list value
- **WHEN** a row's dynasty text "Sfavide" has no decision
- **THEN** the review lists the row as a problem naming the dynasty text, until the text is linked or created

### Requirement: Import wizard
The frontend SHALL provide an Import section that lists past and running imports with their state and counts, and a wizard that guides a user through upload, mapping (with live sample rows), inventory numbers, places, list values and parties, dates, review, and import progress, ending with the report. A user SHALL be able to save the import's mapping and decisions as a template from the mapping step and from the review step, to leave the wizard and return to an import in progress, and to discard an import that has not been committed.

#### Scenario: Return to an import
- **WHEN** a user closes the browser while an import is running and later opens the Import section
- **THEN** the import is listed with its current progress and its report once finished

#### Scenario: Discard before commit
- **WHEN** a user discards an import at the review step
- **THEN** nothing is added to the catalogue and the uploaded workbook and extracted pictures are removed

## ADDED Requirements

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

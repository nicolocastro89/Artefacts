# Spec Delta

## MODIFIED Requirements

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

## ADDED Requirements

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

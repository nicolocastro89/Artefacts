# Spec Delta

## ADDED Requirements

### Requirement: Entering measurements
The artefact form SHALL show, next to the dimensions text, a value and a unit picker for each of width, height, length and diameter. A measurement left empty is not saved. When a value is entered, the unit picker SHALL default to the unit most recently used in the form, or centimetre. Validation errors SHALL be shown next to the measurement they concern.

#### Scenario: Height entered in centimetres
- **WHEN** a user enters 7 as the height, keeps the unit "cm" and saves
- **THEN** the artefact has the height 7 cm

#### Scenario: Measurement removed
- **WHEN** a user clears the width value of an artefact and saves
- **THEN** the artefact no longer has a width

### Requirement: Showing dimensions text and measurements together
The detail page, the print page and the record and collection displays SHALL show the dimensions text as written, followed by the measurements that exist, each with its abbreviated label and unit (for example "diam 17,6 x h 7 · Ø 17.6 cm · H 7 cm"). When there is no dimensions text, the measurements are shown on their own.

#### Scenario: Both shown on the detail page
- **WHEN** a user opens an artefact with the dimensions text "diam 17,6 x h 7", a diameter of 17.6 cm and a height of 7 cm
- **THEN** the dimensions field shows the text followed by "Ø 17.6 cm · H 7 cm"

### Requirement: Unit of measure management
The frontend SHALL provide a page under Reference that lists the units of measure with their names, abbreviation, factor and usage count, and lets a user add a unit, edit its names per language, abbreviation and factor, and delete an unused unit.

#### Scenario: Unit added from the page
- **WHEN** a user adds "Foot", "ft", 0.3048 on the units page
- **THEN** the unit appears in the list and in the measurement unit pickers

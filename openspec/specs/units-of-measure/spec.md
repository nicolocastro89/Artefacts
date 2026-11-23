# units-of-measure Specification

## Purpose
Keeps a managed list of units of measure for artefact measurements, each with a name per language, an abbreviation, and a conversion factor to the SI base unit, so that measurements in different units can be compared and converted.

## Requirements

### Requirement: Units of measure
The system SHALL keep a list of units of measure. A unit SHALL have a full name in one or more content languages (resolved in the request's language when read, with the rules for translatable values), an abbreviation, and a conversion factor: the number of SI base units (metres) in one of the unit. An abbreviation MUST be present, at most 20 characters, and unique among units ignoring case and surrounding whitespace. Names MUST be unique within each language, ignoring case and surrounding whitespace. The factor MUST be a number greater than zero.

#### Scenario: Unit with its factor
- **WHEN** a user reads the unit "Millimetre"
- **THEN** it has the abbreviation "mm" and the factor 0.001

#### Scenario: Duplicate abbreviation
- **WHEN** a user adds a unit with the abbreviation "CM" while "cm" exists
- **THEN** the system rejects it with a validation error on the abbreviation

#### Scenario: Factor must be positive
- **WHEN** a user saves a unit with the factor 0
- **THEN** the system rejects it with a validation error on the factor

### Requirement: Seeded units
A new database SHALL contain the units millimetre ("mm", 0.001), centimetre ("cm", 0.01), metre ("m", 1) and inch ("in", 0.0254), with English and Italian names.

#### Scenario: Fresh database
- **WHEN** the system starts on an empty database and a user lists the units in Italian
- **THEN** the list contains "millimetro", "centimetro", "metro" and "pollice" with their abbreviations and factors

### Requirement: List, add, change and delete units
The system SHALL return every unit with its resolved name, its names in every language, its abbreviation, its factor and the number of artefacts that use it, ordered by factor. A user SHALL be able to add a unit, change its names, abbreviation and factor, and delete it. Changing a unit's factor changes the value in metres of every measurement in that unit; the measured numbers themselves are kept. Deleting a unit that any measurement uses MUST be rejected with a conflict.

#### Scenario: Unit added
- **WHEN** a user adds the unit "Foot" with the abbreviation "ft" and the factor 0.3048
- **THEN** it is listed between centimetre and metre and can be chosen for measurements

#### Scenario: Unit in use cannot be deleted
- **WHEN** a user deletes "cm" while an artefact has a height in centimetres
- **THEN** the system rejects the deletion with a conflict naming the number of artefacts using it

#### Scenario: Unused unit deleted
- **WHEN** a user deletes a unit no measurement uses
- **THEN** it is removed from the list

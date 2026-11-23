# Spec Delta

## MODIFIED Requirements

### Requirement: Artefact read view
The system SHALL provide a read-only, flattened artefact view. Each row contains the artefact's identifier, inventory number, title, maker, creation date (display text, earliest year, latest year), medium, dimensions, the width, height, length and diameter (each as value, unit identifier and value in metres), object type identifier, collection group identifier, place of production (text and region identifier), dynasty identifier, holder identifier, storage location identifier, acquisition method, date, source identifier, lot and note, acquisition price amount and currency, valuation amount and currency, the number of exhibitions, the primary image reference, and the number of images. Names of linked records (object types, regions, collection groups, dynasties, storage locations, parties and units of measure) are not columns of the view: queries over the view resolve them, in the request's language where they are translated. The view SHALL reflect the current state of the catalogue, including the current factor of each unit.

#### Scenario: View reflects updates
- **WHEN** a user changes an artefact's title and then reads the artefact read view
- **THEN** the view returns the new title

#### Scenario: Artefact without images
- **WHEN** an artefact has no images
- **THEN** its read view row has no primary image reference and an image count of 0

#### Scenario: Renamed object type
- **WHEN** a user changes the English name of an artefact's object type and then lists artefacts in English
- **THEN** the list item shows the new object type name

#### Scenario: Renamed party
- **WHEN** a user renames the party "Christies" to "Christie's" and then searches artefacts for "Christie's"
- **THEN** the artefacts acquired from that party are found

#### Scenario: Measurement in metres
- **WHEN** an artefact has a height of 7 cm
- **THEN** its read view row has the height value 7, the centimetre unit identifier and the height in metres 0.07

# Spec Delta

## MODIFIED Requirements

### Requirement: Artefact read view
The system SHALL provide a read-only, flattened artefact view. Each row contains the artefact's identifier, inventory number, title, maker, creation date (display text, earliest year, latest year), medium, dimensions, object type (identifier and name), collection group, place of production (text, region identifier and region name), dynasty, holder, storage location, acquisition method, date, source, lot and note, acquisition price amount and currency, valuation amount and currency, the number of exhibitions, the primary image reference, and the number of images. The view SHALL reflect the current state of the catalogue.

#### Scenario: View reflects updates
- **WHEN** a user changes an artefact's title and then reads the artefact read view
- **THEN** the view returns the new title

#### Scenario: Artefact without images
- **WHEN** an artefact has no images
- **THEN** its read view row has no primary image reference and an image count of 0

#### Scenario: Renamed object type
- **WHEN** a user renames the object type of an artefact and then reads the artefact read view
- **THEN** the row shows the new object type name

### Requirement: Sensitivity classification
Each catalog field SHALL have an optional sensitivity tag. The acquisition price amount and currency fields and the valuation amount and currency fields MUST be tagged `financial`. This change does not restrict access by sensitivity; the tag exists so that a later permission feature can enforce access.

#### Scenario: Price fields are tagged
- **WHEN** a client requests the field catalog
- **THEN** the acquisition price amount and currency fields carry the sensitivity tag `financial`
- **AND** non-sensitive fields such as "title" carry no sensitivity tag

#### Scenario: Valuation fields are tagged
- **WHEN** a client requests the field catalog
- **THEN** the valuation amount and currency fields carry the sensitivity tag `financial`

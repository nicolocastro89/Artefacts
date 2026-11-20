# read-model Specification

## Purpose

Provides read-only views of collection data that are shaped for display and querying, together with a machine-readable catalog of the fields those views expose. This is the stable contract that future custom views and the semantic layer are built on.

## Requirements

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

### Requirement: Field catalog
The system SHALL publish a field catalog. For each read view (an "entity") it lists every field's stable identifier, human-readable label, data type (text, number, date, historical-date, money, image, reference, or boolean), and whether the field can be filtered and sorted. The catalog SHALL describe exactly the fields that the read views expose: no missing fields and no extra ones.

#### Scenario: Catalog describes artefact fields
- **WHEN** a client requests the field catalog
- **THEN** the response contains an "Artefact" entity that lists, among others, a field "title" of type text and a field "primaryImage" of type image

#### Scenario: Catalog stays in sync
- **WHEN** a release adds a field to a read view
- **THEN** the field catalog in that release lists the new field, with no manual catalog edit required

### Requirement: Sensitivity classification
Each catalog field SHALL have an optional sensitivity tag. The acquisition price amount and currency fields and the valuation amount and currency fields MUST be tagged `financial`. This change does not restrict access by sensitivity; the tag exists so that a later permission feature can enforce access.

#### Scenario: Price fields are tagged
- **WHEN** a client requests the field catalog
- **THEN** the acquisition price amount and currency fields carry the sensitivity tag `financial`
- **AND** non-sensitive fields such as "title" carry no sensitivity tag

#### Scenario: Valuation fields are tagged
- **WHEN** a client requests the field catalog
- **THEN** the valuation amount and currency fields carry the sensitivity tag `financial`

### Requirement: API description
The system SHALL publish a machine-readable description of its HTTP API, including the response schema of every endpoint, so that the frontend can discover which fields an endpoint returns.

#### Scenario: Response schema discoverable
- **WHEN** a client requests the API description
- **THEN** it contains the artefact detail endpoint with a response schema that includes the title and the image URL fields

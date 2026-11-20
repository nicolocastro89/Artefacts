# Spec Delta

## MODIFIED Requirements

### Requirement: Artefact read view
The system SHALL provide a read-only, flattened artefact view. Each row contains the artefact's identifier, inventory number, title, maker, creation date (display text, earliest year, latest year), medium, dimensions, object type identifier, collection group, place of production (text and region identifier), dynasty, holder, storage location, acquisition method, date, source, lot and note, acquisition price amount and currency, valuation amount and currency, the number of exhibitions, the primary image reference, and the number of images. Names that exist per language (object type and region names) are not columns of the view: queries over the view resolve them in the request's language. The view SHALL reflect the current state of the catalogue.

#### Scenario: View reflects updates
- **WHEN** a user changes an artefact's title and then reads the artefact read view
- **THEN** the view returns the new title

#### Scenario: Artefact without images
- **WHEN** an artefact has no images
- **THEN** its read view row has no primary image reference and an image count of 0

#### Scenario: Renamed object type
- **WHEN** a user changes the English name of an artefact's object type and then lists artefacts in English
- **THEN** the list item shows the new object type name

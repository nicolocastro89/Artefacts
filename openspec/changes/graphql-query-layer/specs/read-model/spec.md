# Spec Delta

## ADDED Requirements

### Requirement: Read views of an artefact's lists
The system SHALL provide read-only views of an artefact's provenance entries (artefact, position, region identifier, period display text, earliest and latest year, precision, note), images (artefact, media asset, storage reference, original file name, format, size, upload time, caption, whether primary, attachment time), exhibitions (artefact, position, text) and descriptions (artefact, language, text). Like the artefact read view, they reflect the current state of the catalogue, and names of linked records are resolved by queries rather than stored as columns. The field catalog SHALL describe each of them as an entity.

#### Scenario: Provenance view keeps the order
- **WHEN** a user reorders an artefact's provenance entries and then reads the provenance view
- **THEN** the view returns the entries with positions in the new order

#### Scenario: Catalog lists the new entities
- **WHEN** a client requests the field catalog
- **THEN** it contains the entities for provenance entries, images, exhibitions and descriptions, each with its fields and types

# Spec Delta

## MODIFIED Requirements

### Requirement: Create and edit artefacts
The frontend SHALL let a user create an artefact and edit an existing one. This includes historical date entry with immediate feedback on how the text was interpreted, picking or adding a region inline for provenance entries and for the place of production, picking or adding inline an object type, a collection group, a dynasty, a storage location, and a party as holder and as acquisition source, editing the list of exhibitions, recording a valuation, uploading images, choosing the primary image, editing captions and deleting images. Validation errors from the system SHALL be shown next to the field they concern.

#### Scenario: Date interpretation feedback
- **WHEN** a user types "late 17th century" into a historical date field
- **THEN** the form shows the interpreted range 1667 to 1700 before the user saves

#### Scenario: Inline region creation
- **WHEN** a user types a region name that does not exist in the provenance region picker and confirms it
- **THEN** the region is added to the reference list and selected for that entry

#### Scenario: Inline object type creation
- **WHEN** a user types an object type that does not exist and confirms it
- **THEN** the object type is added to the reference list and selected for the artefact

#### Scenario: Server validation shown on field
- **WHEN** saving fails because the inventory number is already used
- **THEN** the error is shown next to the inventory number field

#### Scenario: Source picked from parties
- **WHEN** a user types "Sothe" in the acquisition source picker and chooses "Sotheby's"
- **THEN** the artefact is saved with that party as its acquisition source

#### Scenario: Inline party creation
- **WHEN** a user types a seller that is not yet a party, chooses its kind and confirms it
- **THEN** the party is added and selected as the source

## ADDED Requirements

### Requirement: Reference list and party management
The frontend SHALL provide, among the reference lists, a page each for dynasties, collection groups and storage locations (names per language where the list is translated) and a page for parties (name and kind), each for listing with usage counts, searching, adding, renaming and deleting, and SHALL show the system's error when an entry or party in use cannot be deleted.

#### Scenario: Rename a dynasty in both languages
- **WHEN** a user renames the dynasty "Ilkahnide" to "Ilkhanide" (it) and "Ilkhanid" (en)
- **THEN** the page shows both names and every artefact of that dynasty shows the new name

#### Scenario: Delete a party in use
- **WHEN** a user tries to delete a party that artefacts were acquired from
- **THEN** the page explains that the party is in use and keeps it in the list

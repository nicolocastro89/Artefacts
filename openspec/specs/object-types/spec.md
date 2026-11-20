# object-types Specification

## Purpose

Keeps a managed reference list of object types (such as Ceramics, Carpets or Textiles) so that artefacts are classified consistently and the collection can be browsed and grouped by kind of object.

## Requirements

### Requirement: List and search object types
The system SHALL return the list of object types, optionally filtered by a case-insensitive text match on the name, together with the number of artefacts of each type.

#### Scenario: Type-ahead search
- **WHEN** a user searches object types for "cer"
- **THEN** the system returns object types whose name contains "cer", such as "Ceramics"

#### Scenario: Usage count
- **WHEN** three artefacts have the object type "Carpets" and a user lists object types
- **THEN** "Carpets" is returned with an artefact count of 3

### Requirement: Add or rename an object type
The system SHALL allow a user to add an object type by name and to rename an existing one. Object type names MUST be unique, ignoring case and surrounding whitespace. Adding a name that already exists SHALL return the existing object type instead of creating a duplicate.

#### Scenario: Add new object type
- **WHEN** a user adds the object type "Metalwork" and none with that name exists
- **THEN** the system creates the object type and returns it

#### Scenario: Add existing object type
- **WHEN** a user adds " carpets " and "Carpets" already exists
- **THEN** the system returns the existing "Carpets" object type and creates nothing new

#### Scenario: Rename propagates
- **WHEN** a user renames "Tappeti" to "Carpets"
- **THEN** every artefact of that type shows "Carpets"

#### Scenario: Rename collision
- **WHEN** a user renames an object type to a name another object type already has
- **THEN** the system rejects the rename with a validation error

### Requirement: Object types in use cannot be deleted
The system SHALL allow an object type to be deleted only if no artefact references it.

#### Scenario: Delete referenced object type
- **WHEN** a user deletes an object type that an artefact references
- **THEN** the system rejects the deletion and reports that the object type is in use
- **AND** the object type remains in the list

#### Scenario: Delete unused object type
- **WHEN** a user deletes an object type that no artefact references
- **THEN** the object type is removed from the list

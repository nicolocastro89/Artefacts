# object-types Specification

## Purpose

Keeps a managed reference list of object types (such as Ceramics, Carpets or Textiles) so that artefacts are classified consistently and the collection can be browsed and grouped by kind of object.

## Requirements

### Requirement: List and search object types
The system SHALL return the list of object types, each with its name resolved in the request's language, its names in every language, and the number of artefacts of that type, optionally filtered by a case-insensitive text match on a name in any language. The list is ordered by the resolved name.

#### Scenario: Type-ahead search
- **WHEN** a user searches object types for "cer"
- **THEN** the system returns object types whose name contains "cer", such as "Ceramics"

#### Scenario: Usage count
- **WHEN** three artefacts have the object type "Carpets" and a user lists object types
- **THEN** "Carpets" is returned with an artefact count of 3

#### Scenario: Search matches another language
- **WHEN** a user reading in English searches for "tappeti" and the object type "Carpets" has the Italian name "Tappeti"
- **THEN** the system returns "Carpets"

### Requirement: Add or rename an object type
The system SHALL allow a user to add an object type with a name in one or more languages, and to change the names of an existing one. An object type MUST have a name in at least one language. Names MUST be unique within each language, ignoring case and surrounding whitespace. Adding an object type whose name matches an existing one's name in the same language SHALL return the existing object type instead of creating a duplicate.

#### Scenario: Add new object type
- **WHEN** a user adds the object type named "Metalwork" (en) and none has that English name
- **THEN** the system creates the object type and returns it

#### Scenario: Add existing object type
- **WHEN** a user adds " carpets " (en) and "Carpets" is already an English object type name
- **THEN** the system returns the existing "Carpets" object type and creates nothing new

#### Scenario: Rename propagates
- **WHEN** a user changes the Italian name "Tappeti" of an object type to "Tappeti e kilim"
- **THEN** every artefact of that type shows "Tappeti e kilim" when read in Italian

#### Scenario: Rename collision
- **WHEN** a user gives an object type an English name another object type already has in English
- **THEN** the system rejects the change with a validation error

### Requirement: Object types in use cannot be deleted
The system SHALL allow an object type to be deleted only if no artefact references it.

#### Scenario: Delete referenced object type
- **WHEN** a user deletes an object type that an artefact references
- **THEN** the system rejects the deletion and reports that the object type is in use
- **AND** the object type remains in the list

#### Scenario: Delete unused object type
- **WHEN** a user deletes an object type that no artefact references
- **THEN** the object type is removed from the list

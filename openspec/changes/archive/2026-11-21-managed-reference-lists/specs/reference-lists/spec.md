# Spec Delta

## Purpose

Keeps managed lists of dynasties, collection groups and storage locations, so that artefacts use one spelling for each value and the collection can be browsed and grouped by them.

## ADDED Requirements

### Requirement: Managed lists of dynasties, collection groups and storage locations
The system SHALL keep three reference lists: dynasties, collection groups and storage locations. A dynasty or collection group SHALL have a name in one or more content languages, resolved in the request's language when read, with the rules for translatable values. A storage location SHALL have a single name, since locations are codes ("MI F3", "Beta2"). Names MUST be unique within each list (and, for dynasties and collection groups, within each language), ignoring case and surrounding whitespace. An entry MUST have a name.

#### Scenario: Dynasty with names in two languages
- **WHEN** a user adds the dynasty with the names { "it": "Safavide", "en": "Safavid" }
- **THEN** reading it with `lang=en` returns "Safavid" and with `lang=it` returns "Safavide"

#### Scenario: Storage location has one name
- **WHEN** a user adds the storage location "MI F3"
- **THEN** the location is named "MI F3" in every language

#### Scenario: Same name in two lists
- **WHEN** the dynasty "Ottomana" exists and a user adds the collection group "Ottomana"
- **THEN** the system accepts it, because uniqueness applies within each list

### Requirement: List, search and add entries
The system SHALL return each list with every entry's resolved name, its names in every language (for translated lists) and the number of artefacts that use it, ordered by the resolved name and optionally filtered by a case-insensitive match on a name in any language. Adding an entry whose name matches an existing entry's name in the same list (and language) SHALL return the existing entry instead of creating a duplicate.

#### Scenario: Search matches another language
- **WHEN** a user reading in English searches dynasties for "safav" and the dynasty "Safavid" has the Italian name "Safavide"
- **THEN** the system returns "Safavid"

#### Scenario: Add an existing location
- **WHEN** a user adds the storage location " mi f3 " and "MI F3" exists
- **THEN** the system returns the existing location and creates nothing new

#### Scenario: Usage count
- **WHEN** four artefacts are stored at "Beta2" and a user lists storage locations
- **THEN** "Beta2" is returned with an artefact count of 4

### Requirement: Rename and delete entries
The system SHALL allow a user to change an entry's names; every artefact using the entry shows the new name. Renaming to a name another entry of the same list already has (in the same language) MUST be rejected with a validation error. Deleting an entry that any artefact uses MUST be rejected with a conflict; unused entries can be deleted.

#### Scenario: Rename propagates
- **WHEN** a user renames the dynasty "Ilkahnide" to "Ilkhanide"
- **THEN** every artefact of that dynasty shows "Ilkhanide"

#### Scenario: Entry in use cannot be deleted
- **WHEN** a user deletes the collection group "CINA" while artefacts belong to it
- **THEN** the system rejects the deletion and reports that the group is in use

# region-reference Specification

## Purpose

Provides a shared, user-maintained list of geographic regions that users pick from when recording where an artefact originated and passed through, so the same place is always referred to the same way.

## Requirements

### Requirement: List and search regions
The system SHALL return the list of regions, each with its name resolved in the request's language and with its names in every language, optionally filtered by a case-insensitive text match on a name in any language, so that a user can pick one. The list is ordered by the resolved name.

#### Scenario: Type-ahead search
- **WHEN** a user searches regions for "fla"
- **THEN** the system returns regions whose name contains "fla", such as "Flanders"

#### Scenario: Search matches another language
- **WHEN** a user reading in English searches regions for "turch" and the region "Turkey" has the Italian name "Turchia"
- **THEN** the system returns that region, named "Turkey"

### Requirement: Add or rename a region
The system SHALL allow a user to add a region with a name in one or more languages, and to change the names of an existing region. A region MUST have a name in at least one language. Names MUST be unique within each language, ignoring case and surrounding whitespace. Adding a region whose name matches an existing region's name in the same language SHALL return the existing region instead of creating a duplicate.

#### Scenario: Add new region
- **WHEN** a user adds the region named "Tuscany" (en) and no region has that English name
- **THEN** the system creates the region and returns it

#### Scenario: Add existing region
- **WHEN** a user adds the region named " flanders " (en) and "Flanders" is already an English region name
- **THEN** the system returns the existing "Flanders" region and creates nothing new

#### Scenario: Same text in different languages
- **WHEN** a user adds the Italian name "Vienna" while "Vienna" is the English name of another region
- **THEN** the system accepts it, because uniqueness applies within each language

#### Scenario: Rename propagates
- **WHEN** a user changes the English name of region "Holland" to "Netherlands"
- **THEN** every provenance entry that references that region shows "Netherlands" when read in English

#### Scenario: Rename collision
- **WHEN** a user gives a region an English name that another region already has in English
- **THEN** the system rejects the change with a validation error

#### Scenario: Region without any name
- **WHEN** a user saves a region with blank names in every language
- **THEN** the system rejects the request with a validation error on the names

### Requirement: Optional parent region
The system SHALL allow a region to have an optional parent region, forming a hierarchy such as country, then region, then city. The system MUST reject any change that would make a region its own ancestor.

#### Scenario: Cycle prevented
- **WHEN** a user sets the parent of "Belgium" to "Flanders" while "Flanders" already has "Belgium" as its parent
- **THEN** the system rejects the change with a validation error

### Requirement: Regions in use cannot be deleted
The system SHALL allow a region to be deleted only if no provenance entry, no artefact's place of production and no child region references it.

#### Scenario: Delete referenced region
- **WHEN** a user deletes a region that a provenance entry references
- **THEN** the system rejects the deletion and reports that the region is in use

#### Scenario: Delete region used as place of production
- **WHEN** a user deletes a region that an artefact's place of production references
- **THEN** the system rejects the deletion and reports that the region is in use

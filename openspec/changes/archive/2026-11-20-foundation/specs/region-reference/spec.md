# Spec Delta

## Purpose

Provides a shared, user-maintained list of geographic regions that users pick from when recording where an artefact originated and passed through, so the same place is always referred to the same way.

## ADDED Requirements

### Requirement: List and search regions
The system SHALL return the list of regions, optionally filtered by a case-insensitive text match on the name, so that a user can pick one.

#### Scenario: Type-ahead search
- **WHEN** a user searches regions for "fla"
- **THEN** the system returns regions whose name contains "fla", such as "Flanders"

### Requirement: Add or rename a region
The system SHALL allow a user to add a region by name and to rename an existing region. Region names MUST be unique, ignoring case and surrounding whitespace. Adding a name that already exists SHALL return the existing region instead of creating a duplicate.

#### Scenario: Add new region
- **WHEN** a user adds the region "Tuscany" and no region with that name exists
- **THEN** the system creates the region and returns it

#### Scenario: Add existing region
- **WHEN** a user adds the region " flanders " and "Flanders" already exists
- **THEN** the system returns the existing "Flanders" region and creates nothing new

#### Scenario: Rename propagates
- **WHEN** a user renames region "Holland" to "Netherlands"
- **THEN** every provenance entry that references that region shows "Netherlands"

#### Scenario: Rename collision
- **WHEN** a user renames a region to a name that another region already has
- **THEN** the system rejects the rename with a validation error

### Requirement: Optional parent region
The system SHALL allow a region to have an optional parent region, forming a hierarchy such as country, then region, then city. The system MUST reject any change that would make a region its own ancestor.

#### Scenario: Cycle prevented
- **WHEN** a user sets the parent of "Belgium" to "Flanders" while "Flanders" already has "Belgium" as its parent
- **THEN** the system rejects the change with a validation error

### Requirement: Regions in use cannot be deleted
The system SHALL allow a region to be deleted only if no provenance entry and no child region references it.

#### Scenario: Delete referenced region
- **WHEN** a user deletes a region that a provenance entry references
- **THEN** the system rejects the deletion and reports that the region is in use

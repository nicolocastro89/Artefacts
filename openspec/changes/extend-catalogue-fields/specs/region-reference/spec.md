# Spec Delta

## MODIFIED Requirements

### Requirement: Regions in use cannot be deleted
The system SHALL allow a region to be deleted only if no provenance entry, no artefact's place of production and no child region references it.

#### Scenario: Delete referenced region
- **WHEN** a user deletes a region that a provenance entry references
- **THEN** the system rejects the deletion and reports that the region is in use

#### Scenario: Delete region used as place of production
- **WHEN** a user deletes a region that an artefact's place of production references
- **THEN** the system rejects the deletion and reports that the region is in use

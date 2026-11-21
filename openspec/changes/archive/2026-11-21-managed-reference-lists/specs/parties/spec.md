# Spec Delta

## Purpose

Keeps the people and organisations the collection deals with (auction houses, dealers, private sellers, institutions, holders) as one managed list, so that an artefact's acquisition source and holder refer to the same records, and later loans can refer to them as borrowers.

## ADDED Requirements

### Requirement: Parties
The system SHALL keep a list of parties. A party SHALL have a single name and a kind, either person or organisation. Party names MUST be unique ignoring case and surrounding whitespace. Parties are not translated.

#### Scenario: Add an organisation
- **WHEN** a user adds the party "Sotheby's" as an organisation
- **THEN** the party is listed with the name "Sotheby's" and the kind organisation

#### Scenario: Add an existing party
- **WHEN** a user adds " sotheby's " and "Sotheby's" exists
- **THEN** the system returns the existing party and creates nothing new

### Requirement: List, search, rename and delete parties
The system SHALL return the parties ordered by name, optionally filtered by a case-insensitive match on the name or by kind, each with the number of artefacts that use it as acquisition source and as holder. A user SHALL be able to rename a party and change its kind; renaming to another party's name MUST be rejected with a validation error. Deleting a party that any artefact uses MUST be rejected with a conflict.

#### Scenario: Usage counts
- **WHEN** three artefacts were acquired from "Christie's" and one is held by it
- **THEN** "Christie's" is listed as the source of 3 artefacts and the holder of 1

#### Scenario: Party in use cannot be deleted
- **WHEN** a user deletes a party that is the acquisition source of an artefact
- **THEN** the system rejects the deletion and reports that the party is in use

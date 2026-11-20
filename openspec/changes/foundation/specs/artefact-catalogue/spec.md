# Spec Delta

## Purpose

Lets collection staff record and maintain the core catalogue entry for every artefact in the institution's collection: identity, description, historical dating, acquisition and provenance.

## ADDED Requirements

### Requirement: Create an artefact
The system SHALL allow a user to create an artefact with a unique inventory number and a title. Optional fields are maker, creation date, medium, dimensions and description. The system MUST reject an artefact whose inventory number is missing, is blank, or is already used by another artefact.

#### Scenario: Valid artefact is created
- **WHEN** a user submits an artefact with inventory number "INV-2026-001" and title "Still Life with Lemons"
- **THEN** the system stores the artefact and returns it with a system-assigned identifier

#### Scenario: Duplicate inventory number is rejected
- **WHEN** a user submits an artefact whose inventory number already belongs to another artefact
- **THEN** the system rejects the request with a validation error that names the inventory number field
- **AND** no artefact is created

#### Scenario: Missing title is rejected
- **WHEN** a user submits an artefact without a title
- **THEN** the system rejects the request with a validation error that names the title field

### Requirement: Update an artefact
The system SHALL allow a user to update an existing artefact's descriptive fields, creation date, acquisition and provenance. Updates are subject to the same validation rules as creation.

#### Scenario: Title is changed
- **WHEN** a user changes the title of an existing artefact
- **THEN** later reads of that artefact return the new title

#### Scenario: Updating a non-existent artefact
- **WHEN** a user updates an artefact identifier that does not exist
- **THEN** the system responds that the artefact was not found

### Requirement: Retrieve and list artefacts
The system SHALL return a single artefact by its identifier. It SHALL return a paginated list of artefacts that can be searched by free text (matching inventory number, title or maker) and sorted by inventory number, title or creation date.

#### Scenario: Paginated listing
- **WHEN** a user requests page 2 with a page size of 25
- **THEN** the system returns at most 25 artefacts together with the total count of matching artefacts

#### Scenario: Free-text search
- **WHEN** a user searches for "lemons"
- **THEN** the results include every artefact whose title, maker or inventory number contains "lemons", ignoring case

#### Scenario: Sorting by creation date uses the historical range
- **WHEN** a user sorts artefacts by creation date ascending
- **THEN** artefacts are ordered by the earliest year of their creation date range
- **AND** artefacts without a creation date appear last

### Requirement: Historical dates with precision
The system SHALL record historical dates (an artefact's creation date, and provenance periods) as three parts: the display text exactly as entered, a computed earliest and latest year, and a precision (exact year, decade, century, or approximate). The system SHALL accept at least these forms: a year ("1654"), a circa year ("c. 1650"), a decade ("1680s"), a century ("17th century"), a part of a century ("late 17th century", "early 18th century"), a year range ("1650-1660"), and a bounded year ("before 1720", "after 1800"). The system MUST reject text it cannot interpret, unless the user enters the earliest and latest years explicitly.

#### Scenario: Circa date
- **WHEN** a user enters a creation date of "c. 1650"
- **THEN** the system stores the display text "c. 1650", a year range that brackets 1650, and precision "approximate"

#### Scenario: Part-century date
- **WHEN** a user enters "late 17th century"
- **THEN** the system stores a range of 1667 to 1700 and precision "century"
- **AND** the date is displayed as "late 17th century"

#### Scenario: Uninterpretable text with explicit range
- **WHEN** a user enters "during the reign of Louis XIV" together with an earliest year of 1643 and a latest year of 1715
- **THEN** the system stores the display text unchanged together with the given range

#### Scenario: Uninterpretable text without explicit range
- **WHEN** a user enters "unknown era" with no explicit range
- **THEN** the system rejects the date with a validation error

### Requirement: Acquisition with monetary value
The system SHALL record an artefact's acquisition as a method (purchase, gift, bequest, transfer, or other), an acquisition date, a source, and an optional price. A price consists of an amount and an ISO 4217 currency code. The system MUST reject a price with a negative amount or an unrecognised currency code. The acquisition price SHALL be classified as financially sensitive information. In this change, sensitive information is still visible to every user.

#### Scenario: Purchase with price
- **WHEN** a user records a purchase on 2019-05-02 from "Galerie Example" for 12500.00 EUR
- **THEN** the artefact returns that acquisition with the amount 12500.00 and currency "EUR"

#### Scenario: Invalid currency
- **WHEN** a user records a price with currency code "XYZ"
- **THEN** the system rejects the request with a validation error that names the currency

### Requirement: Provenance entries
The system SHALL allow an artefact to have an ordered list of provenance entries. Each entry references a region from the region reference list and has a historical period (using the historical date rules) and an optional note. The system MUST reject an entry that references a region that does not exist.

#### Scenario: Adding provenance
- **WHEN** a user adds the provenance entries "Flanders, c. 1650" followed by "Paris, 1780s"
- **THEN** the artefact returns both entries in that order, each with its region name and period display text

#### Scenario: Unknown region
- **WHEN** a user adds a provenance entry that references a region identifier that does not exist
- **THEN** the system rejects the request with a validation error

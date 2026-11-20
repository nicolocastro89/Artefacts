# artefact-catalogue Specification

## Purpose

Lets collection staff record and maintain the core catalogue entry for every artefact in the institution's collection: identity, description, historical dating, acquisition and provenance.

## Requirements

### Requirement: Create an artefact
The system SHALL allow a user to create an artefact with a unique inventory number and a title. Optional fields are maker, creation date, medium, dimensions, description, and the classification, origin and holding fields. The system MUST reject an artefact whose inventory number is missing, is blank, or is already used by another artefact.

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
The system SHALL return a single artefact by its identifier. It SHALL return a paginated list of artefacts that can be searched by free text (matching inventory number, title, maker, collection group, place of production, dynasty, holder or storage location) and sorted by inventory number, title or creation date. The list SHALL optionally be grouped by object type, collection group, place-of-production region, century, or dynasty. When grouped, results are ordered by group first and then by the requested sort, each result carries its group key and label, and the response includes every group's label and total count for the current search. Artefacts without a value for the grouping field form a final group labelled as having no value. The century of an artefact is derived from the earliest year of its creation date.

#### Scenario: Paginated listing
- **WHEN** a user requests page 2 with a page size of 25
- **THEN** the system returns at most 25 artefacts together with the total count of matching artefacts

#### Scenario: Free-text search
- **WHEN** a user searches for "lemons"
- **THEN** the results include every artefact whose title, maker or inventory number contains "lemons", ignoring case

#### Scenario: Search matches storage location
- **WHEN** a user searches for "cella 16"
- **THEN** the results include every artefact whose storage location contains "cella 16", ignoring case

#### Scenario: Sorting by creation date uses the historical range
- **WHEN** a user sorts artefacts by creation date ascending
- **THEN** artefacts are ordered by the earliest year of their creation date range
- **AND** artefacts without a creation date appear last

#### Scenario: Grouping by object type
- **WHEN** a user lists artefacts grouped by object type and sorted by title
- **THEN** all artefacts of one object type appear together, ordered by title within the group
- **AND** the response lists each object type with its number of matching artefacts

#### Scenario: Grouping by century
- **WHEN** a user groups artefacts by century and one artefact is dated "late 17th century" while another has no creation date
- **THEN** the first appears in the group for the 17th century
- **AND** the second appears in the final group for artefacts without a date

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
The system SHALL record an artefact's acquisition as a method (purchase, gift, bequest, transfer, or other), an acquisition date, a source, an optional lot (free text, such as an auction lot number), an optional price, and an optional note (free text). A price consists of an amount and an ISO 4217 currency code; both active codes and withdrawn codes (such as ITL, DEM or FRF) are accepted, because historical acquisitions were paid in them. The system MUST reject a price with a negative amount or an unrecognised currency code. The acquisition price and the valuation SHALL be classified as financially sensitive information. In this change, sensitive information is still visible to every user.

#### Scenario: Purchase with price
- **WHEN** a user records a purchase on 2019-05-02 from "Galerie Example" for 12500.00 EUR
- **THEN** the artefact returns that acquisition with the amount 12500.00 and currency "EUR"

#### Scenario: Auction purchase with lot and withdrawn currency
- **WHEN** a user records a purchase from "Sotheby's London", lot "170", for 350000000 ITL with the note "acquistato fuori asta"
- **THEN** the artefact returns that acquisition with lot "170", currency "ITL" and the note unchanged

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

### Requirement: Classification, origin and holding fields
The system SHALL allow an artefact to record, in addition to its identity and description: an object type from the object type reference list; a collection group (free text); a place of production as text exactly as written, with an optional reference to a region from the region reference list; a dynasty or period name (free text); an ordered list of exhibitions, each a free-text entry; a catalogue entry in English (long free text); a holder (free text); a storage location (free text); and a valuation, which is an optional monetary value with the same rules as an acquisition price. All of these fields are optional. Text fields SHALL be stored exactly as entered, apart from removing leading and trailing whitespace. The system MUST reject an object type or place-of-production region that does not exist.

#### Scenario: Artefact with classification and origin
- **WHEN** a user saves an artefact with object type "Ceramics", collection group "CINA", place of production "Cina" linked to the region "China", and dynasty "Sung"
- **THEN** later reads of the artefact return those values, with the object type name and the region name

#### Scenario: Uncertain place kept as written
- **WHEN** a user enters the place of production "Anatolia o Asia Centrale?" without linking a region
- **THEN** the artefact returns the place of production "Anatolia o Asia Centrale?" and no region

#### Scenario: Exhibitions keep their order
- **WHEN** a user saves the exhibitions "Arte Ottomana 1450-1600, Genova, 1999" followed by "Exhibition of Persian Art, New York, 1940"
- **THEN** the artefact returns both entries in that order

#### Scenario: Valuation with withdrawn currency
- **WHEN** a user records a valuation of 350000000 ITL
- **THEN** the artefact returns the valuation with amount 350000000 and currency "ITL"

#### Scenario: Unknown object type
- **WHEN** a user saves an artefact that references an object type identifier that does not exist
- **THEN** the system rejects the request with a validation error that names the object type field

### Requirement: Description per language
The system SHALL store an artefact's description separately for each content language. Create and update requests carry the descriptions of all languages together; reading an artefact returns the description resolved in the request's language with its language, and, for editing, the descriptions in every language. Each description keeps its line breaks and text exactly as entered, apart from removing leading and trailing whitespace.

#### Scenario: Description in two languages
- **WHEN** a user saves an artefact with the descriptions { "it": "Piatto con fenice", "en": "Dish with a phoenix" }
- **THEN** reading it with `lang=it` returns "Piatto con fenice" in language "it", and with `lang=en` returns "Dish with a phoenix" in language "en"

#### Scenario: Description only in one language
- **WHEN** an artefact has only an Italian description and it is read with `lang=en`
- **THEN** the Italian description is returned with language "it"

### Requirement: Artefact reads in the request's language
Reading a single artefact or the artefact list SHALL resolve every translated value it includes in the request's language: the object type name, the region names of provenance entries and of the place of production, the description, and, when the list is grouped by object type or origin, the group labels. Ordering by group follows the resolved labels.

#### Scenario: Provenance region named in the reader's language
- **WHEN** a provenance entry references the region with names "Turchia" (it) and "Turkey" (en) and the artefact is read with `lang=it`
- **THEN** the entry's region name is "Turchia"

#### Scenario: Group labels in the reader's language
- **WHEN** the list is grouped by object type and read with `lang=it`
- **THEN** the group for the object type "Carpets" / "Tappeti" is labelled "Tappeti"

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
The system SHALL return a single artefact by its identifier. It SHALL return a paginated list of artefacts that can be searched by free text (matching inventory number, title, maker, place of production, the names of the collection group, dynasty and storage location in any language, and the names of the holder and acquisition source) and sorted by inventory number, title or creation date. The list SHALL optionally be grouped by object type, collection group, place-of-production region, century, or dynasty. Grouping by a list groups by its entries, labelled in the request's language. When grouped, results are ordered by group first and then by the requested sort, each result carries its group key and label, and the response includes every group's label and total count for the current search. Artefacts without a value for the grouping field form a final group labelled as having no value. The century of an artefact is derived from the earliest year of its creation date.

#### Scenario: Paginated listing
- **WHEN** a user requests page 2 with a page size of 25
- **THEN** the system returns at most 25 artefacts together with the total count of matching artefacts

#### Scenario: Free-text search
- **WHEN** a user searches for "lemons"
- **THEN** the results include every artefact whose title, maker or inventory number contains "lemons", ignoring case

#### Scenario: Search matches storage location
- **WHEN** a user searches for "cella 16"
- **THEN** the results include every artefact whose storage location is named with "cella 16", ignoring case

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

#### Scenario: Grouping by dynasty uses the reader's language
- **WHEN** the list is grouped by dynasty and read with `lang=en`, and the dynasty "Safavide" has the English name "Safavid"
- **THEN** its artefacts appear in one group labelled "Safavid"

### Requirement: Historical dates with precision
The system SHALL record historical dates (an artefact's creation date, and provenance periods) as three parts: the display text exactly as entered, a computed earliest and latest year, and a precision (exact year, decade, century, or approximate). The system SHALL accept at least these forms:
- a year ("1654");
- a circa year, before or after the number ("c. 1650", "1470 circa");
- a decade ("1680s");
- a century ("17th century"), also written as a Roman numeral, with or without "secolo" or "sec." ("XV", "XV secolo", "IX sec.");
- a part of a century: "early", "mid" and "late", or in Italian "inizio", "metà" and "fine", each covering a third of the century; and "prima metà" and "seconda metà", covering a half ("late 17th century", "seconda metà XV");
- a range of centuries or century parts joined by "-", "/", a space, or two parts written in sequence ("XI-XII", "IX/X", "XV XVI", "fine XII inizio XIII");
- a year range, including a shortened second year ("1650-1660", "1450 - 75", "1463-4");
- a century followed by a year range in brackets, where the bracketed range is used ("XV (1440-1446)");
- a bounded year ("before 1720", "after 1800").

The system MUST reject text it cannot interpret, unless the user enters the earliest and latest years explicitly.

#### Scenario: Circa date
- **WHEN** a user enters a creation date of "c. 1650"
- **THEN** the system stores the display text "c. 1650", a year range that brackets 1650, and precision "approximate"

#### Scenario: Circa after the year
- **WHEN** a user enters "1470 circa"
- **THEN** the system stores the same range as "c. 1470" and precision "approximate"

#### Scenario: Part-century date
- **WHEN** a user enters "late 17th century"
- **THEN** the system stores a range of 1667 to 1700 and precision "century"
- **AND** the date is displayed as "late 17th century"

#### Scenario: Roman-numeral century with Italian qualifier
- **WHEN** a user enters "seconda metà XV"
- **THEN** the system stores a range of 1451 to 1500 and precision "century"
- **AND** the date is displayed as "seconda metà XV"

#### Scenario: Span across two centuries
- **WHEN** a user enters "fine XII inizio XIII"
- **THEN** the system stores a range of 1167 to 1233 and precision "century"

#### Scenario: Shortened year range
- **WHEN** a user enters "1450 - 75"
- **THEN** the system stores a range of 1450 to 1475 and precision "exact year"

#### Scenario: Bracketed range refines a century
- **WHEN** a user enters "XV (1440-1446)"
- **THEN** the system stores a range of 1440 to 1446

#### Scenario: Uninterpretable text with explicit range
- **WHEN** a user enters "during the reign of Louis XIV" together with an earliest year of 1643 and a latest year of 1715
- **THEN** the system stores the display text unchanged together with the given range

#### Scenario: Uninterpretable text without explicit range
- **WHEN** a user enters "unknown era" with no explicit range
- **THEN** the system rejects the date with a validation error

### Requirement: Acquisition with monetary value
The system SHALL record an artefact's acquisition as a method (purchase, gift, bequest, transfer, or other), an acquisition date, a source, which is a party, an optional lot (free text, such as an auction lot number), an optional price, and an optional note (free text). A price consists of an amount and an ISO 4217 currency code; both active codes and withdrawn codes (such as ITL, DEM or FRF) are accepted, because historical acquisitions were paid in them. The system MUST reject a price with a negative amount or an unrecognised currency code, and a source that is not a party. The acquisition price and the valuation SHALL be classified as financially sensitive information. In this change, sensitive information is still visible to every user.

#### Scenario: Purchase with price
- **WHEN** a user records a purchase on 2019-05-02 from the party "Galerie Example" for 12500.00 EUR
- **THEN** the artefact returns that acquisition with the source "Galerie Example", the amount 12500.00 and currency "EUR"

#### Scenario: Auction purchase with lot and withdrawn currency
- **WHEN** a user records a purchase from the party "Sotheby's", lot "170", for 350000000 ITL with the note "acquistato fuori asta"
- **THEN** the artefact returns that acquisition with lot "170", currency "ITL" and the note unchanged

#### Scenario: Invalid currency
- **WHEN** a user records a price with currency code "XYZ"
- **THEN** the system rejects the request with a validation error that names the currency

#### Scenario: Unknown source
- **WHEN** a user records an acquisition whose source is a party identifier that does not exist
- **THEN** the system rejects the request with a validation error that names the source

### Requirement: Provenance entries
The system SHALL allow an artefact to have an ordered list of provenance entries. Each entry references a region from the region reference list and has a historical period (using the historical date rules) and an optional note. The system MUST reject an entry that references a region that does not exist.

#### Scenario: Adding provenance
- **WHEN** a user adds the provenance entries "Flanders, c. 1650" followed by "Paris, 1780s"
- **THEN** the artefact returns both entries in that order, each with its region name and period display text

#### Scenario: Unknown region
- **WHEN** a user adds a provenance entry that references a region identifier that does not exist
- **THEN** the system rejects the request with a validation error

### Requirement: Classification, origin and holding fields
The system SHALL allow an artefact to record, in addition to its identity, description and catalogue entry: an object type from the object type reference list; a collection group from the collection group list; a place of production as text exactly as written, with an optional reference to a region from the region reference list; a dynasty from the dynasty list; an ordered list of exhibitions, each a free-text entry; a holder, which is a party; a storage location from the storage location list; and a valuation, which is an optional monetary value with the same rules as an acquisition price. All of these fields are optional. An artefact without a holder is held by the collection itself. Text fields SHALL be stored exactly as entered, apart from removing leading and trailing whitespace. The system MUST reject a reference to an object type, collection group, dynasty, storage location, party or place-of-production region that does not exist.

#### Scenario: Artefact with classification and origin
- **WHEN** a user saves an artefact with object type "Ceramics", collection group "CINA", place of production "Cina" linked to the region "China", and dynasty "Sung"
- **THEN** later reads of the artefact return those values, with the names of the object type, collection group, region and dynasty

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

#### Scenario: Holder and storage location
- **WHEN** a user saves an artefact held by the party "Rossetti Fine Arts srl" at the storage location "Beta2"
- **THEN** the artefact returns the holder "Rossetti Fine Arts srl" and the storage location "Beta2"

#### Scenario: Held by the collection
- **WHEN** a user saves an artefact without a holder
- **THEN** the artefact returns no holder, meaning it is held by the collection itself

#### Scenario: Unknown dynasty
- **WHEN** a user saves an artefact that references a dynasty identifier that does not exist
- **THEN** the system rejects the request with a validation error that names the dynasty field

### Requirement: Description per language
The system SHALL store an artefact's description separately for each content language. Create and update requests carry the descriptions of all languages together; reading an artefact returns the description resolved in the request's language with its language, and, for editing, the descriptions in every language. Each description keeps its line breaks and text exactly as entered, apart from removing leading and trailing whitespace.

#### Scenario: Description in two languages
- **WHEN** a user saves an artefact with the descriptions { "it": "Piatto con fenice", "en": "Dish with a phoenix" }
- **THEN** reading it with `lang=it` returns "Piatto con fenice" in language "it", and with `lang=en` returns "Dish with a phoenix" in language "en"

#### Scenario: Description only in one language
- **WHEN** an artefact has only an Italian description and it is read with `lang=en`
- **THEN** the Italian description is returned with language "it"

### Requirement: Artefact reads in the request's language
Reading a single artefact or the artefact list SHALL resolve every translated value it includes in the request's language: the object type name, the region names of provenance entries and of the place of production, the description, the catalogue entry, and, when the list is grouped by object type or origin, the group labels. Ordering by group follows the resolved labels.

#### Scenario: Provenance region named in the reader's language
- **WHEN** a provenance entry references the region with names "Turchia" (it) and "Turkey" (en) and the artefact is read with `lang=it`
- **THEN** the entry's region name is "Turchia"

#### Scenario: Catalogue entry in the reader's language
- **WHEN** an artefact has catalogue entries in Italian and English and is read with `lang=en`
- **THEN** the returned catalogue entry is the English one, with language "en"

#### Scenario: Group labels in the reader's language
- **WHEN** the list is grouped by object type and read with `lang=it`
- **THEN** the group for the object type "Carpets" / "Tappeti" is labelled "Tappeti"

### Requirement: Catalogue entry per language
The system SHALL store an artefact's catalogue entry (long free text) separately for each content language, in the same way as its description. Create and update requests carry the catalogue entries of all languages together; reading an artefact returns the entry resolved in the request's language with its language, and, for editing, the entries in every language. Each entry keeps its line breaks and text exactly as entered, apart from removing leading and trailing whitespace. A catalogue entry for a language that is not a content language MUST be rejected.

#### Scenario: Catalogue entry in two languages
- **WHEN** a user saves an artefact with the catalogue entries { "it": "Coppa in celadon", "en": "Celadon bowl" }
- **THEN** reading it with `lang=it` returns "Coppa in celadon" in language "it", and with `lang=en` returns "Celadon bowl" in language "en"

#### Scenario: Catalogue entry only in one language
- **WHEN** an artefact has only an English catalogue entry and it is read with `lang=it`
- **THEN** the English entry is returned with language "en"

#### Scenario: Unknown language
- **WHEN** a user saves a catalogue entry for a language that is not a content language
- **THEN** the system rejects the request with a validation error that names the catalogue entries field


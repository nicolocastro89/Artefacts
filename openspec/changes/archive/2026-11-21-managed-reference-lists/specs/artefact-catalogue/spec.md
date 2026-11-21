# Spec Delta

## MODIFIED Requirements

### Requirement: Classification, origin and holding fields
The system SHALL allow an artefact to record, in addition to its identity and description: an object type from the object type reference list; a collection group from the collection group list; a place of production as text exactly as written, with an optional reference to a region from the region reference list; a dynasty from the dynasty list; an ordered list of exhibitions, each a free-text entry; a catalogue entry in English (long free text); a holder, which is a party; a storage location from the storage location list; and a valuation, which is an optional monetary value with the same rules as an acquisition price. All of these fields are optional. An artefact without a holder is held by the collection itself. Text fields SHALL be stored exactly as entered, apart from removing leading and trailing whitespace. The system MUST reject a reference to an object type, collection group, dynasty, storage location, party or place-of-production region that does not exist.

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

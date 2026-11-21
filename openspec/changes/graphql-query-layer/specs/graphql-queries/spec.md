# Spec Delta

## Purpose

Lets a view ask for exactly the collection data it needs, as nested, typed results read from the read views, without the backend writing an endpoint for each view.

## ADDED Requirements

### Requirement: Read-only GraphQL endpoint
The system SHALL accept GraphQL query operations at `/graphql` and answer them from the read views. The schema SHALL contain no mutations: the catalogue is changed only through the existing REST operations. The schema SHALL be published so clients can discover it.

#### Scenario: Query a single artefact
- **WHEN** a client queries an artefact by identifier asking for its title and inventory number
- **THEN** the response contains exactly those two fields for that artefact

#### Scenario: No writes through GraphQL
- **WHEN** a client sends a mutation operation
- **THEN** the system rejects it with an error and changes nothing

#### Scenario: Unknown artefact
- **WHEN** a client queries an artefact whose identifier does not exist
- **THEN** the artefact field is null and no error is reported

### Requirement: Artefact fields and linked records
An artefact SHALL expose every field of the artefact read view. Each linked record (object type, collection group, place of production region, dynasty, storage location, holder and acquisition source) SHALL appear as an object with its identifier and its name resolved in the request's language. Parties also carry their kind. Historical dates SHALL carry their display text, earliest and latest year, and precision. Monetary values SHALL carry an amount and a currency. The primary image SHALL carry signed URLs for each rendition. Values the artefact does not have SHALL be null.

#### Scenario: Names in the reader's language
- **WHEN** a client queries an artefact's dynasty with `lang=en` and the dynasty has the English name "Safavid" and the Italian name "Safavide"
- **THEN** the dynasty's name is "Safavid"

#### Scenario: Image URLs are signed renditions
- **WHEN** a client queries an artefact's primary image
- **THEN** the result has thumb, web and print URLs that the image service accepts

#### Scenario: Held by the collection
- **WHEN** a client queries the holder of an artefact without a holder
- **THEN** the holder is null

### Requirement: Nested lists of an artefact
An artefact SHALL expose its provenance entries in order (region with its resolved name, period as a historical date, note), its images in the order attached (identifiers, file name, format, size, upload time, caption, whether primary, signed URLs), its exhibitions in order, and its descriptions per language together with the description resolved in the request's language. Asking for nested lists of many artefacts SHALL NOT read the database once per artefact.

#### Scenario: Provenance in order
- **WHEN** a client queries an artefact with the provenance entries "Flanders, c. 1650" followed by "Paris, 1780s"
- **THEN** the result lists both entries in that order with region names and periods

#### Scenario: Nested lists for a page of artefacts
- **WHEN** a client queries 25 artefacts with their images and provenance
- **THEN** the number of database queries does not grow with the number of artefacts

### Requirement: Listing artefacts
The system SHALL list artefacts with the free-text search, sorting, grouping and paging of the artefact list (same matching rules, same group-first order, every group's label and count for the whole search). A client SHALL also be able to filter on any field the field catalog marks as filterable and sort on any field marked sortable, combined with the search. The page size MUST NOT exceed 100.

#### Scenario: Same results as the list
- **WHEN** a client lists artefacts with the search "lemons", grouped by object type, sorted by title, page 2 of 25
- **THEN** the items, total and groups are the same as those of the artefact list with the same parameters

#### Scenario: Filter on a catalog field
- **WHEN** a client lists artefacts whose acquisition method is "gift" and whose image count is greater than 0
- **THEN** only gifted artefacts with images are returned

#### Scenario: Filtering on a field that is not filterable
- **WHEN** a client filters on the primary image
- **THEN** the system rejects the query with an error naming the field

#### Scenario: Page size limit
- **WHEN** a client asks for 500 artefacts per page
- **THEN** the system rejects the query with an error stating the maximum

### Requirement: Language of a query
A query SHALL resolve translated names and descriptions in the request's preferred languages, chosen as for every other read: the `lang` parameter, else the Accept-Language header, then the content languages' fallback order.

#### Scenario: Fallback language
- **WHEN** a client queries with `lang=en` an object type that has only an Italian name
- **THEN** the object type's name is the Italian name

### Requirement: Query limits
The system SHALL reject, before reading any data, a query whose nesting depth or estimated cost exceeds the configured limits, with an error that names the limit exceeded.

#### Scenario: Too deep
- **WHEN** a client sends a query nested deeper than the depth limit
- **THEN** the system rejects it with an error naming the depth limit, and reads no data

### Requirement: Sensitivity in the schema
Every GraphQL field that exposes a field the catalog tags as sensitive SHALL carry the same sensitivity tag in the published schema. The tag does not restrict access yet.

#### Scenario: Acquisition price is tagged
- **WHEN** a client reads the published schema
- **THEN** the acquisition price and valuation fields carry the sensitivity tag `financial`

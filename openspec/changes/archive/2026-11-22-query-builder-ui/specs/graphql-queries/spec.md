## MODIFIED Requirements

### Requirement: Listing artefacts
The system SHALL list artefacts with free-text search, sorting, grouping and paging, with the same matching rules and group-first order as the artefact list page (the `artefact-list` system query): every group's label and count for the whole search. A client SHALL also be able to filter on any field the field catalog marks as filterable and sort on any field marked sortable, combined with the search. The page size MUST NOT exceed 100.

#### Scenario: Same results as the list
- **WHEN** a client lists artefacts with the search "lemons", grouped by object type, sorted by title, page 2 of 25, once with its own document and once by running the system query `artefact-list` with the same variables
- **THEN** the items, total and groups are the same

#### Scenario: Filter on a catalog field
- **WHEN** a client lists artefacts whose acquisition method is "gift" and whose image count is greater than 0
- **THEN** only gifted artefacts with images are returned

#### Scenario: Filtering on a field that is not filterable
- **WHEN** a client filters on the primary image
- **THEN** the system rejects the query with an error naming the field

#### Scenario: Page size limit
- **WHEN** a client asks for 500 artefacts per page
- **THEN** the system rejects the query with an error stating the maximum

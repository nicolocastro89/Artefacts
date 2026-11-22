# stored-queries Specification

## Purpose
Defines stored GraphQL queries: saving, validating and running queries by identifier, the system queries shipped with the application, describing what a query returns and takes, and the rules a query builder uses.

## Requirements

### Requirement: Stored queries
The system SHALL keep stored queries, each with a stable identifier, a name, a GraphQL document holding exactly one query operation, and whether it is a system query. The document MUST be valid against the schema and within the query limits; a document that is not SHALL be rejected with a validation error that gives the problem and its position in the document.

#### Scenario: Save a valid query
- **WHEN** a user saves the query "Gifts with images" whose document lists gifted artefacts with their titles and thumbnails
- **THEN** the query is stored and can be read back with its document unchanged

#### Scenario: Invalid document
- **WHEN** a user saves a document that asks for a field the schema does not have
- **THEN** the system rejects it with a validation error naming the field and its line and column

#### Scenario: More than one operation
- **WHEN** a user saves a document holding two query operations
- **THEN** the system rejects it with a validation error

### Requirement: System queries
The system SHALL provide the queries that the built-in views use as system queries, shipped with the application and present after every start with the document of the running release. System queries MUST NOT be changed or deleted through the API. A user SHALL be able to duplicate any stored query, system queries included, into a new user query.

#### Scenario: System query cannot be edited
- **WHEN** a user tries to change the document of the system query `artefact-detail`
- **THEN** the system rejects the change with a conflict and the query is unchanged

#### Scenario: Duplicate a system query
- **WHEN** a user duplicates the system query `artefact-list` as "My list"
- **THEN** a user query named "My list" exists with the same document, and it can be edited

#### Scenario: Release updates a system query
- **WHEN** a new release changes the document of a system query and the application starts
- **THEN** the stored system query has the new document

### Requirement: Manage user queries
The system SHALL list stored queries (system and user) with their identifier, name, kind and last change, and SHALL let a user create, rename, change the document of, and delete user queries. Names MUST be unique ignoring case and surrounding whitespace.

#### Scenario: Rename to a taken name
- **WHEN** a user renames a query to the name of another stored query
- **THEN** the system rejects it with a validation error on the name

#### Scenario: Delete a user query
- **WHEN** a user deletes a user query
- **THEN** it is no longer listed and running it reports that it does not exist

### Requirement: Run a stored query
The system SHALL run a stored query by its identifier with the variables given, returning the same result as sending its document directly. Variables MUST be checked against the document's declarations; a missing required variable or a value of the wrong type SHALL be reported as an error naming the variable.

#### Scenario: Run by identifier
- **WHEN** a client runs `artefact-detail` with the variable `id` of an existing artefact
- **THEN** the result is that artefact's detail data

#### Scenario: Wrong variable type
- **WHEN** a client runs `artefact-list` with the variable `page` set to "two"
- **THEN** the system reports an error naming `page`

### Requirement: Result description
For every stored query the system SHALL describe its result as a map from each value's path (using `.` between fields and `[]` for list items) to its value type (text, number, date, historical-date, money, image, reference, boolean, map or list), together with the query's variables and their types. The description SHALL follow the same type rules as the descriptions of the REST responses.

#### Scenario: Describe the list query
- **WHEN** a client asks for the description of `artefact-list`
- **THEN** it includes `artefacts.items[].primaryImage.urls` of type image, `artefacts.items[].title` of type text and `artefacts.groups[]` of type list, and the variable `page` of type number

### Requirement: Builder rules
The system SHALL publish the rules a query builder needs, derived from the running GraphQL schema and the field catalog: the roots and their arguments, the fields of each type with their value types and whether they are lists or objects, the filterable and sortable fields with the operators of each field's type, the enums with their values, the sensitivity tags, and the query limits (maximum page size, depth and cost). The rules SHALL change with the schema and not be kept by hand.

#### Scenario: Filterable fields
- **WHEN** a client reads the builder rules
- **THEN** `acquisitionMethod` is listed as filterable with the operators of text, and `images` is not listed

#### Scenario: Limits published
- **WHEN** a client reads the builder rules
- **THEN** the maximum page size equals the limit the server enforces

### Requirement: Describe an unsaved document
The system SHALL describe a document without storing it: for a valid document, the result description and variables as for a stored query; for an invalid one, the validation problems with their line and column. Describing SHALL NOT change any stored query.

#### Scenario: Describe a valid document
- **WHEN** a client describes a document that selects `artefacts { items { title } }` with a `$search` variable
- **THEN** the description has the path `artefacts.items[].title` of type text and the variable `search` of type text

#### Scenario: Describe an invalid document
- **WHEN** a client describes a document that asks for a field the schema does not have
- **THEN** the response names the field with its line and column and nothing is stored

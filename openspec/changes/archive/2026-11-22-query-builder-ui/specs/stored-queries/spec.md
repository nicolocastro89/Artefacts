## ADDED Requirements

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

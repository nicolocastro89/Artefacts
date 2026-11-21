## MODIFIED Requirements

### Requirement: Built-in pages use the composition model
Built-in pages SHALL be composed from registered widgets through the same container and binding mechanism that user-defined pages use, with binding definitions as serialisable data. The built-in artefact list and detail pages SHALL be stored system pages rendered from their stored definitions, and the built-in read-only pages SHALL read their data through system stored queries, so that a user page built from stored queries can reproduce them.

#### Scenario: Built-in page as data
- **WHEN** a user reads the system page `artefact-detail` through the pages API
- **THEN** the result is a data structure of containers, data sources, widgets, bindings and configuration that renders the artefact detail page

#### Scenario: Built-in list uses a system query
- **WHEN** the artefact list page is shown
- **THEN** its data comes from running the system query `artefact-list`, and a page that runs the same query with the same bindings shows the same list

#### Scenario: Pages composed in code
- **WHEN** the print, edit or new page is shown
- **THEN** it is composed in code; the print page reads its data through a system query and the edit and new pages read through the API endpoints their forms write to

#### Scenario: Duplicate reproduces the built-in page
- **WHEN** a user duplicates the system page `artefact-list` and opens the copy at `/p/<slug>`
- **THEN** it shows the same list as `/artefacts`

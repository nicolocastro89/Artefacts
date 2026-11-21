## ADDED Requirements

### Requirement: Stored pages
The system SHALL keep stored pages, each with a stable identifier, a slug, a name, a definition holding the page's containers, data sources, layouts and widgets as data, whether it is a system page, and whether it is shown in navigation with a navigation label. Slugs MUST be unique ignoring case, MUST use only lower-case letters, digits and hyphens, and MUST NOT be empty.

#### Scenario: Save a page
- **WHEN** a user saves the page "Gifts" with the slug `gifts` and a valid definition
- **THEN** the page is stored and can be read back with its definition unchanged

#### Scenario: Slug already taken
- **WHEN** a user saves a page whose slug equals the slug of another stored page, ignoring case
- **THEN** the system rejects it with a validation error on the slug

#### Scenario: Invalid slug
- **WHEN** a user saves a page with the slug `My Page!`
- **THEN** the system rejects it with a validation error on the slug

### Requirement: Structural validation of definitions
The system SHALL accept a definition only when it is structurally valid: it has the shape of a page definition, every `query` data source names a stored query that exists, and a page shown in navigation has a navigation label. A rejection SHALL name the problem and its path in the definition. The system SHALL NOT check widget identifiers, binding slots or binding types when saving; these are reported where the page is rendered and in the page editor.

#### Scenario: Unknown stored query
- **WHEN** a user saves a definition whose container reads the stored query `no-such-query`
- **THEN** the system rejects it with a validation error that names the query and the container's path

#### Scenario: Malformed definition
- **WHEN** a user saves a definition in which a container has no data source
- **THEN** the system rejects it with a validation error naming the container's path

#### Scenario: Missing navigation label
- **WHEN** a user saves a page shown in navigation without a navigation label
- **THEN** the system rejects it with a validation error on the label

#### Scenario: Widget problems are not rejected
- **WHEN** a user saves a definition that binds a text field to an image slot
- **THEN** the page is stored, and the widget shows a configuration error when the page is rendered

### Requirement: System pages
The system SHALL provide the built-in artefact list and artefact detail pages as system pages, shipped with the application and present after every start with the definition of the running release. System pages MUST NOT be changed or deleted through the API. A user SHALL be able to duplicate any stored page, system pages included, into a new user page that can be edited.

#### Scenario: System page cannot be edited
- **WHEN** a user tries to change the definition of the system page `artefact-list`
- **THEN** the system rejects the change with a conflict and the page is unchanged

#### Scenario: Duplicate a system page
- **WHEN** a user duplicates the system page `artefact-list` as "My list" with the slug `my-list`
- **THEN** a user page named "My list" exists with the same definition, and it can be edited

#### Scenario: Release updates a system page
- **WHEN** a new release changes the definition of a system page and the application starts
- **THEN** the stored system page has the new definition

### Requirement: Manage user pages
The system SHALL list stored pages (system and user) with their identifier, slug, name, kind, navigation settings and last change, and SHALL let a user create, rename, change the definition and navigation settings of, and delete user pages. Names MUST be unique ignoring case and surrounding whitespace.

#### Scenario: Rename to a taken name
- **WHEN** a user renames a page to the name of another stored page
- **THEN** the system rejects it with a validation error on the name

#### Scenario: Delete a user page
- **WHEN** a user deletes a user page
- **THEN** it is no longer listed, its address `/p/<slug>` shows that the page does not exist, and it is removed from navigation

### Requirement: Render a stored page
The application SHALL render a stored page at `/p/<slug>` through the same renderer, containers, bindings and widgets as the built-in pages, and SHALL allow every registered widget on a stored page. Values taken from the URL search parameters SHALL be available to the page's query variables. An address whose slug matches no page SHALL show a not-found state.

#### Scenario: Open a custom page
- **WHEN** a user opens `/p/gifts` and the stored page `gifts` reads a stored query and binds its fields to widgets
- **THEN** the page shows the query's data in those widgets

#### Scenario: Record id from the URL
- **WHEN** a stored page binds the query variable `id` to the search parameter `id`, and the user opens `/p/record?id=<artefact id>`
- **THEN** the query runs for that artefact

#### Scenario: Unknown slug
- **WHEN** a user opens `/p/nothing-here`
- **THEN** the application shows a not-found state

#### Scenario: Deleted stored query
- **WHEN** a stored page reads a stored query that has since been deleted
- **THEN** the page shows the query's error state, and the page itself is unchanged

### Requirement: Built-in routes load system pages
The routes `/artefacts` and `/artefacts/:id` SHALL render the system pages `artefact-list` and `artefact-detail`, loaded from the pages API, with their addresses unchanged. If a page cannot be loaded, the application SHALL show an error state with a way to retry.

#### Scenario: Artefact list route
- **WHEN** a user opens `/artefacts`
- **THEN** the application loads the system page `artefact-list` and shows the same list as before

#### Scenario: Page fails to load
- **WHEN** the pages API cannot be reached while `/artefacts` is opened
- **THEN** the application shows an error state with a retry action

### Requirement: Pages in navigation
The application SHALL show every page that is shown in navigation as a link, with its navigation label, to `/p/<slug>` in a sidebar section for custom pages.

#### Scenario: Page appears in navigation
- **WHEN** a user saves the page `gifts` shown in navigation with the label "Gifts"
- **THEN** the sidebar has a link "Gifts" to `/p/gifts`

### Requirement: Page editor
The application SHALL provide a page editor for user pages with a JSON editor for the definition, a live preview that renders it, and fields for the page's name, slug and navigation settings. The editor SHALL show the server's validation errors with their paths when saving fails, and SHALL be read-only for system pages with an action to duplicate them.

#### Scenario: Save rejected
- **WHEN** a user saves a definition that names a stored query that does not exist
- **THEN** the editor shows the server's error with the path in the definition

#### Scenario: System page in the editor
- **WHEN** a user opens a system page in the editor
- **THEN** the definition is read-only and a duplicate action is offered

### Requirement: Preview reports page problems
The page editor's preview SHALL report each problem with its path in the definition: widgets that are not registered, binding slots that are missing or have a mismatched type, and binding paths that the stored query does not return. Problems found by the preview SHALL NOT prevent saving.

#### Scenario: Preview shows a binding problem
- **WHEN** a user edits a definition so that a binding path is not in the stored query's result description
- **THEN** the preview names that path as a problem, and the page can still be saved

#### Scenario: Unknown widget
- **WHEN** a user edits a definition to use a widget identifier that is not registered
- **THEN** the preview names the identifier and its path as a problem

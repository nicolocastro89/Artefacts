# query-builder Specification

## Purpose

Defines building stored queries from blocks: the query list and editor, the Blocks and Text views of one GraphQL document, the blocks for fields, filters, ordering, grouping, paging and variables, which documents blocks can show, and the Run panel for trying an unsaved query.

## Requirements

### Requirement: Query list and editor
The application SHALL list stored queries at `/queries` with their name, kind and last change, and provide an editor at `/queries/new` and `/queries/{id}`, with a "Queries" item in the navigation. System queries SHALL open read-only with an action to duplicate them; user queries SHALL be editable and deletable. Saving SHALL use the existing stored query rules and show the server's errors.

#### Scenario: System query opens read-only
- **WHEN** a user opens the system query `artefact-list`
- **THEN** its document cannot be changed and a duplicate action is offered

#### Scenario: Duplicate then edit
- **WHEN** a user duplicates `artefact-list` as "My list" and changes a block
- **THEN** the copy is saved with the change and the system query is unchanged

#### Scenario: Save rejected
- **WHEN** a user saves a name that another query already has
- **THEN** the editor shows the server's error on the name

### Requirement: The document is the only stored form
A query built with blocks SHALL be saved as its GraphQL document and nothing else. Blocks SHALL read the document by parsing it and write it by printing it, so a query written as text can be opened in blocks when it fits, and a query built with blocks can be read and edited as text.

#### Scenario: Open a system query in blocks
- **WHEN** a user duplicates the system query `artefact-detail` and opens its Blocks view
- **THEN** every selected field, including nested objects and lists, appears as a block

#### Scenario: Round trip
- **WHEN** a document that blocks can represent is shown in blocks and printed again without a change
- **THEN** it selects, filters, orders and groups exactly as before

### Requirement: Blocks and text are two views of one document
The editor SHALL offer a Blocks view and a Text view of the same document. Changing a block SHALL update the document; changing the text SHALL update the blocks when the document can be shown in them. A text edit SHALL NOT be rewritten by the editor. Printing from a block change SHALL warn first when the document has comments, which printing drops.

#### Scenario: Switch to text
- **WHEN** a user adds a filter block and opens the Text view
- **THEN** the text contains the filter in the `where` argument

#### Scenario: Text the blocks cannot show
- **WHEN** a user types a query that uses an alias and opens the Blocks view
- **THEN** the view is disabled, names the alias as the reason, and the text is unchanged

#### Scenario: Comments warned about
- **WHEN** a user changes a block in a document that has a comment
- **THEN** the editor warns that the comment will be removed and applies the change only after the user accepts

### Requirement: Selection blocks
The Blocks view SHALL let a user choose the root (`artefacts` list or single `artefact` by identifier) and select any field of the root's type, including fields of nested objects and lists, as a tree. It SHALL offer only fields the schema has and SHALL mark fields tagged sensitive.

#### Scenario: Select nested fields
- **WHEN** a user selects `acquisition`, then its `price` and the `amount` of the price
- **THEN** the document selects `acquisition { price { amount } }`

#### Scenario: Sensitive field marked
- **WHEN** a user browses the fields of an artefact
- **THEN** `valuation` and `acquisition.price` are marked as financial

### Requirement: Filter blocks
For the `artefacts` root the Blocks view SHALL let a user build the `where` argument from conditions grouped with and/or. A condition picks a field the builder rules mark as filterable, an operator valid for the field's type, and a value that is a literal or a variable. The view SHALL offer only operators of the field's type.

#### Scenario: Two conditions
- **WHEN** a user adds the conditions acquisition method equals "Gift" and image count is greater than 0 in an and group
- **THEN** the document filters with `where: { and: [{ acquisitionMethod: { eq: "Gift" } }, { imageCount: { gt: 0 } }] }`

#### Scenario: Operators follow the type
- **WHEN** a user picks the field `imageCount`
- **THEN** the operators offered are those of a number, not of text

### Requirement: Order, grouping and paging blocks
For the `artefacts` root the Blocks view SHALL let a user set the search text, the ordering by one or more sortable fields with a direction each, the grouping, and the page and page size. The page size SHALL NOT be settable above the maximum the builder rules state.

#### Scenario: Order by two fields
- **WHEN** a user orders by title ascending then by inventory number descending
- **THEN** the document orders by both, in that sequence

#### Scenario: Page size limit
- **WHEN** a user enters a page size above the maximum
- **THEN** the view states the maximum and does not accept the value

### Requirement: Variable blocks
A user SHALL be able to declare variables with a name and type and use them as the value of a condition or of an argument such as search, page or page size. A variable SHALL take the type of the place it is used. Removing a variable that is in use SHALL be refused with the places that use it named.

#### Scenario: Search from a variable
- **WHEN** a user sets the search to a new variable `search`
- **THEN** the document declares `$search: String` and passes it as `search: $search`

#### Scenario: Variable in use
- **WHEN** a user removes a variable that a condition uses
- **THEN** the view refuses and names the condition

### Requirement: Run panel
The editor SHALL provide a Run panel that shows an input for each declared variable, typed from the query's description, and runs the unsaved document against real data in the reader's language. It SHALL show the result and the result paths with their types, and SHALL show server errors with their line and column. Running SHALL NOT save the query.

#### Scenario: Run with a variable
- **WHEN** a user enters "lemons" for the variable `search` and runs the query
- **THEN** the panel shows the artefacts that match, and the query is not saved

#### Scenario: Error with position
- **WHEN** a user runs a document that asks for a field the schema does not have
- **THEN** the panel shows the error with its line and column

#### Scenario: Result paths
- **WHEN** a query returns `artefacts.items[].title`
- **THEN** the panel lists that path as text, as the page editor sees it

# Spec Delta

## MODIFIED Requirements

### Requirement: Containers route data to widgets through bindings
A container SHALL own a data source, either an API endpoint or a stored query, and a set of bindings. A stored query data source names the query and gives its variables, each a literal or a value taken from the route or the URL search parameters; values are converted to the variable's declared type. Each binding maps a path in the data source's response to a binding slot of a child widget. Binding paths of a stored query resolve against the query's result data. A binding MUST be rejected when the value type at the response path does not match the slot's declared type; for a stored query the types come from the query's result description.

#### Scenario: Valid binding
- **WHEN** a container's response has `primaryImage.urls.web` of type image and it is bound to the Image widget's `src` slot of type image
- **THEN** the Image widget renders that image

#### Scenario: Type mismatch rejected
- **WHEN** a binding maps a text field to the Image widget's `src` slot
- **THEN** the binding is reported as invalid and the widget shows a configuration error instead of rendering incorrect content

#### Scenario: Missing required binding
- **WHEN** a widget's required slot has no binding
- **THEN** the widget shows a configuration error that names the missing slot

#### Scenario: Query variables from the URL
- **WHEN** a container reads the stored query `artefact-list` with the variable `page` taken from the URL search parameter `page`, and the URL has `page=2`
- **THEN** the query runs with `page` as the number 2

#### Scenario: Binding to a path the query does not return
- **WHEN** a binding maps `artefact.valuation` to a slot and the stored query does not select the valuation
- **THEN** the widget shows a configuration error naming the path

### Requirement: Built-in pages use the composition model
Built-in pages SHALL be composed from registered widgets through the same container and binding mechanism that user-defined pages will use. Binding definitions SHALL be serialisable data, so that a page definition stored as data can reproduce a built-in page's layout. The built-in read-only pages (the artefact list, detail and print pages) SHALL read their data through system stored queries, so that a page built from stored queries can reproduce them. Pages that edit data read through the API endpoints their forms write to.

#### Scenario: Built-in page as data
- **WHEN** a developer serialises the artefact detail page's composition
- **THEN** the result is a data structure of containers, data sources, widgets, bindings and configuration that renders the same page when loaded back

#### Scenario: Built-in list uses a system query
- **WHEN** the artefact list page is shown
- **THEN** its data comes from running the system query `artefact-list`, and a page that runs the same query with the same bindings shows the same list

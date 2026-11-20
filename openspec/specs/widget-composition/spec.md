# widget-composition Specification

## Purpose

Defines how frontend widgets are built so that any widget can be placed inside any container and fed data by that container through a field mapping. Built-in pages and future user-defined pages both depend on this contract.

## Requirements

### Requirement: Widgets receive data only from their container
A widget SHALL NOT fetch its own data. Every widget SHALL render only the values bound to it by its parent container, plus its own configuration. The same widget SHALL render correctly whatever data source its container uses, as long as the bound values have the right types.

#### Scenario: Same widget, different sources
- **WHEN** the Image widget is bound once to the primary image of an artefact detail response and once to an image field in a list row
- **THEN** it renders the image correctly in both places with no change to the widget

### Requirement: Widget registry with declared contracts
Every widget SHALL be registered in a widget registry under a stable identifier. Each registration declares: its binding slots (each with a name, a value type, and whether it is required), its configuration options with their default values, and its print behaviour (whether it must be kept together on one printed page, and what it shows instead in print, if anything). The registry SHALL make it possible to list all widgets and their contracts while the application is running.

#### Scenario: Registry lists contracts
- **WHEN** the application lists the registered widgets
- **THEN** each entry exposes its identifier, binding slots with types, configuration options and print behaviour

### Requirement: Containers route data to widgets through bindings
A container SHALL own a data source, either an API endpoint or (in the future) a stored query, and a set of bindings. Each binding maps a path in the data source's response to a binding slot of a child widget. A binding MUST be rejected when the value type at the response path does not match the slot's declared type.

#### Scenario: Valid binding
- **WHEN** a container's response has `primaryImage.urls.web` of type image and it is bound to the Image widget's `src` slot of type image
- **THEN** the Image widget renders that image

#### Scenario: Type mismatch rejected
- **WHEN** a binding maps a text field to the Image widget's `src` slot
- **THEN** the binding is reported as invalid and the widget shows a configuration error instead of rendering incorrect content

#### Scenario: Missing required binding
- **WHEN** a widget's required slot has no binding
- **THEN** the widget shows a configuration error that names the missing slot

### Requirement: Item scope for list containers
A list container (Table or Repeater) SHALL be bound to a list in its parent's response. The bindings of its columns or child widgets SHALL resolve relative to the current list item, not the root response.

#### Scenario: Repeater item binding
- **WHEN** a Repeater is bound to `provenance[]` and a child FieldList binds `region.name` and `period.display`
- **THEN** each repeated item shows its own region name and period

### Requirement: Built-in pages use the composition model
Built-in pages SHALL be composed from registered widgets through the same container and binding mechanism that user-defined pages will use. Binding definitions SHALL be serialisable data, so that a page definition stored as data can reproduce a built-in page's layout.

#### Scenario: Built-in page as data
- **WHEN** a developer serialises the artefact detail page's composition
- **THEN** the result is a data structure of containers, data sources, widgets, bindings and configuration that renders the same page when loaded back

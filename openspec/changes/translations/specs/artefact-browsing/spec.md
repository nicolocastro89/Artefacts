# Spec Delta

## ADDED Requirements

### Requirement: Editing values per language
The frontend SHALL let a user edit a translatable value in every content language at once: the reference lists show one name column per language for regions and object types, and the artefact form shows one description field per language, labelled with the language. Saving sends all languages together. Inline creation of a region or object type from a picker records the typed name in the language the user is reading in.

#### Scenario: Region names per language
- **WHEN** a user enters "Turchia" in the Italian column and "Turkey" in the English column of a new region and saves
- **THEN** the region is created with both names

#### Scenario: Description per language in the form
- **WHEN** a user fills the English description and leaves the Italian description empty, then saves
- **THEN** the artefact has an English description and no Italian description

### Requirement: Showing a fallback language
The detail page and the print record SHALL show the description resolved in the reader's language, and SHALL mark it with its language when it is not the language the reader asked for.

#### Scenario: Fallback marked
- **WHEN** a reader whose browser prefers English opens an artefact that has only an Italian description
- **THEN** the description is shown with a marker saying it is in Italian

### Requirement: Content language management
The frontend SHALL provide a Languages list among the reference lists, where a user can add a language and change the fallback order, and SHALL show the system's error when a language in use cannot be removed.

#### Scenario: Reorder fallback
- **WHEN** a user moves Italian above English
- **THEN** values missing in the reader's language fall back to Italian first

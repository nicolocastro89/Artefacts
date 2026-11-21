# Spec Delta

## MODIFIED Requirements

### Requirement: Artefact detail page
The frontend SHALL provide an artefact detail page that shows the primary image, a gallery of the other images, the descriptive fields, the creation date as display text, the classification (object type, collection group), the origin (place of production with its linked region, dynasty), the acquisition including lot and note, the valuation, the holder and storage location, the exhibitions in order, the catalogue entry in the reader's language, and the provenance entries in order. Fields without a value are omitted rather than shown empty. Selecting an image SHALL show it at `web` size.

#### Scenario: Detail shows provenance
- **WHEN** a user opens an artefact with two provenance entries
- **THEN** the detail page lists both entries in order, with region name and period text

#### Scenario: Detail shows exhibitions and catalogue entry
- **WHEN** a user opens an artefact with two exhibitions and a catalogue entry in the reader's language
- **THEN** the detail page lists both exhibitions in order and shows the catalogue entry text with its line breaks preserved

#### Scenario: Empty fields omitted
- **WHEN** an artefact has no holder and no valuation
- **THEN** the detail page shows no holder or valuation label

### Requirement: Editing values per language
The frontend SHALL let a user edit a translatable value in every content language at once: the reference lists show one name column per language for regions and object types, and the artefact form shows one description field and one catalogue entry field per language, labelled with the language. Saving sends all languages together. Inline creation of a region or object type from a picker records the typed name in the language the user is reading in.

#### Scenario: Region names per language
- **WHEN** a user enters "Turchia" in the Italian column and "Turkey" in the English column of a new region and saves
- **THEN** the region is created with both names

#### Scenario: Description per language in the form
- **WHEN** a user fills the English description and leaves the Italian description empty, then saves
- **THEN** the artefact has an English description and no Italian description

#### Scenario: Catalogue entry per language in the form
- **WHEN** a user fills the Italian catalogue entry and leaves the English one empty, then saves
- **THEN** the artefact has an Italian catalogue entry and no English one

### Requirement: Showing a fallback language
The detail page and the print record SHALL show the description and the catalogue entry resolved in the reader's language, and SHALL mark each with its language when it is not the language the reader asked for.

#### Scenario: Fallback marked
- **WHEN** a reader whose browser prefers English opens an artefact that has only an Italian description
- **THEN** the description is shown with a marker saying it is in Italian

#### Scenario: Catalogue entry fallback marked
- **WHEN** a reader whose browser prefers Italian opens an artefact whose only catalogue entry is in English
- **THEN** the catalogue entry is shown with a marker saying it is in English

# artefact-browsing Specification

## Purpose

Gives collection staff a professional, unobtrusive interface for finding artefacts and viewing and editing their full catalogue record, including images and provenance.

## Requirements

### Requirement: Artefact list page
The frontend SHALL provide an artefact list page that shows artefacts either as a table (inventory number, thumbnail, title, maker, creation date) or as a grid of image cards. It SHALL offer free-text search, sorting, pagination and grouping. Grouping can be by object type, collection group, place of production, century or dynasty; when grouped, each group is introduced by a heading with its label and its total number of matching artefacts, in both the table and the grid. The chosen view mode, search text, sort order, grouping and page SHALL be reflected in the URL, so that the view can be bookmarked and shared.

#### Scenario: Switch to grid
- **WHEN** a user switches the list from table to grid
- **THEN** artefacts are shown as cards with their thumbnail and title, and the URL records the grid mode

#### Scenario: Shared URL restores view
- **WHEN** a user opens a URL that contains a search term, sort order, grouping and page
- **THEN** the list shows exactly that search, sort, grouping and page

#### Scenario: Artefact without image
- **WHEN** an artefact has no primary image
- **THEN** the list shows a neutral placeholder in place of the thumbnail

#### Scenario: Grouped by object type
- **WHEN** a user groups the list by object type
- **THEN** the list shows a heading for each object type with its count, followed by that type's artefacts

#### Scenario: Group continues on the next page
- **WHEN** a group's artefacts continue onto the next page
- **THEN** the next page repeats that group's heading above its first artefact

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

### Requirement: Create and edit artefacts
The frontend SHALL let a user create an artefact and edit an existing one. This includes historical date entry with immediate feedback on how the text was interpreted, picking or adding a region inline for provenance entries and for the place of production, picking or adding inline an object type, a collection group, a dynasty, a storage location, and a party as holder and as acquisition source, editing the list of exhibitions, recording a valuation, uploading images, choosing the primary image, editing captions and deleting images. Validation errors from the system SHALL be shown next to the field they concern.

#### Scenario: Date interpretation feedback
- **WHEN** a user types "late 17th century" into a historical date field
- **THEN** the form shows the interpreted range 1667 to 1700 before the user saves

#### Scenario: Inline region creation
- **WHEN** a user types a region name that does not exist in the provenance region picker and confirms it
- **THEN** the region is added to the reference list and selected for that entry

#### Scenario: Inline object type creation
- **WHEN** a user types an object type that does not exist and confirms it
- **THEN** the object type is added to the reference list and selected for the artefact

#### Scenario: Server validation shown on field
- **WHEN** saving fails because the inventory number is already used
- **THEN** the error is shown next to the inventory number field

#### Scenario: Source picked from parties
- **WHEN** a user types "Sothe" in the acquisition source picker and chooses "Sotheby's"
- **THEN** the artefact is saved with that party as its acquisition source

#### Scenario: Inline party creation
- **WHEN** a user types a seller that is not yet a party, chooses its kind and confirms it
- **THEN** the party is added and selected as the source

### Requirement: Region management
The frontend SHALL provide a page for listing, adding, renaming, re-parenting and deleting regions, and SHALL show the system's error when a region in use cannot be deleted.

#### Scenario: Delete in-use region
- **WHEN** a user tries to delete a region that is in use
- **THEN** the page explains that the region is in use and keeps it in the list

### Requirement: Object type management
The frontend SHALL provide a page for listing, adding, renaming and deleting object types, showing how many artefacts use each one, and SHALL show the system's error when an object type in use cannot be deleted.

#### Scenario: Delete in-use object type
- **WHEN** a user tries to delete an object type that artefacts use
- **THEN** the page explains that the object type is in use and keeps it in the list

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

### Requirement: Content language management
The frontend SHALL provide a Languages list among the reference lists, where a user can add a language and change the fallback order, and SHALL show the system's error when a language in use cannot be removed.

#### Scenario: Reorder fallback
- **WHEN** a user moves Italian above English
- **THEN** values missing in the reader's language fall back to Italian first

### Requirement: Reference list and party management
The frontend SHALL provide, among the reference lists, a page each for dynasties, collection groups and storage locations (names per language where the list is translated) and a page for parties (name and kind), each for listing with usage counts, searching, adding, renaming and deleting, and SHALL show the system's error when an entry or party in use cannot be deleted.

#### Scenario: Rename a dynasty in both languages
- **WHEN** a user renames the dynasty "Ilkahnide" to "Ilkhanide" (it) and "Ilkhanid" (en)
- **THEN** the page shows both names and every artefact of that dynasty shows the new name

#### Scenario: Delete a party in use
- **WHEN** a user tries to delete a party that artefacts were acquired from
- **THEN** the page explains that the party is in use and keeps it in the list

### Requirement: Entering measurements
The artefact form SHALL show, next to the dimensions text, a value and a unit picker for each of width, height, length and diameter. A measurement left empty is not saved. When a value is entered, the unit picker SHALL default to the unit most recently used in the form, or centimetre. Validation errors SHALL be shown next to the measurement they concern.

#### Scenario: Height entered in centimetres
- **WHEN** a user enters 7 as the height, keeps the unit "cm" and saves
- **THEN** the artefact has the height 7 cm

#### Scenario: Measurement removed
- **WHEN** a user clears the width value of an artefact and saves
- **THEN** the artefact no longer has a width

### Requirement: Showing dimensions text and measurements together
The detail page, the print page and the record and collection displays SHALL show the dimensions text as written, followed by the measurements that exist, each with its abbreviated label and unit (for example "diam 17,6 x h 7 · Ø 17.6 cm · H 7 cm"). When there is no dimensions text, the measurements are shown on their own.

#### Scenario: Both shown on the detail page
- **WHEN** a user opens an artefact with the dimensions text "diam 17,6 x h 7", a diameter of 17.6 cm and a height of 7 cm
- **THEN** the dimensions field shows the text followed by "Ø 17.6 cm · H 7 cm"

### Requirement: Unit of measure management
The frontend SHALL provide a page under Reference that lists the units of measure with their names, abbreviation, factor and usage count, and lets a user add a unit, edit its names per language, abbreviation and factor, and delete an unused unit.

#### Scenario: Unit added from the page
- **WHEN** a user adds "Foot", "ft", 0.3048 on the units page
- **THEN** the unit appears in the list and in the measurement unit pickers

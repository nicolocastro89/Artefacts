# artefact-browsing Specification

## Purpose

Gives collection staff a professional, unobtrusive interface for finding artefacts and viewing and editing their full catalogue record, including images and provenance.

## Requirements

### Requirement: Artefact list page
The frontend SHALL provide an artefact list page that shows artefacts either as a table (inventory number, thumbnail, title, maker, creation date) or as a grid of image cards. It SHALL offer free-text search, sorting and pagination. The chosen view mode, search text, sort order and page SHALL be reflected in the URL, so that the view can be bookmarked and shared.

#### Scenario: Switch to grid
- **WHEN** a user switches the list from table to grid
- **THEN** artefacts are shown as cards with their thumbnail and title, and the URL records the grid mode

#### Scenario: Shared URL restores view
- **WHEN** a user opens a URL that contains a search term, sort order and page
- **THEN** the list shows exactly that search, sort and page

#### Scenario: Artefact without image
- **WHEN** an artefact has no primary image
- **THEN** the list shows a neutral placeholder in place of the thumbnail

### Requirement: Artefact detail page
The frontend SHALL provide an artefact detail page that shows the primary image, a gallery of the other images, the descriptive fields, the creation date as display text, the acquisition, and the provenance entries in order. Selecting an image SHALL show it at `web` size.

#### Scenario: Detail shows provenance
- **WHEN** a user opens an artefact with two provenance entries
- **THEN** the detail page lists both entries in order, with region name and period text

### Requirement: Create and edit artefacts
The frontend SHALL let a user create an artefact and edit an existing one. This includes historical date entry with immediate feedback on how the text was interpreted, picking or adding a region inline for provenance entries, uploading images, choosing the primary image, editing captions and deleting images. Validation errors from the system SHALL be shown next to the field they concern.

#### Scenario: Date interpretation feedback
- **WHEN** a user types "late 17th century" into a historical date field
- **THEN** the form shows the interpreted range 1667 to 1700 before the user saves

#### Scenario: Inline region creation
- **WHEN** a user types a region name that does not exist in the provenance region picker and confirms it
- **THEN** the region is added to the reference list and selected for that entry

#### Scenario: Server validation shown on field
- **WHEN** saving fails because the inventory number is already used
- **THEN** the error is shown next to the inventory number field

### Requirement: Region management
The frontend SHALL provide a page for listing, adding, renaming, re-parenting and deleting regions, and SHALL show the system's error when a region in use cannot be deleted.

#### Scenario: Delete in-use region
- **WHEN** a user tries to delete a region that is in use
- **THEN** the page explains that the region is in use and keeps it in the list

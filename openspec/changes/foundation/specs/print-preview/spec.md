# Spec Delta

## Purpose

Lets users preview and print a displayed page as properly paginated output, where content is split across pages deliberately rather than cut at arbitrary points. This is also the basis for later PDF export.

## ADDED Requirements

### Requirement: Paginated print preview
The frontend SHALL provide a print preview for the artefact detail page. The preview lays the content out on discrete pages of the selected paper size (A4 by default, with Letter as an alternative) and orientation, and shows the total page count. The printed output SHALL match the preview's page breaks.

#### Scenario: Preview shows pages
- **WHEN** a user opens the print preview for an artefact
- **THEN** the content is shown as separate A4 pages with a visible page count

#### Scenario: Print matches preview
- **WHEN** a user prints from the preview
- **THEN** the printed pages break at the same points as the preview

### Requirement: Content units are not cut
Widgets whose print behaviour is "keep together" (such as an image with its caption, or a field group) SHALL never be split across a page boundary. If such a unit does not fit in the space left on the current page, it SHALL start on the next page. A unit taller than a whole page SHALL be scaled down to fit one page.

#### Scenario: Image moves to next page
- **WHEN** an image with its caption does not fit in the space left on a page
- **THEN** the image and caption both appear at the top of the next page

#### Scenario: Oversized unit
- **WHEN** a keep-together unit is taller than one page
- **THEN** it is scaled to fit a single page and is not split

### Requirement: Tables continue across pages
Table widgets SHALL be allowed to break between rows. A table that continues onto a new page SHALL repeat its header row there. No single row SHALL be split across pages.

#### Scenario: Long provenance table
- **WHEN** a provenance table has more rows than fit on the current page
- **THEN** it continues on the next page with the header row repeated and no row cut in half

### Requirement: Print-specific rendering
In print, widgets SHALL use their print rendering: images use the `print` size URL, and interactive controls (buttons, menus, editors) are hidden. Every page SHALL carry a running header with the artefact's inventory number and title, and a footer with "page N of M".

#### Scenario: Interactive controls hidden
- **WHEN** a user previews a page that has edit and upload buttons
- **THEN** those buttons do not appear in the preview or the printout

#### Scenario: Running header and footer
- **WHEN** a print preview spans three pages
- **THEN** each page shows the artefact's inventory number and title in the header and "page N of 3" in the footer

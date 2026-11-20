# Proposal

## Why

The collection is recorded today in seven Excel workbooks (ceramics, manuscripts, metalwork, miniatures, parchments, carpets, textiles: about 730 objects and 730 embedded photographs, up to 210 MB per file). Re-typing them is not realistic, and while the catalogue is empty staff keep working in the spreadsheets. An import that understands these workbooks as they are is what lets the catalogue replace them. Beads epic: art-li9. It depends on `extend-catalogue-fields`, which adds the fields these workbooks fill.

## What Changes

- An **Import** wizard in the app, with every step also available through the API:
  1. **Upload** an `.xlsx` workbook (up to 2 GB), choose the object type for its rows, and optionally a saved mapping template. The upload and parsing run in the background.
  2. **Map** columns to catalogue fields. Cells that hold several values on separate lines (title + dimensions; place + date + dynasty; seller + date + lot + price) are split line by line. Each line is recognised by its content (a dimension, a date, a lot, a price, a catalogue number, an item number) and assigned to a field, with "everything else goes to …" so no text is lost. Mappings are saved as named templates and reused.
  3. **Inventory numbers** come from a pattern the user sets (for example `CER-{group}-{item}`, or a catalogue number found in the row such as `W.389`). Database identifiers remain system-assigned UUIDs.
  4. **Places**: each distinct place-of-production text is matched against the region list. Similar regions are suggested (fuzzy match, tolerant of spelling variants such as "Cina" / "China", and of names linked in earlier imports); the user links one, creates a new region, or keeps the text unlinked. Decisions are remembered in the template.
  5. **Dates** that are not understood are listed once per distinct text, so the user can give a year range or leave those rows to be fixed in the sheet.
  6. **Review**: a preview of every row as it will be imported (with its pictures), counts of ready rows, rows already in the catalogue, and problems.
  7. **Import** runs in the background with progress. Each row is imported independently: every good row is saved, and problem rows are listed in a final report (row, field, original text, reason) that can be downloaded.
- **Rows and pictures**: group heading rows (`01. CINA`) set the collection group for the rows below them. Pictures embedded in the sheet are attached to the row they are anchored to, in reading order, with the first as the primary image. Unsupported picture formats (such as JPEG XR) are reported as warnings and the row still imports.
- **Re-running** the same workbook skips rows whose inventory number already exists in the catalogue, so fixing problem rows in the sheet and importing again adds only those rows.
- **Historical dates** accept the forms used in the workbooks: Roman-numeral centuries with Italian qualifiers ("XV secolo", "fine XII inizio XIII", "seconda metà XV", "IX/X"), "1470 circa", and shortened ranges ("1450 - 75"). Display text is kept exactly as written.

## Capabilities

### New Capabilities

- `artefact-import`: the staged import of `.xlsx` workbooks with embedded pictures, covering parsing, column and line mapping with saved templates, inventory number patterns, place matching, date resolution, preview, background commit with per-row results, and the wizard UI.

### Modified Capabilities

- `artefact-catalogue`: historical dates accept Roman-numeral centuries with Italian qualifiers, "circa" after the year, and shortened year ranges.

## Impact

- Backend:
  - a new import module: application handlers, a background worker, and EF Core tables for import jobs, staged rows, mapping templates and the place dictionary;
  - an `.xlsx` reader dependency, DocumentFormat.OpenXml (MIT), streaming the sheets and reading drawing anchors;
  - the date parser;
  - new endpoints;
  - storage of the uploaded workbook and extracted pictures in the existing S3 storage, with clean-up of abandoned imports.
- Frontend: an Import wizard (new routes and widgets), a reports view, and navigation.
- Operations: the API process runs a background worker, and long imports survive a closed browser but not an API restart in the middle of a row (interrupted jobs resume from the next unfinished row).
- Data: the real workbooks in `data/` (git-ignored) are the acceptance test set. Nothing from them is committed. Tests use small synthetic workbooks built in code.

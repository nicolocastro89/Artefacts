# Tasks

## 1. Prerequisites

- [ ] 1.1 Confirm `extend-catalogue-fields` is applied (object types, new artefact fields, withdrawn currencies) and verify `openspec list` shows it archived or complete before starting section 3
- [ ] 1.2 Add DocumentFormat.OpenXml to Infrastructure and a test helper that builds synthetic `.xlsx` workbooks in code (headings, group heading rows, multi-line cells, pictures anchored to rows, a JPEG XR picture); verify a test reads back what the helper wrote

## 2. Historical dates (Domain)

- [ ] 2.1 Extend `HistoricalDateParser` with Roman-numeral centuries ("XV", "XV secolo", "IX sec."), Italian parts ("inizio", "metà", "fine", "prima metà", "seconda metà"), century spans ("XI-XII", "IX/X", "XV XVI", "fine XII inizio XIII"), trailing "circa", shortened year ranges ("1450 - 75", "1463-4") and bracketed ranges ("XV (1440-1446)"); verify with unit tests for every scenario in the modified spec and that all existing date tests still pass
- [ ] 2.2 Run the parser over every distinct date line in the real workbooks (local script, output not committed) and verify the share of interpreted dates; list the remaining forms in the change notes rather than guessing rules for them

## 3. Reading workbooks (Infrastructure)

- [ ] 3.1 Implement the workbook reader (streaming sheet read, shared strings, headings row, record and group heading classification, drawing anchors to rows in reading order, picture extraction with format detection); verify with synthetic-workbook tests (group heading sets group, two pictures in order, picture on a non-record row becomes a warning)
- [ ] 3.2 Verify the reader against the seven real workbooks locally: record and heading counts match the survey (731 records, 35 headings, 732 pictures), with no exceptions; record the numbers in the change notes

## 4. Mapping engine (Application)

- [ ] 4.1 Implement the line detectors (itemTitle, dimensions, historicalDate, calendarDate with Italian and English months, lot with remainder, money with separator rules and currency words, catalogueNumber, locationCode) as pure functions; verify with unit tests built from real line shapes ("lot 170 £ 7.865", "€ 5.196,00", "lire 350.000.000", "8 aprile 1975", "27-11-2018")
- [ ] 4.2 Implement column rules (single target, line rules by detector or position, rest target, joining of continuation lines) and the default mapping suggestion from headings and detector hit rates; verify that no source line is lost (property test: every non-empty line appears in exactly one target)
- [ ] 4.3 Implement the inventory number pattern (tokens, padding, prefer field) and repeat detection in sheet order; verify with unit tests for the three spec scenarios
- [ ] 4.4 Implement place normalisation and similarity ranking (trigram Jaccard, Jaro-Winkler, alias hits first, threshold 0.6, top three); verify "Cina" suggests "China" and an alias recorded in a template ranks first
- [ ] 4.5 Implement the row evaluation that turns raw row + mapping + decisions into a would-be artefact request plus problems and warnings (repeat, missing title, unresolved date, invalid money, already in catalogue); verify with unit tests

## 5. Jobs, worker and API

- [ ] 5.1 Add `import_jobs`, `import_rows` and `import_templates` with the `Imports` migration; verify that a fresh database and the development database migrate
- [ ] 5.2 Streamed upload endpoint (`POST /api/imports`, `.xlsx` only, object type required, optional template) storing the source under `imports/{jobId}/`; verify integration tests for acceptance and for rejecting `.csv`/`.xls`
- [ ] 5.3 `ImportWorker` read phase (claim with SKIP LOCKED, stage rows and pictures, set awaiting mapping, failed with reason on unreadable files); verify an integration test with a synthetic workbook reaches awaiting mapping with the expected staged rows
- [ ] 5.4 Mapping, places, dates and review endpoints, plus import template CRUD; verify integration tests: mapping saved and reused through a template, place suggestions returned, date resolution applied, review counts match
- [ ] 5.5 Commit phase (create places, then per row: copy pictures, one transaction for the artefact, media and row status; roll back and clean up on failure; skip existing inventory numbers; unsupported pictures as warnings); verify integration tests: good rows import despite problem rows, re-import adds only fixed rows, and a JPEG XR picture becomes a warning
- [ ] 5.6 Restart safety: a job left in `importing` resumes from the next unfinished row without duplicates; verify with an integration test that stops the worker mid-job and starts a new host
- [ ] 5.7 Report endpoints (JSON and `.xlsx` with imported, skipped and problem rows including sheet row, field, original text and reason), discard, and staging clean-up (on finish, on discard, and a 14-day sweep); verify with integration tests that storage under `imports/{jobId}/` is empty afterwards
- [ ] 5.8 Staged picture thumbnails through imgproxy for the `imports/` prefix; verify a review row's thumbnail URL returns an image
- [ ] 5.9 Regenerate the OpenAPI document and the frontend client; verify `npm run build` typechecks

## 6. Wizard (Frontend)

- [ ] 6.1 Import section in the rail and the jobs list (state, counts, progress, links); verify with a component test
- [ ] 6.2 Upload step (file, object type picker, template picker, upload progress); verify with a component test, including the rejection message for a `.csv`
- [ ] 6.3 Mapping step (column samples, target or line rules editor, rest target, inventory pattern with live examples, save as template, live preview of five rows); verify with component tests for line rule editing and the "no rest target" warning
- [ ] 6.4 Places and dates steps (suggestions with scores, region search, create region with parent, keep unlinked; year ranges for unresolved dates); verify with component tests
- [ ] 6.5 Review step (counts, rows with parsed values, thumbnails and problems, filter by status) and commit; progress view polling while importing; report view with links and `.xlsx` download; discard; verify with component tests
- [ ] 6.6 Follow DESIGN.md (plates, condensed labels, one action colour, yellow only for selection, phone layout); verify with headless captures of each step at 1440 and 390 with no overflow

## 7. Acceptance with the real workbooks

- [ ] 7.1 Import all seven workbooks from `data/` into a fresh development database with one shared template; verify that every record row is either imported or listed as a problem with a reason, that the picture counts match, and that a second run of each workbook reports everything as already present
- [ ] 7.2 Spot-check ten imported artefacts across workbooks against the sheet (title, dimensions, place and region, date range, dynasty, acquisition source, date, lot and price, exhibitions, English entry, holder and location, pictures and primary); record any mapping fixes in the template and the findings in the change notes
- [ ] 7.3 Run `dotnet test`, `npm test`, `npm run build`, `npm run lint` and `openspec validate import-artefacts --strict`; verify all pass
- [ ] 7.4 Create beads for follow-ups found during acceptance (including CSV import and updating existing records from a workbook) and verify `bd list` shows them

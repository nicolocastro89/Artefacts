# Design

## Context

See proposal.md for motivation. This change builds on `extend-catalogue-fields` (object types, collection group, place of production, dynasty, exhibitions, English catalogue entry, holder, location, valuation, acquisition lot/note, withdrawn currencies), which must be applied first.

What the real workbooks look like, from inspecting all seven in `data/` (git-ignored):
- **Sheets:** one sheet of records per workbook. Row 1 holds the headings ("Gruppo e numero", "Nome File", "Immagine", "Zona di Produzione / Data / Dinastia", "Provenienza", "Formato Immagine", "Mostre", sometimes "Valore", and "William" for the English catalogue entry). Some workbooks have a second empty or "Template" sheet.
- **Group headings:** 35 heading rows such as "01. CINA" or "05. LAMPASSI" (only column A filled), 731 record rows.
- **Column A:** the group number, then optional holder or location lines ("Rossetti / Fine Arts srl", "MI F3", "GE", "cella 16", "Beta2") or a catalogue number ("W.389").
- **Title column:** "NN. Title", then dimension lines, sometimes a remark ("DA MONTARE").
- **Origin column:** place lines (sometimes two: "Cina nord-orientale / (Qinghai, Xinjiang)"), a date line in Italian or English forms (Roman centuries, "1470 circa", "1450 - 75", even a Hijri date), and a dynasty line.
- **"Provenienza" column:** this is the acquisition. Seller or auction house, a date ("12 06 1990", "8 aprile 1975", "27-11-2018"), "lot 170", a price in £, €, $ or lire with mixed separators ("£ 7.865", "€ 5.196,00", "£ 85,400", "lire 350.000.000"), and free remarks ("acquistato fuori asta", "Carbon dating AD 527-62").
- **Pictures:** 732, floating and anchored in the "Immagine" column (a few in neighbouring columns), at most two per row, every one on a record row. Formats: JPEG, PNG, TIFF (up to 32 MB each) and one JPEG XR (`.wdp`).
- **Repeats:** 15 group+item keys repeat within a workbook.

The backend is a DDD layered .NET 10 API with EF Core on PostgreSQL and S3-compatible storage (SeaweedFS) behind an `IMediaStorage` port. Uploads up to 2 GB are already allowed. The frontend is React with generated OpenAPI types.

## Goals / Non-Goals

**Goals:**
- Import the seven workbooks as they are, with their pictures, losing no text.
- Make every decision reviewable before anything is written, and every outcome explainable after.
- Survive a closed browser and an API restart.

**Non-Goals:**
- CSV and legacy `.xls` files (the source data is `.xlsx`; images need `.xlsx`). The epic's CSV wish moves to a follow-up bead.
- Updating existing artefacts from a workbook: re-import only adds missing rows.
- Provenance chains (the workbooks record acquisition, not provenance history).
- Editing individual row values inside the wizard: fixes happen in the workbook, then re-import.
- Running several imports concurrently.

## Decisions

### Read once, stage everything, map in memory
On upload, the request body is streamed straight to storage (`imports/{jobId}/source.xlsx`) with a multipart reader. Nothing is buffered in memory. The worker then reads the workbook once with **DocumentFormat.OpenXml** (MIT, Microsoft):
- the sheet is read with the forward-only `OpenXmlReader` and shared strings are loaded once;
- each non-empty row's raw cell texts are stored in `import_rows.raw` (JSON);
- the drawing part's `twoCellAnchor`/`oneCellAnchor` pictures are copied to `imports/{jobId}/pictures/{n}.{ext}`, with the anchor row and column recorded.

Mapping, patterns, places and dates are then pure functions applied to staged rows in memory. The workbooks are small in rows (at most about 230), so the review recomputes instantly, and the 200 MB file is never read twice.
*Alternatives:*
- ClosedXML: convenient, but loads the whole workbook into memory and is weaker on drawing anchors.
- Re-reading the workbook on every mapping change: slow on 200 MB files.

### Row classification
- **Headings:** the first non-empty row is the headings row. A row is a group heading when only column A has a value matching `^\s*(\d+)\.\s*(\S.*)$`.
- **Pictures:** a picture belongs to the record row of its `from` anchor. Otherwise it goes to the nearest record row above, with a warning. Order follows the anchor row, then the anchor column, then the offset.

### Mapping model: column rules with content-recognised lines
A mapping (stored as JSON in the job and in templates) is a list of column rules. Each rule names a source column, by heading text with its index as a fallback, and either:
- a single target field; or
- **line rules**: an ordered list of `{ detector | position } → target` entries, plus `rest → target`.

Detectors are small pure recognisers in the Application layer:

| Detector | Recognises |
|---|---|
| `itemTitle` | `NN. text`, producing item number and title |
| `dimensions` | cm, diam, h, or `n x n` |
| `historicalDate` | anything the date parser accepts |
| `calendarDate` | `dd mm yyyy`, `dd-mm-yyyy`, or `d <Italian or English month> yyyy`, producing a `DateOnly` |
| `lot` | `lot`/`lotto` plus a number; the rest of the line is passed on, so "lot 170 £ 7.865" also yields a price |
| `money` | a currency symbol or word (£ € $, lire, EUR/GBP/USD/ITL) plus an amount |
| `catalogueNumber` | `W.389`-style numbers |
| `locationCode` | `MI F3`, `GE`, `FI`, `cella 16`, `Beta2`; the list of patterns is part of the template |

A line matches the first rule that accepts it. Consecutive unmatched lines that belong together (a holder split over two lines, "Rossetti" / "Fine Arts srl") are joined with a space when they go to the same single-value target, and with a line break when the target is long text. The suggested default mapping is derived from heading keywords ("Nome" → title, "Zona" → origin rules, "Provenienza" → acquisition rules, "Mostre" → exhibitions with one entry per paragraph, "William" → English catalogue entry, "Valore" → valuation, "Formato" → ignore, "Immagine" → pictures) plus the detectors' hit rates on the first 20 rows.
*Alternative:* purely positional line mapping. Rejected because the second line is a date in one row and a place continuation in the next.

### Money amounts
- The currency comes from the symbol or word: £ → GBP, € → EUR, $ → USD, "lire"/"L." → ITL.
- **Separators:** when both "." and "," appear, the last one is the decimal separator. When only one appears and it is followed by exactly three digits (once or repeated), it is a thousands separator ("7.865" → 7865, "85,400" → 85400). Otherwise it is the decimal separator.
- The parsed amount is shown next to the original text in the review, and the original line is also kept in the acquisition note when the parse needed a guess, for example a single separator followed by exactly three digits.

### Inventory number pattern
- **Syntax:** `{group:2}` (zero-padded), `{item:2}`, `{row}`, `{field:catalogueNumber}`, and literal text, plus an optional `prefer` field used when the row has a value for it. For example, `prefer catalogueNumber, else CER-{group:2}-{item:2}`.
- **Repeats:** detected in the review, in sheet order; the first row wins. Rows whose number already exists in the catalogue are "already present" (skipped), which is what makes re-import incremental.

### Places: similarity plus remembered aliases
Place texts are normalised (NFKD with accents stripped, lower case, punctuation and "?" removed, whitespace collapsed). Candidates are every region's names in all content languages (the `translations` change), plus every alias recorded in any template's place decisions (alias → region). An exact match on a normalised name in any language ranks first: the Italian workbooks mostly hit Italian region names exactly ("Cina", "Turchia"), so similarity scoring is only the fallback. Each candidate is scored with the maximum of the trigram Jaccard and the Jaro-Winkler similarity, and the top three at ≥ 0.6 are shown. For "Cina" vs "China", Jaro-Winkler is about 0.9. Exact alias hits rank first. This runs in memory, which is enough for hundreds of regions. Decisions are stored per normalised text: `link(regionId)`, `create(names, parentId)` (names per language, the workbook's text pre-filled under Italian) (executed when the import is committed, before rows, so every row links the same new region), or `keep`.
*Alternative:* PostgreSQL `pg_trgm`. Unnecessary at this scale, and it would add an extension to the database setup.

### Dates
The date parser in the Domain layer gains the Italian and Roman forms in the modified spec. The parser stays one function, so the form, the API and the import interpret identically. Distinct unparsed texts are listed in the review. A resolution (earliest, latest) is stored per text on the job and copied into the template for reuse.

### Jobs, rows and the worker
- **Tables:**
  - `import_jobs`: id, state, object type, template id, sheet, mapping JSON, decisions JSON, counters, error, timestamps;
  - `import_rows`: job id, sheet row, group number and label, raw JSON, picture refs JSON, status (`pending | ready | problem | imported | skipped`), problems JSON, artefact id;
  - `import_templates`: name, mapping JSON, decisions JSON.
- **Worker:** an `ImportWorker` `BackgroundService` claims work with `SELECT … FOR UPDATE SKIP LOCKED`, one job at a time, in two phases:
  - **Read:** stage rows and pictures, then set the job to `awaiting mapping`.
  - **Commit:**
    1. Create the places marked for creation.
    2. For each `ready` row in sheet order: copy its pictures from staging to `originals/` (server-side S3 copy).
    3. In one database transaction, create the artefact through the existing command handler path, so all domain validation applies; attach the media assets; and set `import_rows.status = imported` with the artefact id.
    4. On failure, roll back, delete that row's copied originals, and record the problem.
    5. A row already marked `imported` is never repeated, so a restart resumes safely. On start-up, a job left in `importing` is re-claimed.
- **Clean-up:** on finish or discard, staging (`imports/{jobId}/`) is deleted. A daily sweep discards jobs left uncommitted for 14 days.
- **Formats:** pictures in formats the catalogue rejects (JPEG XR, EMF) become warnings, and the row imports without them.

### API
The import API lives under `/api/imports`:
- `POST` creates a job; the multipart body holds the file, object type and optional template;
- `GET /api/imports` lists jobs; `GET /{id}` returns state, progress and counts;
- `GET /{id}/columns` returns headings with sample values and detector hit rates;
- `PUT /{id}/mapping`;
- `GET /{id}/places` with suggestions, and `PUT /{id}/places`;
- `GET /{id}/dates` and `PUT /{id}/dates`;
- `GET /{id}/review` (paged rows with parsed values, picture thumbnail URLs and problems; counts);
- `POST /{id}/commit` and `POST /{id}/discard`;
- `GET /{id}/report` (JSON) and `GET /{id}/report.xlsx`.

Templates have `GET/POST/PUT/DELETE /api/import-templates`. Staged picture thumbnails go through imgproxy using the `imports/` key prefix, with the same signing.

### Frontend
The Import section (rail item) lists jobs. The wizard route `/imports/:id` derives its step from the job state, and the user can move back to earlier steps until commit. The wizard is a bespoke page like Reference lists, not a page-definition composition: it is a workflow over job state, not a record view. It reuses the design primitives (plates, tables, condensed labels, yellow only for selection).
- **Mapping step:** each column's heading and three sample cells, its target or line rules, and a live preview of five parsed rows.
- **Review:** the records-table look, with status chips and problems inline.
- **Progress:** polled every 2 seconds while importing.

## Risks / Trade-offs

- [Detectors misclassify a line, for example a dimension-like number in a remark] → every parsed value is shown beside its source text in the review, rules are editable per column, and nothing is written before confirmation.
- [Money separator guesses are wrong for an odd format] → the original line is kept in the acquisition note whenever the parse needed a guess.
- [A 200 MB upload over a slow connection times out] → the body streams to storage; the client shows upload progress; Kestrel's request timeout is raised for this endpoint only.
- [Staging doubles storage temporarily] → clean-up on finish and discard, plus the 14-day sweep.
- [Generated inventory numbers are not the curator's real numbers] → the pattern is the user's choice, and inventory numbers stay editable afterwards. The source group and item are kept in the collection group and the inventory number.
- [Real data must never reach git] → `data/` is git-ignored, and tests build synthetic workbooks in code with the OpenXml SDK.

## Migration Plan

1. Migration `Imports`: the three tables, plus an index on `(job_id, status)`.
2. Deploy normally; the worker starts with the API. No existing data changes.
3. Rollback: drop the tables. Artefacts already imported remain as ordinary records.

## Open Questions

- Which Kestrel request timeout suits the largest uploads on the deployment network. It is configuration only, with a default of 30 minutes for the import endpoint.

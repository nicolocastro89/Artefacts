# Implementation notes

Findings from running the implementation against the real workbooks in `data/` (git-ignored). Only counts and the shapes of the values are recorded here, never the collection's records.

## Dates (task 2.2)

The extended parser interprets 674 of the roughly 713 date lines in the origin columns (about 95%). The remaining forms are left to the import's date step, where the user gives a year range per distinct text, rather than guessed at with new rules:

| Form | Lines | Shape |
|---|---|---|
| Trailing question mark | 5 | `<century span> secolo?`, `prima metà <century>?`, `<century> secolo ?` |
| "datato" (dated) | 5 | `datato <year>`, `datato <year>-<year>`, `datato <d> <mm> <yyyy> AD`, `datato <d> <Italian month> <yyyy>` |
| Hijri dates | 5 | `<d> <Hijri month> <AH year>`, `AH <year>/<month> <year>`, `(forse <AH> = <AD>)` |
| A bracketed date on its own line | 3 | `(<d> <Italian month> <yyyy>)`, `(<Italian month> <yyyy>)`, `(<year> - <yy>)`: the continuation of the line above |
| A part of a century on its own line | 7 | `seconda metà`, `inizio`, `fine`: the century is on another line or missing |
| Qualifier after the century | 2 | `<century> prima metà` |
| Questioned bracket | 3 | `metà <century> (<year>?)`, `inizio <century> (prima del <year>)` |
| Before the common era | 2 | `<century> secolo a.C.`, `<century>- <century> secolo a.C.` (years before 1 are not supported) |
| Thirds and quarters | 2 | `ultimo terzo <century> sec`, `metà o terzo quarto <century> sec` |
| Other | 5 | an English sentence with "10-13th century", `<century> secolo circa`, a typo (`iniziaXVII`), a material word before the date |

## Reading the workbooks (task 3.2)

The reader reads all seven workbooks without errors, choosing the records sheet in each (empty sheets and the headings-only "Template" sheets are skipped). Reading a workbook, including streaming every picture, takes 0.1 to 2.7 seconds for files of 2 to 200 MB.

| | Survey | Reader |
|---|---|---|
| Record rows | 731 | 726 |
| Group headings | 35 | 40 |
| Pictures | 732 | 736 |

The survey undercounted the group headings: five heading cells it missed hold a line break after the number or between words, or have a one-digit number ("2. INDIA"). They are all real headings, so the five rows the survey counted as records are headings. The drawing parts hold 736 pictures, all anchored on record rows (none needed the nearest-row fallback), at most two per row. By content they are 674 JPEG, 33 PNG and 29 TIFF. The one `.wdp` (JPEG XR) part is not a picture of its own: it is an Excel brightness layer over an ordinary embedded image, which is what the reader takes.

## Tuning against the workbooks (tasks 4 and 6)

Running the reader, the suggested mapping and the row evaluation over all seven workbooks locally showed where the detectors met shapes the design did not list. With the changes below, 692 of the 726 rows are ready on the suggested mapping alone, before any place or date decision:

- **Item titles** also take a comma after the number ("19, frammento"), a dash with an old code ("01-B3A mamelucco"), a letter after a space ("03 b. coppa"), a number, a space and a word ("08 Firmano"), and titles that start with a number ("05. 2 piastrelle"). Lines that are measurements ("27.5 x 30") are still excluded. A column takes one item number per row, so a later numbered line is title text. The first line is the title when it has no number.
- **Group numbers:** the row's own number in the first column wins over the heading above it. Two workbooks have headings that disagree with their rows, which produced repeated inventory numbers.
- **Dates:** a line that only looks like a date ("Qinghai Dulan VIII") gives way to a real date elsewhere in the cell. A lone part of a century joins the next line ("seconda metà" / "XVI"), and a bracketed line after a date is part of it.
- **Place similarity:** Jaro-Winkler counts only at 0.8 or above. Below that it rated short unrelated names as similar ("Iran" and "France" at 0.75); spelling variants such as "Cina" and "China" are above it.
- **Templates** work across workbooks. The `{sheet}` pattern token gives each workbook its own three-letter prefix. A rule whose heading is missing falls back to its column number only when no other rule claims that column by heading.

The remaining rows are 26 dates for the dates step (see above) and about a dozen sheet irregularities that the review lists: a title and measurements on one line, a number with no title, or a missing item number.

The headless captures of every step at 1440 and 390 pixels, in light and dark, showed no overflow once the rail on narrow phones shows the sections other than the current one as icons.

## Acceptance with the real workbooks (tasks 7.1 and 7.2)

The seven workbooks were imported into a freshly reset and seeded development database with one shared template, saved from the first workbook's suggested mapping, and with no place or date decisions:

| Workbook | Rows | Imported | Problems | Pictures attached |
|---|---|---|---|---|
| Manuscripts | 47 | 41 | 6 | 42 |
| Ceramics | 70 | 67 | 3 | 69 |
| Metalwork | 38 | 34 | 4 | 34 |
| Miniatures | 157 | 152 | 5 | 152 |
| Parchments | 7 | 7 | 0 | 7 |
| Carpets | 184 | 178 | 6 | 183 |
| Textiles | 223 | 213 | 10 | 215 |
| **Total** | **726** | **692** | **34** | **702** |

- **Every row is accounted for:** each one is imported or listed as a problem with a reason (26 dates not understood, 7 inventory numbers the pattern could not build, 3 rows without a title, 1 repeat).
- **Pictures match:** the pictures attached equal the supported pictures staged for the imported rows. The other pictures belong to problem rows.
- **A second run of each workbook** with the template imported nothing, reported every imported row as already present, and listed the same problems.
- **Speed:** each workbook took 2 to 43 seconds from upload to finished.

Spot-checking ten artefacts across the workbooks against their sheet rows found title, measurements, group, place, date range, dynasty, acquisition source, date, lot and price (with the price as written in the note when the separator was a guess), exhibitions, English entry, holder and storage location, and pictures with the first as primary, all as written. Findings:

- One manuscript's origin cell holds a pasted English caption after a blank line, which ends up in the dynasty after the real dynasty. The review shows it, and it can be corrected in the sheet; a short field's remaining lines could stop at the first paragraph.
- Exhibitions written one after another without a blank line become one entry (one row in the sample). Paragraphs are the only reliable separator.
- Places link only by an exact name in some language. The seeded regions have no Italian names for most of the workbooks' places, so linking them is a decision for the places step.

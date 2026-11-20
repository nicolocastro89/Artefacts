---
name: Artefacts
description: Collection catalogue for a private art collection, kept like a storage rack.
colors:
  rail: "#2e3a41"
  rail-raised: "#3a4850"
  rail-ink: "#eef2f3"
  rail-ink-muted: "#c3ccd0"
  rail-focus: "#9cc8ec"
  ground: "#e9ecec"
  plate: "#ffffff"
  plate-sunk: "#f4f6f6"
  line: "#cfd5d6"
  line-soft: "#e3e7e8"
  ink: "#1d2427"
  ink-muted: "#545f65"
  ink-faint: "#6d787d"
  action: "#2a5d86"
  action-hover: "#214d70"
  action-ink: "#ffffff"
  action-soft: "#e3edf5"
  tag: "#f2c23a"
  tag-ink: "#2b2205"
  tag-soft: "#fcf3d6"
  danger: "#b3261e"
  danger-soft: "#fbe9e7"
  warning: "#8a5d00"
  warning-soft: "#fdf1d8"
  success: "#2f6b3a"
typography:
  display:
    fontFamily: "Archivo Variable, Archivo, system-ui, sans-serif"
    fontSize: "1.75rem"
    fontWeight: 650
    lineHeight: 1.2
    letterSpacing: "-0.02em"
  headline:
    fontFamily: "Archivo Variable, Archivo, system-ui, sans-serif"
    fontSize: "1.375rem"
    fontWeight: 650
    lineHeight: 1.2
    letterSpacing: "-0.01em"
  title:
    fontFamily: "Archivo Variable, Archivo, system-ui, sans-serif"
    fontSize: "1.1875rem"
    fontWeight: 650
    lineHeight: 1.2
    letterSpacing: "-0.01em"
  body:
    fontFamily: "Archivo Variable, Archivo, system-ui, sans-serif"
    fontSize: "0.875rem"
    fontWeight: 400
    lineHeight: 1.45
    fontFeature: "tnum"
  body-sm:
    fontFamily: "Archivo Variable, Archivo, system-ui, sans-serif"
    fontSize: "0.8125rem"
    fontWeight: 400
    lineHeight: 1.45
  label:
    fontFamily: "Archivo Variable, Archivo, system-ui, sans-serif"
    fontSize: "0.75rem"
    fontWeight: 600
    letterSpacing: "0.06em"
    fontVariation: "wdth 75"
  inventory:
    fontFamily: "Archivo Variable, Archivo, system-ui, sans-serif"
    fontSize: "0.875rem"
    fontWeight: 650
    letterSpacing: "0.03em"
    fontFeature: "tnum"
    fontVariation: "wdth 75"
rounded:
  sm: "3px"
  md: "6px"
spacing:
  s1: "0.25rem"
  s2: "0.5rem"
  s3: "0.75rem"
  s4: "1rem"
  s5: "1.25rem"
  s6: "1.5rem"
  s8: "2rem"
  s10: "2.5rem"
components:
  button-default:
    backgroundColor: "{colors.plate}"
    textColor: "{colors.ink}"
    rounded: "{rounded.sm}"
    padding: "0 0.75rem"
    height: "2.25rem"
  button-default-hover:
    backgroundColor: "{colors.plate-sunk}"
  button-primary:
    backgroundColor: "{colors.action}"
    textColor: "{colors.action-ink}"
    rounded: "{rounded.sm}"
    padding: "0 0.75rem"
    height: "2.25rem"
  button-primary-hover:
    backgroundColor: "{colors.action-hover}"
  button-quiet:
    textColor: "{colors.action}"
    rounded: "{rounded.sm}"
    padding: "0 0.75rem"
    height: "2.25rem"
  button-quiet-hover:
    backgroundColor: "{colors.action-soft}"
    textColor: "{colors.action-hover}"
  button-sm:
    padding: "0 0.5rem"
    height: "1.875rem"
    typography: "{typography.body-sm}"
  input:
    backgroundColor: "{colors.plate}"
    textColor: "{colors.ink}"
    rounded: "{rounded.sm}"
    padding: "0.5rem 0.75rem"
    height: "2.25rem"
  segmented-pressed:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.plate}"
    height: "2.125rem"
  inventory-tag:
    backgroundColor: "{colors.plate}"
    textColor: "{colors.ink}"
    typography: "{typography.inventory}"
    rounded: "{rounded.sm}"
    padding: "0.125rem 0.5rem 0.125rem 1.125rem"
  inventory-tag-selected:
    backgroundColor: "{colors.tag}"
    textColor: "{colors.tag-ink}"
  plate:
    backgroundColor: "{colors.plate}"
    rounded: "{rounded.md}"
  thumb-plate:
    backgroundColor: "{colors.plate-sunk}"
    rounded: "{rounded.sm}"
    width: "3rem"
  rail:
    backgroundColor: "{colors.rail}"
    textColor: "{colors.rail-ink}"
    width: "13.5rem"
    padding: "1.25rem 0.75rem"
  rail-link-active:
    backgroundColor: "{colors.rail-raised}"
    textColor: "{colors.rail-ink}"
    rounded: "{rounded.sm}"
  rack:
    backgroundColor: "{colors.plate}"
    rounded: "{rounded.md}"
    padding: "1rem 1.25rem 1.25rem"
    width: "27.5rem"
  table-row-selected:
    backgroundColor: "{colors.tag-soft}"
  record-card-selected:
    backgroundColor: "{colors.tag-soft}"
---

# Design System: Artefacts

## Overview

**Creative North Star: "The Storage Rack"**

The collection as it is really kept: racked, tagged, and pulled out when you need to look. A powder-coated steel rail holds the navigation; the work area is a galvanised cool-grey ground on which every record, table and form sits on a white plate. Images sit on identical neutral plates at a fixed scale so staff recognise artefacts by eye, and each record carries a tie-on inventory tag set in Archivo's condensed width. The interface recedes; the artefact and its record lead.

Density is calm and high: a 14px body, 4px spacing base, tight table rows with fixed-scale thumbnails, condensed uppercase column labels. Colour is rationed by meaning. One enamel blue marks only things you can press. Rack-label yellow means "selected" and nothing else. Everything else is steel, ground, plate and ink.

The world rejects both the white-sidebar, blue-button admin template and the precious white-wall gallery. Its signature is the pull-out rack: selecting a record slides a panel out from the right with the image large under its yellow tag, the key facts, and Open/Edit.

**Key Characteristics:**
- Steel rail, galvanised ground, white plates; flat with a whisper of plate shadow.
- One action colour, only on pressables; one selection colour, only on selection.
- Archivo throughout; condensed width (75%) for tags, column labels, counts; tabular numerals everywhere.
- Fixed-scale thumbnails on a neutral plate (4:3, object-fit contain).
- Motion is physical and short: the rack slides out, rows travel to their new place on re-sort.
- Print is a first-class surface with its own running header and page logic.

## Colors

A cool steel-and-galvanised neutral world with exactly two meaningful accents: enamel blue for action, rack-label yellow for selection.

### Primary
- **Enamel Blue** (action): fills the primary button, colours quiet buttons, links, caret, focus ring, the upload bar and drop-zone hover. Hover deepens to **Enamel Blue Deep** (action-hover); **Enamel Wash** (action-soft) is the hover ground of quiet buttons, text selection and the keyboard-highlighted option in a picker list.

### Secondary
- **Rack-Label Yellow** (tag): the selected inventory tag, the rail's active-section icon, the chosen gallery thumbnail border, the selected record card border. **Label Wash** (tag-soft) fills selected table rows and selected cards. Yellow keeps the same value in dark mode; its text is always **Label Ink** (tag-ink).

### Neutral
- **Powder-Coat Steel** (rail): the navigation rail; **Raised Steel** (rail-raised) is hover and active link ground on it. Rail text is **Rail Ink** / **Rail Ink Muted**; focus on the rail uses **Rail Focus Blue**.
- **Galvanised Ground** (ground): page background, and the sticky form action bar.
- **White Plate** (plate): every record surface: tables, cards, form sections, the rack, buttons, inputs.
- **Sunk Plate** (plate-sunk): table header band, row hover, image plates behind thumbnails, drop zone.
- **Line** / **Soft Line** (line, line-soft): plate borders and control borders; row dividers inside plates.
- **Ink**, **Muted Ink**, **Faint Ink** (ink, ink-muted, ink-faint): text, secondary text and labels, placeholders and hover borders.

### States
- **Danger**, **Warning**, **Success** with soft grounds carry meaning only: errors, invalid fields, date-interpretation problems, destructive actions.

### Dark theme
Dark values are defined for every token (via `prefers-color-scheme` and an explicit `data-theme` switch on the rail). Rail goes to near-black steel (#121a1e), ground #1a2226, plates #222c31, ink #e4e9eb, action lightens to #7ab0dc with dark action-ink #0d1a24, tag-soft becomes #3a3418. Yellow stays #f2c23a.

### Named Rules
**The Pressable Blue Rule.** Enamel blue appears only on something you can press or type into (buttons, links, focus, caret, upload). Never on headings, icons-as-decoration, borders of static plates, or status.

**The Yellow Means Selected Rule.** Rack-label yellow marks the current selection (selected row, card, tag, gallery pick, active rail section) and nothing else. A property is not a selection: the primary image is marked in ink, and title tags on detail and edit pages are plain.

## Typography

**Display Font:** Archivo Variable (with Archivo, system-ui, Segoe UI, sans-serif)
**Body Font:** Archivo Variable
**Label Font:** Archivo Variable at condensed width (font-stretch 75%)

**Character:** One sturdy grotesque doing everything, with its width axis as the second voice: normal width reads the record, condensed width labels the inventory. Numerals are tabular across the whole app.

### Hierarchy
- **Display** (650, 1.75rem, 1.2, -0.02em): the record heading on detail and print pages.
- **Headline** (650, 1.375rem, 1.2): page titles in the work-area head, next to the record count.
- **Title** (650, 1.1875rem, 1.2): rack title, second-level headings.
- **Body** (400, 0.875rem, 1.45): everything readable; table cells, facts, form values. Titles in cells weight 550.
- **Body small** (0.8125rem): hints, sub-heads, pager, captions, field labels (550).
- **Label** (600, 0.75rem, 0.06em, uppercase, condensed): column heads, form-section legends (650), meta labels.
- **Inventory** (650, inherited size, 0.03em, condensed, tabular): the tie-on tag and inventory numbers (600, 0.02em in running text).

### Named Rules
**The Two Widths Rule.** Normal width reads; condensed width labels and counts. Do not set reading text condensed, and do not set inventory numbers, counts or column heads at normal width.

**The Tabular Rule.** All numerals are tabular (`font-variant-numeric: tabular-nums` on body). Columns of numbers and dates must align.

## Layout

A two-column shell: the steel rail fixed at 13.5rem on the left (sticky, full height), the work area filling the rest with 1.5rem padding. The page head is a wrapping flex row: title and count on the left, actions (search, Table/Grid switch, primary action) pushed right. Spacing follows a 4px base (s1–s10); plate internals use s2–s5, section gaps s4–s6.

Collection layout puts the list and the rack side by side (`minmax(0,1fr) auto`, gap s4); the rack is 27.5rem wide and sticky. The record grid reflows by whole cells (`auto-fill, minmax(11rem, 1fr)`). Detail pages split 5:7, collapsing to one column under 52rem. Forms cap at 64rem and use `auto-fit, minmax(16rem, 1fr)` field grids; the save bar is sticky at the bottom on the ground colour.

Responsive behaviour, from the build:
- **72rem**: the rack stops being a side column and becomes a bottom sheet (max 85dvh, top corners rounded, image plate 16:10).
- **48rem**: the rail becomes a sticky top bar (brand, sections, theme switch in one row; counts hidden); work padding drops to 1rem.
- **40rem**: the record table becomes stacked rows (thumb 4.5rem | tag, title, muted date) with sort controls as a wrapping header row; optional columns hide; image-manager tools wrap below.
- **30rem**: the rail's wordmark yields so the top bar fits; field rows stack label over value.

### Named Rules
**The Columns Never Move Rule.** Column order is fixed. Narrow screens hide optional columns or restack a row; they never reorder.

**The Contained Overflow Rule.** Stacks use a `minmax(0, 1fr)` column so wide content (tables) scrolls inside its plate rather than widening the page.

**The Clamp While Racked Rule.** While the rack is open, record-table cells clamp to two lines; the full text stays in the cell title.

## Elevation & Depth

Flat, with tonal layering doing most of the work: ground below, white plates above, steel beside. Shadows are restrained and each has one job.

### Shadow Vocabulary
- **Plate** (`0 1px 2px rgb(20 30 35 / 0.06)`): every plate at rest: tables, cards, form sections.
- **Rack** (`-18px 0 32px -24px rgb(20 30 35 / 0.55)`): the pulled-out rack only; it casts leftward over the list.
- **Pop** (`0 8px 24px -8px rgb(20 30 35 / 0.3)`): things that float over content: the rack's tag on its image, picker lists.
- **Sheet** (`0 -12px 40px -16px rgb(0 0 0 / 0.45)`): the rack as a bottom sheet under 72rem.

Dark mode deepens all shadows (plate 0.3, rack 0.8, pop 0.6 black alpha).

### Named Rules
**The Plate Rule.** Surfaces are white plates on the galvanised ground with a 1px line and the plate shadow. Nothing floats unless it is literally pulled out or dropped over content.

## Shapes

Small, machined corners: 3px on controls, tags, thumbnails and notices; 6px on plates, cards, the rack and form sections. Borders are 1px solid line; dashed borders only for the drop zone and widget configuration errors. The inventory tag has a punched hole: a 5px circle in ground colour inset on its left, which becomes a white hole when the tag is selected. The upload bar is the only fully rounded element.

## Components

### Buttons
Plain, firm, machined.
- **Shape:** 3px corners, 2.25rem tall, 0.75rem side padding, weight 550, 1px border.
- **Default:** white plate, line border, ink text; hover to sunk plate with faint-ink border.
- **Primary:** enamel blue fill and border, white text; hover to deep blue. One per page head ("New artefact", "Save").
- **Quiet:** transparent, blue text; hover blue wash. Used for secondary actions and the rack's close.
- **Danger:** danger text on a default button; hover adds danger wash and border. In reference lists, destructive row actions sit in muted ink at rest and take the danger colour only on hover/focus.
- **Small:** 1.875rem tall, 0.5rem padding, small text. **Icon:** square 2.25rem.
- **Transitions:** background and border, 220ms, `cubic-bezier(0.16, 1, 0.3, 1)`.

### Segmented control (Table/Grid)
A single bordered strip of plate with inner dividers; buttons in muted ink, weight 550. The pressed segment inverts to ink fill with plate text: a mode, not a selection, so never yellow.

### Inventory tag (signature)
The world's marker. Condensed, 650, tabular, 3px corners, line border on plate, punched hole at left. Selected: yellow fill and border, label-ink text, white hole. On the rack image it sits top-right with the pop shadow.

### Cards / Containers
- **Plate:** white, 1px line, 6px corners, plate shadow.
- **Record card:** plate with s2 padding, thumbnail full width, tag, title (600, 1.3) and muted meta; hover border faint ink; selected yellow border on label wash.
- **Form section:** fieldset plate with s5 padding, condensed uppercase legend sitting in the border.

### Image plates
Every image sits on sunk plate at a fixed aspect (4:3 default), `object-fit: contain`, soft-line border, 3px corners. Table thumbnails are 3rem wide; stacked phone rows 4.5rem; the rack plate is 4:3 (16:10 as a sheet).

### Inputs / Fields
- **Style:** white plate, 1px line, 3px corners, 2.25rem min height, s2/s3 padding; placeholder faint ink; hover border faint ink.
- **Focus:** 2px focus-blue outline at -1px offset, border turns focus blue.
- **Error:** danger border; danger message below. Field label 550 small; hint muted small.
- **Historical date:** the interpretation appears under the input in small tabular text, problems in warning colour; the live region takes no space when empty.

### Data table
White plate wrapper that scrolls horizontally inside itself. Header band on sunk plate, sticky, condensed uppercase labels in muted ink; sort controls inherit the label voice and go to ink when active. Rows divided by soft line; record rows have 0.375rem vertical padding. Hover row on sunk plate; selected row on label wash; focused row a 2px inset focus outline.

### Navigation (steel rail)
Condensed uppercase wordmark (700, 0.08em). Links in muted rail ink, weight 550, s2/s3 padding, 3px corners; hover and active on raised steel with full rail ink; the active link's icon turns rack-label yellow. Counts sit right-aligned in condensed small text. The light/dark/system theme switch is a small bordered segment at the foot of the rail. Under 48rem the rail is a top bar.

### Pull-out rack (signature)
A 27.5rem plate pulled out beside the list: sticky, scrolls internally, rack shadow, slides in from the right over 260ms (translate 2.5rem with a matching clip). Contents in order: close button only (no label above the heading), the image on a 4:3 plate with the yellow inventory tag tied on top-right, title, key facts in a 6.5rem label column, Open/Edit actions. Arrow keys walk records; Esc slides it back. Under 72rem it becomes a bottom sheet.

### Travelling rows
On re-sort, rows animate from their old position to the new one (FLIP, 320ms, `cubic-bezier(0.16, 1, 0.3, 1)`). Reduced motion disables it and collapses all transitions to 1ms.

### Image manager
Item rows on plates (thumb | name and fields | tools). The primary image is marked with an ink-outlined condensed label carrying a filled star, and its row border is faint ink. Drop zone: dashed faint-ink border on sunk plate, blue border and wash while dragging over.

### Print record
Paged.js preview, A4 or Letter, portrait or landscape, margins 18/16/20/16 mm. Running header: inventory number (condensed 650, 0.04em) and title; footer: condensed uppercase "Artefacts" and "page N of M", all 8.5pt muted ink. Body 10pt, display 20pt. Images, field rows and figures never split across pages; table rows never split and headers repeat. Image height is capped per paper (at most 150mm, leaving room for the heading). Image plates print on white; shadows drop.

## Do's and Don'ts

### Do:
- **Do** keep enamel blue (action) on pressables only, and use one primary button per head.
- **Do** use rack-label yellow (tag, tag-soft) only to show the current selection.
- **Do** mark properties (primary image, a record's own tag on its detail page) in ink, not yellow.
- **Do** set inventory numbers, counts and column labels in Archivo condensed (75%) with tabular numerals.
- **Do** put every image on the same neutral sunk plate at a fixed aspect, contained, never cropped.
- **Do** place every record surface on a white plate with a 1px line and the plate shadow, on the galvanised ground.
- **Do** let the grid reflow by whole cells and keep table column order fixed at every width.
- **Do** keep destructive row actions in muted ink at rest, danger colour on hover and focus.
- **Do** design every record view to print: no unit split across a page, running inventory header, page N of M.

### Don't:
- **Don't** use blue for decoration, status or headings, or yellow for highlights, badges, warnings or emphasis.
- **Don't** put eyebrow labels or kickers above headings; the rack carries only its close button above its title.
- **Don't** reorder or move columns on narrow screens; hide optional columns or restack the row.
- **Don't** add shadows beyond the four roles (plate, rack, pop, sheet) or lift plates on hover.
- **Don't** use corners larger than 6px or pill shapes on controls.
- **Don't** fall back to the white-sidebar blue-button admin template or a white-wall gallery look.

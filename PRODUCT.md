# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

A small team managing a **private or corporate art collection**: the people who register new acquisitions, keep catalogue records accurate, track where each artefact came from, and (soon) manage loans and exhibitions. They use the app mainly at a desk on large screens, often for long sessions of data entry and lookup. Phones must also work, for checking or looking up a record away from the desk.

## Product Purpose

Artefacts is the single source of truth for the collection. It catalogues every artefact (identity, description, imprecise historical dating, acquisition including price, provenance by region and period) with its images, and lets staff find, view, edit and print those records. Success means staff trust the catalogue enough to stop keeping parallel spreadsheets, and can produce a clean printed record of any artefact on demand.

## Positioning

The product is built around the collection's own data rather than a fixed set of screens: every screen is composed of pluggable widgets that a page feeds with data, over a read model described by a field catalog. That is the foundation for user-defined views (custom queries and custom pages) that staff will be able to build themselves later, without a developer.

## Operating Context

- Single institution, one collection.
- Data entry is careful and detailed. Dates are often imprecise ("c. 1650", "late 17th century") and must be kept exactly as written.
- Images are the primary way staff recognise an artefact. Originals can be large archival files (TIFF), and the app shows them at sized renditions.
- Printed or PDF artefact records are a routine output, so print layout matters as much as the screen.
- Planned, not yet built: outgoing loans and derived status, exhibitions, import from xlsx/csv with images, a custom query engine and page builder, roles and permissions, server-side batch PDF, and video.

## Capabilities and Constraints

- Terminology: the central object is an **artefact** (British spelling throughout). **Regions** form a managed reference list used by provenance.
- Acquisition price is financially sensitive. Today everyone can see it; permissions will restrict it later, so the UI must be able to hide such fields cleanly.
- Every widget gets its data from its container and never fetches its own. Pages must be composable from widgets.
- Print: paginated preview, no content unit cut across a page boundary, tables continue across pages with repeated headers.
- Stack (existing codebase): .NET 10 API, React 19 + TypeScript + Vite frontend, plain CSS with design tokens, no UI framework.

## Brand Commitments

None yet. There is no institution logo, colour or typeface the app must use. The product name is "Artefacts".

## Evidence on Hand

No real collection data or photography is in the repository yet. Seed and sample data are placeholders and must not be presented as real holdings.

## Product Principles

1. **The artefact comes first.** Images and record content lead; the interface recedes.
2. **Keep what the curator wrote.** Never silently normalise, round or reinterpret entered data; show how it was interpreted next to it.
3. **Composable by design.** Every screen is built from data-fed widgets, so that later user-defined views reuse the same parts.
4. **Print is a first-class output.** Anything shown as a record must print well.
5. **Calm density.** Staff work through many records; information should be dense but never noisy.

## Accessibility & Inclusion

No formal conformance target has been set. Follow good practice: keyboard operability, visible focus, sufficient contrast in both light and dark themes, and responsive layouts down to phone width.

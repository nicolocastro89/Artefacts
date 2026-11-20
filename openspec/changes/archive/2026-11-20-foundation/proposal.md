# Proposal

## Why

The institution needs a system to catalogue and monitor its art collection (artefacts, their origin, acquisition, media, and later loans and exhibitions), and the most important long-term feature — user-defined views and pages — only works if the architecture is designed for it from the start. This first change establishes that architecture with a thin, working slice through every layer (domain, persistence, read model, media, pluggable frontend widgets, paged printing), so every later feature builds on proven foundations rather than retrofitting them.

## What Changes

- New repository setup: git, beads issue tracking with a roadmap of follow-up epics, a .NET 10 backend solution and a React + TypeScript frontend, and a Docker Compose development environment (PostgreSQL, SeaweedFS as a persistent S3-compatible store, and imgproxy).
- New DDD backend with a write side for the **Artefact** aggregate (inventory number, title, maker, creation date, medium, dimensions, description, acquisition with a `Money` amount, and provenance entries) and the **Region** reference aggregate (a selectable list that users can add to).
- A `HistoricalDate` / `HistoricalPeriod` value object that stores imprecise dates ("c. 1650", "1680s", "late 17th century") as the original display text plus a computed earliest/latest range and a precision, so they can be filtered and sorted.
- Artefact images: upload to S3-compatible storage (original kept unchanged), validation by magic bytes and size, a primary image per artefact, and signed imgproxy URLs for the `thumb`, `web` and `print` presets. The backend contains no image-processing code.
- A separate read side: PostgreSQL views (starting with `v_artefact`) mapped in a dedicated read DbContext, plus a field catalog generated from that read model's metadata. Each catalog field has a label, a type, filter/sort capability, and an optional sensitivity tag (acquisition price is tagged `financial`). This is the groundwork for the future semantic layer and custom views; it does not include a query engine.
- A published OpenAPI document, so frontend tooling can discover response shapes.
- A frontend design system built with the impeccable skill. The UI is professional and unobtrusive.
- A frontend widget architecture: a widget registry where each widget declares typed binding slots, a config schema and print behaviour. Containers own a data source and route response fields to widget bindings; list containers switch the binding scope to the current item. The built-in pages use this same mechanism.
- Artefact list and artefact detail pages composed from the initial widgets (Image, FieldList, Table/Repeater).
- A paginated print preview (Paged.js) of the artefact detail page. Content is laid out on real pages, widgets marked keep-together never split across a page boundary, and tables continue onto the next page with their header repeated.

## Capabilities

### New Capabilities
- `artefact-catalogue`: Create, read, update and list artefacts, including historical dating, acquisition (with a monetary value tagged as sensitive), and provenance entries that reference regions over historical periods.
- `region-reference`: A managed list of regions that users can pick from and add to, used by provenance.
- `artefact-media`: Uploading, validating, storing and retrieving artefact images; choosing the primary image; and getting signed URLs for each image size.
- `read-model`: Read-only, query-optimised views of domain data and a machine-readable field catalog that describes the fields available for display, filtering and sorting, including sensitivity tags.
- `widget-composition`: Frontend widgets with typed binding slots that are fed by a parent container's data source through a configurable field mapping, including scoping to list items.
- `artefact-browsing`: Frontend pages for finding and viewing artefacts (list and detail), built from widgets.
- `print-preview`: Paginated print preview and printing of displayed pages without cutting content units across pages.

### Modified Capabilities
<!-- None: this is the first change in a greenfield project. -->

## Impact

- **New code:** `backend/` (.NET 10 solution: Domain, Application, Infrastructure, Api, tests), `frontend/` (React + TypeScript + Vite), `docker-compose.yml`, `.beads/`.
- **Dependencies:** EF Core 10 with the Npgsql provider, the AWS SDK for .NET (S3), Paged.js, and a React data-fetching/routing stack. Docker is required for local development.
- **External systems:** PostgreSQL, S3-compatible object storage (SeaweedFS locally; AWS S3 or another S3-compatible store later), and imgproxy.
- **Out of scope (tracked as beads epics):** loans and derived status, exhibitions, import from xlsx/csv, the custom query engine, page definitions and the view-builder UI, server-side batch PDF, roles and permissions, video, and an imgproxy caching layer.

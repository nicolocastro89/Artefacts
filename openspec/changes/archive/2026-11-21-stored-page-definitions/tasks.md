## 1. Backend: model and storage

- [x] 1.1 Add the `StoredPage` entity (id, slug, name, definition JSON, is_system, show_in_navigation, navigation_label, timestamps) with slug and name rules, and the `stored_pages` table with unique indexes on slug and name (case-insensitive) in the regenerated `Initial` migration
- [x] 1.2 Add `StoredPageValidator`: slug rules, definition shape, existing stored queries for `query` data sources, navigation label; errors carry the path in the definition
- [x] 1.3 Add the page handler in `Application` (list, read by id and by slug, create, update, delete, duplicate) with conflicts for system pages and validation errors for names and slugs, with DTOs
- [x] 1.4 Add `Api/PageEndpoints.cs` under `/api/pages` (including `by-slug/{slug}`, `navigation=true` filter and `duplicate`) and regenerate the OpenAPI document and frontend API types

## 2. Backend: system pages

- [x] 2.1 Write `artefact-list.json` and `artefact-detail.json` from the current TypeScript definitions, as embedded resources, with `recordSections.ts` helpers used only by these pages inlined
- [x] 2.2 Add `SystemPageSync` (upsert at start, off where system query sync is off) and check that system pages cannot be changed or deleted
- [x] 2.3 Integration tests: create, duplicate-slug and invalid-slug rejection, structural validation errors with paths, unknown stored query, system page conflict, duplicate a system page, release update of a system page

## 3. Frontend: loading and routes

- [x] 3.1 Add `usePage(slug)` with loading, not-found and retryable error states, and change `PageRoute` to take a slug
- [x] 3.2 Route `/artefacts` and `/artefacts/:id` through `artefact-list` and `artefact-detail`, add `/p/:slug`, and delete `artefactList.ts` and `artefactDetail.ts`
- [x] 3.3 Add the navigation section for pages shown in navigation to `AppShell`
- [x] 3.4 Update the frontend test fixtures and tests so list and detail render from loaded definitions; compare screenshots at 1440 and 390 widths with the previous pages

## 4. Frontend: page editor

- [x] 4.1 Add `lintPage` (unknown widgets, missing or mistyped slots, binding paths absent from the query description) reusing the renderer's binding validation, with unit tests
- [x] 4.2 Add the page list (`/pages`) with create, duplicate and delete, and the editor (`/pages/:id`) with the JSON field, name, slug, navigation settings, live preview and server error display; read-only with a duplicate action for system pages
- [x] 4.3 Component tests for the editor: preview problems, save rejected with a path, system page read-only

## 5. Acceptance

- [x] 5.1 Duplicate `artefact-list`, put the copy in navigation, and confirm `/p/<slug>` shows the same list as `/artefacts`
- [x] 5.2 Build a small custom page over a user stored query, shown in navigation, with a record id taken from `?id=`
- [x] 5.3 Run backend and frontend tests, lint and build; `openspec validate stored-page-definitions --strict`

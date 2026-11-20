# Tasks

> **Status (2026-11-20).** The backend has been verified against Docker: `dotnet test` passes (108 tests), the compose stack comes up healthy, `dotnet ef database update --context CollectionDbContext` applies to the compose PostgreSQL, and an end-to-end smoke test (TIFF + JPEG upload, all three presets, rejection of unsigned/tampered URLs, primary switch) passed. Fixes made during verification:
>   - Media storage is now chosen when the service is resolved, not when it is registered, so test configuration overrides take effect.
>   - Enum query parameters (`sort`, `direction`) are parsed case-insensitively, matching the camelCase contract, and invalid values return field-keyed validation errors.
>   - imgproxy URLs use the bare preset form (`/{sig}/web/plain/...`) that `IMGPROXY_ONLY_PRESETS` requires.
>   - `Artefacts.Application.Tests` now has tests, because an empty test module fails the run.
> - **Backend:** all tasks verified. 9.5 was checked by removing `a.medium` from the view: the drift test failed, then passed once the column was restored.
> - **Frontend:** 12.2, 12.3 and 12.5 have component tests (`npm test`: 32 tests). To let `api.*` calls run under test, `api/client.ts` looks up `fetch` per request, and `test/setup.ts` resolves relative `Request` URLs as a browser does. 12.4 was verified in headless Edge against the running stack (23 checks: TIFF and JPEG upload, TIFF thumbnails through imgproxy, make primary, caption save and reload, captions on the record page and in print, deleting non-primary and primary images, a rejected non-image). That check found the detail gallery opening on the first upload instead of the primary (art-hfw.2, fixed with a test).
> - **Print preview (13.2–13.5), verified 2026-11-20** in headless Edge over the DevTools protocol, against the seed. Checked: A4/Letter and portrait/landscape; no hidden or overflowing content; `thead` repeated on a split table; running header and footer on every page; the printed PDF has the same page count as the preview. Four bugs were fixed:
>   1. Lazy print images stalled Paged.js, so print images now load eagerly.
>   2. The paginated clone kept the source's hiding class, which made every page after the first blank.
>   3. The running-header selectors depended on a root class that Paged.js drops.
>   4. Changing paper or orientation left the previous layout's observers and `@page` styles running; `destroyPreviewer` now tears them down.
>
>   The print image height is also capped from the page box, so the heading and primary image share page 1 in landscape. Because of that cap, no seeded image triggers the "scale a unit taller than a page" path, which remains untested.
> - **Seed (14.1):** `node scripts/seed/seed.mjs` (dependency-free; uses the public API) creates 31 regions and 30 artefacts with 32 PNG/TIFF images, and a rerun creates nothing. The edge cases are listed at the top of `scripts/seed/data.mjs`. The user confirmed the list and detail pages; print was verified by the checks above.
> - **README (1.4):** every command and URL in it was run as written against the working tree, not a fresh clone, because nothing is committed yet. After the first commit (7c3c5ad) the quick start was repeated from a fresh clone: backend build, `npm install`, production build, 32 frontend tests, API health, Scalar, OpenAPI, Vite and a no-op seed all passed. A clone under a very deep Windows path fails `npm install` and MSBuild (260-character path limit); the README troubleshooting notes this.
> - **Design (impeccable):** Storage Rack direction chosen; contract in `.impeccable/surfaces/`. 10.3 is done: the finish review (headless captures at 1440 and 390, light and dark, every list/detail/form/regions/print state) returned *fix* with seven material fixes (yellow reserved for selection, two-line cells while the rack is open, stacked phone rows, no eyebrow, grid alignment, form polish, quiet row deletes), all scored resolved, verdict *ship*. The documenter then wrote `DESIGN.md` and `.impeccable/design.json` from the shipped CSS. Open design notes are beads under art-hfw.
> - **Deviations recorded:** SeaweedFS replaced the AWS Emulator (see design D12). TypeScript is pinned to 5.9, because openapi-typescript needs the TypeScript 5 compiler API. The OpenAPI document is generated at build time into `backend/openapi/`.
> - **Nothing committed** beyond the automatic `bd init` commit.

## 1. Repository and work tracking

- [x] 1.1 Run `git init` and add a `.gitignore` covering .NET, Node, IDE folders and `.env` files; verify `git status` shows no `bin/`, `obj/` or `node_modules/` entries
- [x] 1.2 Run `bd init` and verify `bd list` runs without error
- [x] 1.3 Create beads epics with their dependencies: loans + derived status view, exhibitions, xlsx/csv import with images, query engine (semantic layer), page definitions persistence, view builder UI, server batch PDF (Playwright), roles & permissions (including sensitive-field enforcement and imgproxy URL expiry), imgproxy caching proxy, video support. Verify `bd ready` lists only the epics whose dependencies are met
- [x] 1.4 Add a root `README.md` explaining the prerequisites and how to start the stack; verify that following it from a clean clone gets the stack running

## 2. Development environment

- [x] 2.1 Add the SeaweedFS S3 identities config (`docker/seaweedfs/s3.json`, with fixed development credentials); verify the file is valid JSON and is mounted by compose
- [x] 2.2 Add `docker/docker-compose.yml` with `postgres:17`, `s3`, a bucket-init one-shot and `imgproxy` (S3 source enabled, signing key/salt, presets `thumb`/`web`/`print`, `ONLY_PRESETS`, raised max source resolution), with configuration in `docker/.env.example`; verify that `docker compose up` brings every service to healthy and the bucket exists
- [x] 2.3 Manually smoke-test imgproxy: upload a test image to the bucket with the AWS CLI, request a signed `thumb` URL, and verify a resized image is returned while an unsigned URL is rejected

## 3. Backend skeleton

- [x] 3.1 Create the .NET 10 solution with the Domain, Application, Infrastructure and Api projects plus three test projects, with project references following the dependency rule (Domain has no references); verify `dotnet build` succeeds
- [x] 3.2 Configure the Api: Minimal API endpoint groups, ProblemDetails, a global domain-exception-to-ValidationProblemDetails mapping with camelCase field keys, built-in OpenAPI, and Scalar in Development; verify `/openapi/v1.json` and the Scalar UI load
- [x] 3.3 Add configuration binding (connection string, S3 options with ServiceURL/path-style/bucket, imgproxy options with internal and public base URL plus key/salt, max upload size) with startup validation; verify the app fails fast with a clear message when a required setting is missing
- [x] 3.4 Set up the integration test fixture with Testcontainers PostgreSQL, `WebApplicationFactory` and `FileSystemMediaStorage`; verify a trivial health endpoint test passes

## 4. Domain model

- [x] 4.1 Implement `HistoricalDate` and its parser following design D5 (every listed form, explicit-range override, rejection of unparseable text without a range); verify with table-driven unit tests covering every row in D5 and the spec scenarios
- [x] 4.2 Use `HistoricalDate` for provenance periods (no separate `HistoricalPeriod` type is needed, because a period is the same year range with a precision); verify with unit tests covering provenance entries that have periods
- [x] 4.3 Implement `Money` with ISO 4217 validation and the non-negative rule; verify unit tests for a valid EUR value, a negative amount and the code "XYZ"
- [x] 4.4 Implement the `Region` aggregate (name normalisation, rename, parent assignment) and a domain service for cycle detection; verify unit tests for the self-parent and ancestor-cycle cases
- [x] 4.5 Implement the `Artefact` aggregate: required inventory number/title, descriptive fields, `Acquisition`, ordered `ProvenanceEntry` list, and `ImageAttachment` list with the primary-image invariants (first is primary, make primary, deleting the primary promotes the earliest remaining); verify unit tests for every artefact-media primary-image scenario
- [x] 4.6 Implement the `MediaAsset` aggregate (storage key, original file name, format, size, uploaded time); verify unit tests

## 5. Persistence (write side)

- [x] 5.1 Implement `CollectionDbContext` with snake_case naming, complex types for `Money`/`HistoricalDate`, `OwnsMany` provenance with a position column, the unique `normalized_name` index on regions, and a provenance→region foreign key with `RESTRICT`; verify the model builds without warnings
- [x] 5.2 Add a unique index on the artefact inventory number and translate its violation into a validation error on `inventoryNumber`; verify with an integration test of the duplicate scenario
- [x] 5.3 Create the `Initial` migration (tables only); verify that `dotnet ef database update` against the compose PostgreSQL succeeds

## 6. Regions API

- [x] 6.1 Implement the list/search, add (returning the existing region on a normalised-name match), rename, set-parent and delete handlers and endpoints; verify integration tests for every region-reference scenario, including the rename that changes how provenance displays and the deletion blocked because the region is in use

## 7. Artefacts API

- [x] 7.1 Implement the create and update handlers and endpoints, with validation errors mapped to field keys; verify integration tests for the create, duplicate, missing-title, update and not-found scenarios
- [x] 7.2 Implement acquisition and provenance editing as part of create/update, with region-existence validation; verify integration tests for the purchase-with-price, invalid-currency, provenance-order and unknown-region scenarios
- [x] 7.3 Implement the `POST /api/historical-dates/parse` endpoint; verify an integration test showing that "late 17th century" returns 1667–1700 with precision century
- [x] 7.4 Implement the artefact detail endpoint (aggregate mapped to a DTO including provenance with region names and images with `ImageUrls`); verify an integration test for the provenance scenario

## 8. Media

- [x] 8.1 Implement `IMediaStorage` with `S3MediaStorage` (AWS SDK, configurable endpoint and path-style addressing) and `FileSystemMediaStorage`; verify an integration test (a Testcontainers SeaweedFS container) that puts, gets and deletes an object
- [x] 8.2 Implement format sniffing for JPEG, PNG, TIFF and WebP by magic bytes; verify unit tests including a PDF renamed to `.jpg`
- [x] 8.3 Implement the streaming upload endpoint (Kestrel limit and application size check, store the object then commit, compensating delete on failure); verify integration tests for valid TIFF, disguised file, oversized file and unknown artefact
- [x] 8.4 Implement the make-primary, caption-edit and delete-image endpoints (deleting also removes the stored object); verify integration tests for the primary-change, primary-deletion and caption scenarios
- [x] 8.5 Implement `ImgproxyUrlBuilder` (HMAC-SHA256 signature, presets, public base URL); verify a unit test against a known signature vector and a manual check that the browser displays a TIFF's `web` URL

## 9. Read side

- [x] 9.1 Write `v_artefact.sql` (flattened fields, primary image key, image count) and a migration that applies it; verify the view returns rows after seeding
- [x] 9.2 Implement `ReadDbContext` (no tracking, keyless `ArtefactRow` mapped with `ToView`) and field-metadata attributes on `ArtefactRow`, with acquisition price fields marked `[Sensitive("financial")]`; verify the model builds
- [x] 9.3 Implement the artefact list endpoint on `ReadDbContext` (free text on inventory number/title/maker, sort by inventory number/title/creation date with NULLs last, pagination with total count); verify integration tests for the pagination, search and sorting scenarios
- [x] 9.4 Implement `FieldCatalogBuilder` and `GET /api/catalog`; verify integration tests asserting the "title" (text) and "primaryImage" (image) fields, the `financial` tag on the price fields and no tag on "title"
- [x] 9.5 Add an integration test comparing `ReadDbContext` mappings with `information_schema.columns` for each view; verify it fails when a column is deliberately removed from the view, then passes again once the column is restored
- [x] 9.6 Verify the OpenAPI document contains the artefact detail response schema with title and `ImageUrls` fields (an integration test parses `/openapi/v1.json`)

## 10. Frontend foundation and design system

- [x] 10.1 Scaffold Vite + React + TypeScript (strict), React Router, TanStack Query, Vitest and Testing Library; verify that `npm run build` and `npm test` pass
- [x] 10.2 Add OpenAPI client generation (`openapi-typescript` with `openapi-fetch`) as an npm script, plus a typed API module; verify the generated types compile against the running API
- [x] 10.3 Use the impeccable skill to establish the design system (tokens, typography, layout shell, navigation, table, card and form primitives) for a professional, unobtrusive, image-first UI, and record it in `DESIGN.md`; verify that `DESIGN.md` exists and the app shell renders with the tokens
- [x] 10.4 Build the app shell with navigation (Artefacts, Regions) and a ProblemDetails error display helper that maps validation errors to fields; verify a component test of the field-error mapping

## 11. Widget composition

- [x] 11.1 Implement `defineWidget`, `WidgetRegistry` (listable) and the widget contract types (slots, config, print behaviour); verify a unit test that registers a widget and lists its contract
- [x] 11.2 Implement the binding path resolver (`a.b`, `[]` lists, item scope) and the build-time field-type map derived from OpenAPI schemas (`ImageUrls`→image, `HistoricalDate`→historical-date, `Money`→money, primitives); verify unit tests for nested, list and item-relative paths
- [x] 11.3 Implement binding validation and the `WidgetConfigError` placeholder; verify component tests for the type-mismatch and missing-required-slot scenarios
- [x] 11.4 Implement the `PageDefinition` types and `PageRenderer` (Container with endpoint data source, ListContainer); verify a test that serialises a page definition to JSON and back and renders identical output
- [x] 11.5 Implement the widgets `image`, `image-gallery`, `field-list`, `table`, `repeater` and `heading`; verify a component test showing the `image` widget rendering from both a detail response and a list row

## 12. Artefact and region pages

- [x] 12.1 Build the artefact list page as a `PageDefinition` with table and grid modes, search, sort and pagination synchronised to the URL, and a thumbnail placeholder; verify component tests for grid switching, URL restoration and the placeholder
- [x] 12.2 Build the artefact detail page as a `PageDefinition` (primary image, gallery with `web`-size viewer, fields, acquisition, provenance repeater); verify a component test for the provenance scenario
- [x] 12.3 Implement the action widget `artefact-form` (create/edit, historical date field with live parse feedback, money input, provenance editor with a region picker that can add regions inline, server errors shown on their fields); verify component tests for the date-feedback, inline-region and duplicate-inventory-number scenarios
- [x] 12.4 Implement the action widget `image-manager` (multi-file upload with progress, make primary, caption edit, delete); verify against the running stack by uploading a TIFF and a JPEG and checking the primary image switches
- [x] 12.5 Build the region management page (list, add, rename, re-parent, delete with an in-use error); verify a component test for the delete-in-use scenario

## 13. Print preview

- [x] 13.1 Add a `PrintContext` and print rendering for widgets (`print` image URLs, interactive controls hidden); verify a component test showing edit buttons absent in print mode
- [x] 13.2 Implement the `/artefacts/:id/print` route with a `PrintPreview` component running Paged.js after data and images have loaded, paper size (A4/Letter) and orientation selection, and a page count; verify manually that the preview shows separate pages with a count
- [x] 13.3 Apply `keepTogether` (break-inside: avoid) and pre-pagination scaling of oversized units; verify manually with an artefact whose image lands near a page bottom (it moves whole to the next page) and with an oversized image (it is scaled onto one page)
- [x] 13.4 Implement row-safe table breaking and a Paged.js handler that repeats `thead`; verify manually with an artefact that has enough provenance entries to span pages
- [x] 13.5 Add running headers (inventory number and title) and footers ("page N of M"); verify manually in the preview, and verify that the browser's print dialog shows the same page breaks

## 14. End-to-end verification

- [x] 14.1 Add a seed script with roughly 30 artefacts, regions, provenance and sample images (including a TIFF); verify the list, detail and print preview all work against the seeded data
- [x] 14.2 Run `dotnet test`, `npm test` and `openspec validate foundation --strict`; verify all pass
- [x] 14.3 Commit the foundation and record any discovered follow-up work as beads issues; verify `git log` shows the commit and `bd list` contains the follow-ups

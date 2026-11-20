# Design

## Context

This is a greenfield repository: it contains only the OpenSpec scaffolding. The motivation is in `proposal.md`, and the behaviour contracts are in `specs/`. These constraints were settled while exploring:

- Single institution, so no multi-tenancy.
- Roles and permissions are not built yet, but the design must leave room for them. Acquisition price is the first sensitive field.
- Loans will be outgoing only, and artefact status will be *derived* from them later. Neither is part of this change, but the read-side design has to accommodate them.
- Custom views will eventually be built by non-technical staff through a semantic layer. The friendly builder UI comes later; the layering has to exist from the start.
- Video is a stretch goal. This change covers images only.
- Media storage must be pluggable. It starts with a persistent, open-source S3-compatible store (SeaweedFS) for local development.

## Goals / Non-Goals

**Goals:**
- Prove every architectural seam with real code: the DDD write side, the read side with its field catalog, object storage with image delivery, the widget/binding composition model, and paged printing.
- Keep the domain model free of persistence and HTTP concerns.
- Make the custom-views roadmap (query engine, then page definitions, then builder UI) an *additive* path: no rewrite of the widgets or the read model.

**Non-Goals:**
- A query engine or user-defined queries. `/api/queries` does not exist yet.
- Saving or loading page definitions from the database. Built-in pages are defined as serialisable data in frontend code only.
- Authentication and authorisation enforcement.
- Server-side PDF generation, or caching in front of imgproxy.
- Production deployment topology.

## Decisions

### D1. Repository and solution layout

```
/backend
  Artefacts.sln
  src/Artefacts.Domain          aggregates, value objects, domain errors (no dependencies)
  src/Artefacts.Application     use cases (commands/queries), ports (IMediaStorage, IImageUrlBuilder)
  src/Artefacts.Infrastructure  EF Core (write + read contexts, migrations), S3, imgproxy signing
  src/Artefacts.Api             Minimal API endpoints, ProblemDetails, OpenAPI
  tests/Artefacts.Domain.Tests
  tests/Artefacts.Application.Tests
  tests/Artefacts.Api.IntegrationTests      (Testcontainers: PostgreSQL)
/frontend                        Vite + React + TypeScript
/docker                          compose file, SeaweedFS S3 config, imgproxy env
/openspec, /.beads
```

This is a modular monolith: bounded contexts (Catalogue, Media, Reference) are folders and namespaces inside each layer, not separate projects. *Alternative considered:* a project per bounded context. That adds ceremony before any boundary has been tested; a namespace can be extracted into a project later.

### D2. Application layer without a mediator library

Use cases are plain handler classes (`CreateArtefactHandler`, ...) registered in DI and called from endpoints. *Alternative considered:* MediatR. Its licence has changed to commercial terms, and it adds indirection without behaviour we need. Cross-cutting concerns (validation, transactions) go in small explicit decorators or endpoint filters.

### D3. Minimal APIs and the built-in OpenAPI document

Endpoints are grouped per context (`/api/artefacts`, `/api/regions`, `/api/artefacts/{id}/images`, `/api/catalog`, `/api/historical-dates/parse`). OpenAPI comes from the .NET 10 built-in `Microsoft.AspNetCore.OpenApi`, with Scalar as a development UI. Errors are returned as RFC 9457 ProblemDetails. Validation failures use `ValidationProblemDetails` with camelCase field keys, so that the frontend can attach each error to its field (artefact-browsing spec).

### D4. One write DbContext, aggregates referenced by ID

`CollectionDbContext` maps every aggregate. Aggregates refer to each other by ID only: Artefact holds `RegionId` inside its provenance entries, not a `Region` navigation. Database foreign keys still exist where the database has to protect integrity (provenance to region with `ON DELETE RESTRICT`, which backs the "region in use" rule). *Alternative considered:* a DbContext per bounded context. That makes cross-context transactions awkward and gains nothing for a single-database monolith.

Value objects are mapped as follows:
- `Money` → EF complex type: `amount numeric(19,4)` and `currency char(3)`. Currency codes are validated against an ISO 4217 list in the Domain.
- `HistoricalDate` → complex type: `display text`, `earliest_year int null`, `latest_year int null`, `precision smallint`.
- `ProvenanceEntry` → `OwnsMany`, stored in the table `artefact_provenance` with a `position` column for ordering.
- `Region.NormalizedName` (lower-cased and trimmed) has a unique index. This enforces case-insensitive uniqueness without relying on `citext`.

PostgreSQL naming uses snake_case (`EFCore.NamingConventions`).

### D5. HistoricalDate parsing lives in the Domain and is exposed as an endpoint

The parser is a pure Domain function, so it is fully unit-testable. The frontend gets live interpretation feedback by calling `POST /api/historical-dates/parse`, so the rules are never duplicated in TypeScript. Range conventions:

| Input | Earliest–Latest | Precision |
|---|---|---|
| `1654` | 1654–1654 | Year |
| `c. 1650` / `circa 1650` / `ca. 1650` | 1640–1660 (±10) | Approximate |
| `1680s` | 1680–1689 | Decade |
| `17th century` | 1601–1700 | Century |
| `early / mid / late 17th century` | 1601–1633 / 1634–1666 / 1667–1700 | Century |
| `1650-1660` | 1650–1660 | Year (range) |
| `before 1720` | null–1719 | Approximate |
| `after 1800` | 1801–null | Approximate |

Text the parser cannot interpret is accepted only together with explicit earliest and latest years. Sorting uses `COALESCE(earliest_year, latest_year)`, with NULLs last.

### D6. Images: Artefact owns attachments, MediaAsset owns the stored file

- `MediaAsset` (aggregate root, Media context): storage key, original file name, detected format, size in bytes, upload time.
- `Artefact.Images`: a list of `ImageAttachment` entities (`MediaAssetId`, `Caption`, `IsPrimary`, `AttachedAt`). The "exactly one primary image" rule involves all of an artefact's images, so the Artefact aggregate enforces it.

*Alternative considered:* image entities embedded entirely inside Artefact. This is simpler today, but exhibitions and loan reports will need to reference media independently later.

Upload flow: multipart request to the API. The API sniffs the format from the first bytes (JPEG `FF D8 FF`, PNG signature, TIFF `II*\0` / `MM\0*`, WebP `RIFF....WEBP`), enforces the configured maximum size (both the Kestrel request limit and an application check), and streams the file to `IMediaStorage` under the key `originals/{mediaAssetId}`. Only after that does it commit the database transaction. If the database step fails, the stored object is deleted on a best-effort basis. Files are never buffered fully into memory.

`IMediaStorage` has two implementations: `S3MediaStorage` (AWS SDK; ServiceURL and path-style addressing are configurable, so it works against SeaweedFS, AWS, or any S3-compatible store) and `FileSystemMediaStorage` (for tests and fallback).

### D7. imgproxy produces all image sizes, the API only signs URLs

imgproxy runs as a container and reads originals directly from S3 using `s3://<bucket>/<key>` sources (`IMGPROXY_USE_S3`, `IMGPROXY_S3_ENDPOINT` pointing at SeaweedFS with path-style addressing). Configuration:
- `IMGPROXY_KEY` / `IMGPROXY_SALT` are shared with the API. Unsigned URLs are rejected.
- `IMGPROXY_ONLY_PRESETS=true`, with presets `thumb` (fit 400), `web` (fit 1600) and `print` (fit 3000, high quality). Output is JPEG or WebP, chosen by format negotiation for `thumb` and `web`, and always JPEG for `print`, for maximum compatibility with print and PDF output.
- `IMGPROXY_MAX_SRC_RESOLUTION` is raised so that large collection photographs (≥100 MP) are accepted.
- Auto-rotation from EXIF and sRGB conversion are enabled.

`ImgproxyUrlBuilder : IImageUrlBuilder` produces `{publicBaseUrl}/{signature}/{name}/plain/s3://{bucket}/{key}`. URLs are computed when a response is built and are never stored. The public base URL is configured separately from the internal one: the browser reaches imgproxy at `localhost:8080`, while imgproxy reaches S3 at the internal `s3:8333`.

### D8. Read side: PostgreSQL views, a ReadDbContext, and a generated field catalog

- Views are created in EF migrations with `migrationBuilder.Sql(...)`, because EF does not manage view DDL. Each view's SQL lives in `Infrastructure/Read/Views/*.sql` as an embedded resource. Migrations reference it, and changing a view means writing a new migration with `CREATE OR REPLACE VIEW`.
- `ReadDbContext` is configured with `QueryTrackingBehavior.NoTracking` and maps keyless read models (`ArtefactRow` → `v_artefact`) with `ToView`. Later views (`v_loan`, `v_artefact_status`) will add navigations between read models. That is what the future query compiler will traverse.
- Read-model properties carry attributes: `[Field(Label = "Acquisition price", Type = FieldType.Money)]`, `[Filterable]`, `[Sortable]`, `[Sensitive("financial")]`. `FieldCatalogBuilder` walks the ReadDbContext's EF model together with these attributes to produce `GET /api/catalog`. Composite types (historical date, money) are described as one logical field backed by several columns.
- An integration test compares every mapped read-model column with `information_schema.columns` for its view. This is how "the catalog stays in sync" (read-model spec) is enforced.
- The artefact **list** endpoint queries `ReadDbContext`, which exercises the read side from day one. The artefact **detail** endpoint loads the aggregate and maps it, because it needs provenance and all images.

The sensitivity tag is metadata only in this change. When roles arrive, the same tag drives field removal both in the future query compiler and in response DTO serialisation, through a single `ISensitiveFieldPolicy` hook. The DTO side is not built now; only the tag is.

### D9. Frontend stack

- Vite, React 19 and TypeScript in strict mode.
- TanStack Query for server state and React Router for routing, with list state kept in URL search parameters.
- The API client is typed and generated from the OpenAPI document (`openapi-typescript` with `openapi-fetch`), regenerated by an npm script.
- Visual design and design tokens come from the **impeccable** skill: a professional, quiet, content-first look in which artefact images dominate and the interface chrome stays out of the way. The component styling approach (CSS modules with tokens, plus headless primitives for accessible menus and dialogs) is confirmed during that step. Impeccable produces `DESIGN.md`, which records the design system.
- Tests: Vitest with Testing Library.

### D10. Widget composition model

```
PageDefinition (serialisable)
  └─ Container { id, dataSource, children }
       dataSource: { kind: "endpoint", operationId, params } | { kind: "query", queryId }  (query: later)
       children:   WidgetInstance { widgetId, bindings: { slot -> path }, config }
                   | ListContainer { widgetId: "table"|"repeater", itemsPath: "provenance[]", children/columns }
```

- `defineWidget({ id, slots: { src: { type: "image", required: true } }, config: {...defaults}, print: { keepTogether, render? }, component })` registers a widget in a `WidgetRegistry` (a module-level map that is listable at runtime).
- Binding paths use a tiny dot syntax: `a.b`, with `[]` marking a list. Inside a list container, paths resolve against the current item.
- **Type checking:** a build step derives a field-type map for each OpenAPI operation from the generated schemas. The `ImageUrls` schema maps to type `image`, `HistoricalDate` to `historical-date`, `Money` to `money`, and primitives map to `text` / `number` / `date` / `boolean`. `bindResolver` checks each binding against its slot type, and a widget whose binding is invalid or missing renders a `WidgetConfigError` placeholder. Stored queries will later supply the same map from `/api/catalog`.
- Built-in pages (artefact list, artefact detail) are written as `PageDefinition` constants rendered by `PageRenderer`. Interactive parts that don't fit the binding model (forms, upload) are "action widgets" that receive an entity ID binding and own their mutation. This is the one deliberate exception to "widgets do not fetch": they *write* data but never read it themselves.
- Initial widgets: `image`, `image-gallery`, `field-list`, `table`, `repeater`, `heading`, plus the action widgets `artefact-form` and `image-manager`.

### D11. Printing with Paged.js in a dedicated preview route

- The route `/artefacts/:id/print` renders the same `PageDefinition` inside a `PrintContext`. Widgets switch to their print rendering: `print` image URLs, no interactive controls.
- After the data has loaded and images have been decoded, Paged.js (a polyfill for the CSS Paged Media standard) paginates the DOM into page boxes. That gives the on-screen preview, and `window.print()` produces the same page breaks.
- `keepTogether` widgets get `break-inside: avoid`. Before pagination, any such unit taller than the page content height is scaled down by measuring it and applying a CSS transform (print-preview spec).
- Tables use `break-inside: auto` with `tr { break-inside: avoid }`. Repeating the `thead` is done by a custom Paged.js handler, because Paged.js does not do it natively.
- Running headers and footers use the CSS features `string-set` and `@page` margin boxes with `counter(page)` / `counter(pages)`.
- Later, the batch-PDF epic points headless Chromium (Playwright) at this same route, so preview and PDF share one layout engine.

*Alternatives considered:* `react-pdf` (every widget would need a second implementation) and a plain print stylesheet (no reliable preview or page count).

### D12. Development environment

`docker/docker-compose.yml` runs `postgres:17`, `s3` (SeaweedFS in single-container `server -s3` mode, with data in a named volume and fixed credentials from an S3 identities config file), an `s3-init` one-shot (`amazon/aws-cli`) that creates the bucket, and `imgproxy`. The API and frontend run on the host (`dotnet watch`, `vite`) for a fast inner loop.

*Alternatives considered:*
- The **AWS Emulator** (the first choice while exploring). Its user guide shows HTTPS only, with a self-signed CA and credentials regenerated on every start, and all data held in memory. A restart would lose every image while PostgreSQL kept the rows, so it suits throwaway test runs, not a persistent development store.
- **LocalStack.** The free Hobby plan is non-commercial only, needs an auth token, and does not include persistence (that requires the paid Base plan).
- **Garage.** Viable, but it needs layout and key bootstrap steps after first start, and it is AGPL-licensed.
- **.NET Aspire** as the orchestrator. It can be adopted later without touching application code.

### D13. Work tracking

The repository is initialised with git and beads (`bd init`). OpenSpec `tasks.md` is the checklist for this change. Beads holds one epic per roadmap item, with dependencies between them, plus any work discovered during implementation. The same task is never tracked in both places.

## Risks / Trade-offs

- [SeaweedFS's S3 API is not 100% identical to AWS S3] → We use only basic object operations (put, get, delete, head). The S3 client is fully configurable, so the store can be swapped without code changes. An integration test runs put/get/delete against a SeaweedFS container, and a manual smoke test covers imgproxy reading from it.
- [Paged.js is lightly maintained, and repeating table headers needs a custom handler] → Pagination is isolated behind a `PrintPreview` component. Vivliostyle is the fallback engine. The batch-PDF epic can move pagination into Chromium's native print engine if needed.
- [Signed imgproxy URLs never expire, so anyone holding one can view the image] → Acceptable while there are no roles. The roles epic must check whether imgproxy supports URL expiry, or else route sensitive media through the API.
- [imgproxy has no cache, so batch printing at `print` size is CPU-heavy] → A caching-proxy epic is on the roadmap. It isn't needed for single-page preview.
- [Very large uploads go through the API] → Requests are streamed, not buffered. If proxy timeouts or limits become a problem, direct-to-S3 presigned uploads are a future option.
- [View SQL drifting from the read-model classes] → The `information_schema` comparison test runs in CI.
- [Orphaned S3 objects if a database commit fails and the compensating delete also fails] → Rare. A future maintenance task can sweep keys that have no MediaAsset.
- [The "c. ±10 years" convention may not match curatorial practice] → It is a single constant in the parser and easy to adjust. Explicit ranges always override it.

## Migration Plan

Not applicable. This is a greenfield project with no existing data or deployments. The first EF migration (`Initial`) creates the schema and `v_artefact`.

## Open Questions

- The exact ISO 4217 list source: embed a static list, or use `System.Globalization` region data. This can be decided during implementation without affecting behaviour.
- Which header and footer content should appear on non-artefact pages once more print routes exist. This is deferred to the batch-PDF epic.

# Design

## Context

See proposal.md for the motivation.

**Backend today:**
- The read side is `ReadDbContext` over `v_artefact`, plus SQL functions that resolve names in the request's language (`object_type_name`, `region_name`, `reference_entry_name`) or match them (`reference_entry_matches`, `party_name`).
- `ArtefactListQuery` implements the list's search, group-first ordering, grouping and paging.
- `FieldCatalogBuilder` builds the field catalog from the `ReadDbContext` model and the catalog attributes. It requires every read entity to be a catalogued view.
- The detail endpoint maps the write aggregate (provenance, images, descriptions) to a DTO.
- Request languages come from `RequestLanguagesMiddleware` (`lang`, then Accept-Language, then the fallback order), which runs for every request.
- Image URLs are signed per rendition by `IImageUrlBuilder`.
- The catalogue entry is a single English column, `artefacts.catalogue_entry_en`, outside the read views.

**Frontend today:**
- Page definitions name an `endpoint` data source by OpenAPI operation id.
- `scripts/generate-field-types.mjs` turns the OpenAPI response schemas into path → field type maps, and `validateBindings` checks slots against them.
- `DataSource` already reserves `{ kind: 'query', queryId }`, which does not run yet.

**Constraints:** nothing is live, so there is a single `Initial` migration that is regenerated. Warnings are errors. The REST write side stays.

## Goals / Non-Goals

**Goals:**
- One query path for built-in and user views, so a view stored as data can reproduce a built-in page.
- No N+1: names and nested lists load in batches per request.
- The list keeps exactly the current search, grouping and ordering semantics, by reusing the existing implementation.

**Non-Goals:**
- Enforcing sensitivity, authentication, or rate limiting.
- Subscriptions or real-time updates.
- Reading only the selected columns of `v_artefact` (a flat row is cheap).
- A query editor UI.
- Removing the REST read endpoints.

## Decisions

### Hot Chocolate in the Infrastructure read side, mapped by the Api
- **Packages:** `HotChocolate.AspNetCore` and `HotChocolate.Data.EntityFramework` 16.7.6 (the newest stable release, pinned in task 1.1; it targets .NET 10).
  - 16.x has no `AddOperationDocumentStorage`. The document storage is registered in the schema services (`ConfigureSchemaServices`), and the application's stored-query lookup is shared into them with `AddApplicationService`.
  - Hot Chocolate's global usings are disabled in Infrastructure (`HotChocolateImplicitUsings=disable`), because `HotChocolate.Types.ObjectType` clashes with the domain's `ObjectType`.
- **Location:** the schema types live in `Artefacts.Infrastructure/Graph`, next to `ReadDbContext`, whose row types and SQL functions are internal to Infrastructure. `AddInfrastructure` registers the GraphQL server, and `Program` maps `/graphql`.
- **No mutations:** the schema registers only a query type, so any mutation is rejected by validation.
- **Tooling:** the schema SDL is exported at build next to the OpenAPI document (`backend/openapi/schema.graphql`), so clients and reviewers can see it.

*Alternative:* a separate `Artefacts.Graph` project. Rejected because it would need Infrastructure's internals made public, or duplicated.

### Schema shape
- **Root:**
  - `artefact(id: UUID!): Artefact`
  - `artefacts(search, sort, direction, group, page = 1, pageSize = 25, where: ArtefactFilterInput, order: [ArtefactSortInput!]): ArtefactPage!`
  - `language: String!` (the reader's first language, which the detail page's fallback marker needs).
- **`ArtefactPage`:** `items`, `total`, `page`, `pageSize`, `groups { key label count }`.
- **`Artefact` fields:**
  - the scalar columns of `ArtefactRow`;
  - `creationDate: HistoricalDate`;
  - `acquisition: Acquisition` (method, date, source, lot, note, price: Money);
  - `valuation: Money`;
  - `objectType`, `collectionGroup`, `dynasty`, `storageLocation`: `NamedRef { id name }`;
  - `placeOfProduction { text region: NamedRef }`;
  - `holder: PartyRef { id name kind }`;
  - `primaryImage: ArtefactImage`, the same object as an entry of `images` (file details, caption, `urls`), read through the images loader;
  - `description`, `descriptionLanguage`, `descriptions: [Translation { language text }]`;
  - `catalogueEntry`, `catalogueEntryLanguage`, `catalogueEntries: [Translation { language text }]`, resolved like the description fields;
  - `exhibitions: [String!]!`;
  - `provenance: [ProvenanceEntry!]!`;
  - `images: [ArtefactImage!]!`.

  The names mirror the REST DTOs, so page bindings keep their paths below the root field.
- **Filtering and sorting:** `where` and `order` use Hot Chocolate's filtering and sorting conventions on `ArtefactRow`. Only properties marked `[Filterable]` or `[Sortable]` take part, read from the same attributes the catalog uses, so the two cannot disagree.
  - `where` combines with `search`.
  - `order`, when given, replaces `sort`/`direction` after the group-first ordering.

*Alternative:* a schema generated from the catalog at runtime. Rejected because it gives weaker typing and more code, and the attributes already describe it.

### The list reuses `ArtefactListQuery`
`ArtefactListQuery` is split so that the search filter, the group-first ordering and the group totals are composable `IQueryable` steps.
- The REST list and the `artefacts` resolver call the same steps.
- The resolver inserts the `where` filter after the search and before grouping, so totals and groups reflect the filter.
- The page size is capped at 100 by an argument validator. Values above it are an error, not clamped, since GraphQL clients should see the limit.

### Names and nested lists through DataLoaders
Rows come from `v_artefact` (and the new views). Linked names, parties, provenance, images, exhibitions and descriptions resolve through batch DataLoaders keyed by id, so one request issues one query per kind:
- **Names:** one loader per record kind resolves names with the existing SQL functions in the request's languages. The languages are read from the scoped `ILanguagePreferences`, so they are part of the request scope, not the key.
- **Nested lists:** loaders read the new views by `artefact_id = ANY(@ids)`, ordered by position or attachment time.
- **Image URLs:** signed in the resolver with `IImageUrlBuilder`.

A test counts the SQL commands of a 25-artefact query with images and provenance.

### New read views
`v_artefact_provenance`, `v_artefact_image` (joined to `media_assets` for the storage key, file name, format, size and upload time), `v_artefact_exhibition`, `v_artefact_description` and `v_artefact_catalogue_entry` (artefact, language, text, like the description view). Each is a keyless entity in `ReadDbContext` with catalog attributes, so the catalog and the column-sync test cover them. They are appended to the regenerated migration.

### Multilingual catalogue entry
The catalogue entry moves from `artefacts.catalogue_entry_en` to a `catalogue_entry` column on `artefact_translations`, beside `description`, so it is translated the same way.
- **Domain:** the artefact keeps an entry per language, replaced as a whole like the descriptions, with the same length limit and the same language check. `SetCatalogueEntry` becomes a replace of the per-language map.
- **REST:** the write request takes `catalogueEntries` (language to text); the detail returns that map plus `catalogueEntry` and `catalogueEntryLanguage`, resolved in the reader's languages like `description`. The `*En` names go away.
- **Import:** the catalogue-entry mapping field names a language. The header heuristic keeps matching the same headings and suggests English for them; other languages are chosen in the mapping step.
- **Frontend:** the form edits the entry per language as it does descriptions; the detail and print pages show the resolved entry with the same fallback marker as the description.
- **Migration:** the column moves in the regenerated `Initial`; nothing is live, so no data is carried over.

*Alternative:* keep the single English column and expose it only. Rejected because the collection is bilingual and the entry should follow the reader's language like every other text.

### Limits
- Hot Chocolate's execution depth rule, with a maximum depth of 8.
- Cost analysis with configured maximum field cost and type cost; list fields declare their page size for the estimate.
- Introspection stays on (there are no users to hide it from yet).
- Limits live in configuration (`GraphQL:MaxDepth`, `GraphQL:MaxFieldCost`, `GraphQL:MaxTypeCost`), read at resolve time through `IOptions`, because configuration read at startup misses test overrides.

### Sensitivity as a schema directive
A `@sensitive(tag: String!)` directive is applied to fields whose catalog property carries `[Sensitive]` (acquisition price and valuation). It is metadata only. When roles arrive, one field middleware enforces it, alongside the `ISensitiveFieldPolicy` hook planned in the foundation design.

### Stored queries: table, system files and running by identifier
- **Table:** `stored_queries(id text pk, name, normalized_name unique, document text, is_system bool, created_at, updated_at)`.
  - System ids are slugs (`artefact-list`, `artefact-detail`).
  - User ids are generated as `q-` plus a short random string, so they never clash with system slugs.
- **System queries:**
  - They are `.graphql` files embedded in Infrastructure (`Graph/Queries/artefact-list.graphql`, `artefact-detail.graphql`), each starting with a `# name:` comment.
  - At startup, after migrations, a hosted step upserts them: it inserts missing ones and overwrites the document and name of existing system rows.
  - A system query whose document no longer validates fails the startup, and a test validates every system file.
- **Running:**
  - Clients post `{ "id": "artefact-list", "variables": {...} }` to `/graphql`. A small middleware in front of the endpoint replaces the identifier by the stored document, read from the table on every request, and passes on the ordinary `{ query, variables }`. A missing identifier is answered with a "does not exist" error.
  - Ad-hoc documents stay allowed (the proposal's choice): a body with its own `query` is left alone.
  - Variable checks are GraphQL's own coercion; an error filter puts the variable's name in the message (the server reports it only in an extension).

  *Why not the persisted operation pipeline:* Hot Chocolate caches documents and compiled operations by document identifier, so an edited or deleted stored query kept running as it was (seen in a test). Resolving the identifier before the server sees it makes its caches keyed by the document's text, so they are always right.
- **`/api/queries` REST endpoints:**
  - list, get, and create with name and document;
  - update name and/or document;
  - delete;
  - `POST /{id}/duplicate` with a new name;
  - `GET /{id}/description`.

  Writes to system rows return 409 `system_query`. Saving parses the document, requires exactly one operation of type query, and validates it against the schema, including the depth rule. The cost limit is enforced when the query runs, not when it is saved (the server exposes cost analysis only as part of running a request). Errors are returned as validation problems keyed `document`, with line and column in the message. A unique-index violation on `normalized_name` maps to a `name` error, as for templates.

*Alternative:* a custom `POST /api/queries/{id}/run`. Rejected because posting the identifier to `/graphql` already gives the GraphQL response format and error shape, and the same client code serves both ad-hoc and stored queries.

### Result description computed on the server
`GET /api/queries/{id}/description` returns `{ fields: { path: type }, variables: { name: { type, required } } }`. It walks the document's selection set against the schema, following fragments and using aliases as path segments. Type rules:
- `ImageUrls` → image
- `HistoricalDate` → historical-date
- `Money` → money
- `UUID` → reference
- `String` and enums → text
- `Int`, `Float` and `Decimal` → number
- `Date` and `DateTime` → date
- `Boolean` → boolean
- lists → `list` with `[]` and recursion
- `[Translation]` → list, as REST gives the `descriptions` map; the form keeps using REST

These rules are the same as `generate-field-types.mjs`. A shared test table of cases runs in a backend test, and the frontend script's test keeps them aligned.

*Alternative:* computing the description in the frontend from the exported SDL. Rejected because it gives two implementations, and user queries live in the database, so they need a runtime source anyway.

### Frontend `query` data source
- **Shape:** `DataSource` gets `{ kind: 'query'; queryId: string; variables?: Record<string, ParamValue>; require?: string }`.
- **Running it:**
  - `RenderContainer` fetches the description (TanStack Query, long stale time, key `['query-description', id]`).
  - It resolves the variables with `resolveParams` and coerces each to the declared type: numbers parsed, booleans from `true`/`false`, empty values dropped.
  - It posts `{ id, variables }` to `/graphql` and binds against `data`.
  - GraphQL errors render like an `ApiError` with their messages.
- **Not found:** when `require` names a path that is null, the container shows the same "not found" state as a 404. The detail and print pages set `require: 'artefact'`.
- **Binding checks:** they use the description's field map instead of the generated OpenAPI map. While the description loads, the container shows its loading state, as it does for data.
- **Page definitions:**
  - List: `{ kind: 'query', queryId: 'artefact-list', variables: { search, sort, direction, group, page from search; pageSize: 25 } }`, with bindings rebased to `artefacts.items[]`, `artefacts.groups[]` and `artefacts.total`.
  - Detail and print: `artefact-detail` with `id` from the route and bindings prefixed `artefact.`; `language` comes from the root.
  - The edit page keeps `getArtefact`.

## Risks / Trade-offs

- [The open `/graphql` endpoint can be used for expensive queries before logins exist] → depth and cost limits, the page-size cap and no mutations. The deployment is local and private until roles (art-3xg).
- [Two read paths (REST and GraphQL) can drift] → the list shares `ArtefactListQuery` steps. A parity test compares REST and GraphQL for list and detail on the same data. A bead tracks retiring the REST list once nothing uses it.
- [Hot Chocolate 16 API details (persisted pipeline options, cost directives) may differ from older docs] → task 1.1 spikes a minimal schema with persisted operations and cost limits before the rest is built on it.
- [Runtime-fetched descriptions slow the first render of a page] → cached per query id. System query descriptions could be prefetched with the route later if needed.
- [Bindings rebased by hand could miss a path] → the existing list, detail and print component tests run against query fixtures, and the binding validator reports any mismatch as a configuration error, which the tests assert is absent.

- [The catalogue entry contract changes (`catalogueEntryEn` becomes `catalogueEntries`)] → every consumer is in this repository (form, pages, import, tests) and moves in this change; the generated client is regenerated.

## Migration Plan

1. Regenerate the `Initial` migration with `stored_queries`, the translation column `catalogue_entry` (replacing `catalogue_entry_en`) and the five new views appended.
2. Reset the development data (`docker compose … down -v`), start the API (system queries load at startup), and run the seed.
3. Rollback means checking out the previous commit and resetting the data, since nothing is live.

## Open Questions

- Limit values: depth 8 and the cost maxima of 5,000 hold for both system queries, the list at the page-size maximum of 100 included (`SystemQueryTests`), so they were kept. Revisit them when user queries or larger pages are common.

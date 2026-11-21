## Context

A `PageDefinition` (containers, data sources of kind `endpoint` | `query` | `static`, layouts, widgets with bindings and config, repeat nodes) is already serialisable data, rendered by `PageRenderer` and mounted by `PageRoute`. The built-in list and detail pages are TypeScript constants in `frontend/src/pages/definitions`, wired to fixed routes in `router.tsx`, and read the system stored queries `artefact-list` and `artefact-detail`. Stored queries already have the pattern this change copies: a table, a CRUD API, system rows synced from embedded files at start (`SystemQuerySync`, off for build-time generation through `GraphQL:SyncSystemQueries=false`), and read-only system rows that can be duplicated.

The widget registry (identifiers, slots, binding types, config) exists only in the frontend. Binding validation against a query's result description is also frontend code.

## Goals / Non-Goals

**Goals:**
- Store, manage and render page definitions; run the built-in list and detail pages from stored system pages.
- A JSON editor with live preview that finds widget and binding problems.

**Non-Goals:**
- Strict server-side validation of widgets and bindings (needs a shared widget-contract file; a follow-up).
- Role-based restrictions on pages or widgets (art-3xg), a query builder (art-zh1), a visual page builder.
- Moving the print, edit and new pages out of code; replacing a system page at `/artefacts`.

## Decisions

**1. Mirror the stored-query design.** A `stored_pages` table (id, slug, name, definition as `json`, `is_system`, `show_in_navigation`, `navigation_label`, timestamps), a `StoredPage` domain entity, a handler in `Application`, and `Api/PageEndpoints.cs` under `/api/pages`. System pages are rows with `is_system = true`, and changes to them return a conflict. The definition column is `json`, not `jsonb`: `jsonb` reorders object keys, and the key order of a widget's `bindings` is the order its fields are shown in (found when the detail page's fields came out in a different order). System pages are compared with the shipped text for the same reason. Alternative: a generic key-value document store. Rejected: it would not give unique slugs, names or kind without extra work, and stored queries show the dedicated table is small.

**2. System pages are embedded JSON, synced at start.** `Pages/*.json` embedded resources (`artefact-list.json`, `artefact-detail.json`) with the slug equal to the file name, upserted by a `SystemPageSync` hosted service modelled on `SystemQuerySync` and disabled by the same `GraphQL:SyncSystemQueries` switch (or an equivalent flag next to it). The JSON is the only copy; `artefactList.ts` and `artefactDetail.ts` are deleted. The section helpers in `recordSections.ts` that only these pages use are inlined into the JSON; anything the print or edit pages share stays in code. Alternative: keep the TypeScript as the source and generate the JSON at build time. Rejected: it keeps two representations and a build step, and the point is that stored data is the real thing.

**3. Structure-only validation on the server.** A `StoredPageValidator` reads the JSON and checks the shape (containers, data source kinds, layout and node types, no unknown node kinds), that each `query` data source names an existing stored query (by id or name as the data source records it), the slug rules, and the navigation label. Errors carry a JSON path (for example `containers[0].dataSource.query`). Widget identifiers, slots and binding types are not checked. Alternative: share a widget-contract file between backend and frontend like `field-type-rules.json`. Deferred: it is the right follow-up for strict validation, but the server would then reject pages the frontend renders, and the editor's preview already covers the need.

**4. The frontend loads pages through the API.** A `usePage(slug)` hook fetches `GET /api/pages/by-slug/{slug}`. `PageRoute` takes a slug (built-in routes pass `artefact-list` and `artefact-detail`; `/p/:slug` passes the route parameter). Loading and failure states are explicit: a skeleton, a not-found state for an unknown slug, and an error state with retry for a failed request. No fallback copy of the definitions is kept: the pages read their data from the same API, so a fallback could only show an empty shell.

**5. Routing and navigation.** One route `/p/:slug` renders any stored page, so custom pages cannot collide with built-in routes. Pages shown in navigation come from `GET /api/pages?navigation=true` and are listed in a sidebar section in `AppShell`. A custom page that needs a record takes it from the search parameters (`?id=`) through the existing `search` parameter values; no path templates are added.

**6. Editor in the frontend with the registry.** The page editor (`/pages`, list; `/pages/:id`, edit) has a JSON text area, name, slug and navigation fields, and a preview pane that renders the definition with `PageRenderer`. A `lintPage(definition, registry, descriptions)` function walks the definition and reports unknown widget ids, missing or mistyped binding slots and binding paths absent from the stored query's result description; the same binding validation `PageRenderer` already runs is reused so the editor and the renderer agree. The text area is a plain code field (no new dependency); a richer code editor is not needed for v1. Save sends the JSON; the server's error paths are shown next to the field. System pages open read-only with a duplicate action.

**7. Duplicate endpoint.** `POST /api/pages/{id}/duplicate` with a new name and slug, copying the definition into a user page, like duplicating a stored query.

**8. Deleting a stored query used by a page is not blocked.** The page renders the query's error state, as stored queries already do for a missing id. Blocking it would need a reference index between pages and queries, which is deferred.

## Risks / Trade-offs

- [A page can be stored that renders with errors] → the editor preview reports them with paths; the render shows the existing configuration-error box. Matches today's behaviour for built-in definitions.
- [The app cannot show the list or detail page when `/api/pages` fails] → an error state with retry; the data would be unavailable anyway.
- [An extra request before every page renders] → one small JSON fetch, cached for the session by the query client; revisit if measured to matter.
- [Moving the definitions to JSON can change the rendered pages subtly] → compare screenshots of the list and detail pages at 1440 and 390 widths before and after, as in the GraphQL change, and keep the existing frontend tests running against the loaded definitions.
- [Write widgets (`artefact-form`, `image-manager`) are available on custom pages] → they can do only what the REST endpoints already allow; restrictions belong to art-3xg.

## Migration Plan

Nothing is live, so the `stored_pages` table is added to the regenerated `Initial` migration, as was done for stored queries. System pages appear at the first start. The TypeScript definitions for list and detail are deleted in the same change. Rollback is reverting the change; no data outside `stored_pages` is touched.

## Open Questions

- Whether `/api/pages` data sources reference a stored query by id or by name inside the definition. Prefer the identifier the query description endpoint and the existing `query` data source already use, so duplicated system pages keep working; confirm against `frontend/src/widgets/core/page.ts` when implementing.

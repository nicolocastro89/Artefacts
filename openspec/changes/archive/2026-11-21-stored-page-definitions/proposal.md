## Why

Pages are already data: a `PageDefinition` (containers, data sources, layouts, widgets, bindings) is rendered by one `PageRenderer`, and the list and detail pages already read system stored queries. But every page is a TypeScript constant wired to a fixed route, so only a developer can add or change a view. Storing page definitions lets users build custom views over their own stored queries, and proves the composition model by running the built-in list and detail pages from stored data.

## What Changes

- Add stored pages: a page has a stable identifier, a slug, a name, a definition (JSON), whether it is a system page, and whether it is shown in navigation with a label.
- Add `/api/pages` to list, read, create, rename, change, duplicate and delete pages. The server checks the structure of a definition (shape, unique slug, a label when shown in navigation, and that every stored query it names exists). It does not check widgets or bindings.
- Ship the artefact list and artefact detail pages as system pages: embedded JSON files, stored at every start with the definition of the running release. System pages cannot be changed or deleted; any page can be duplicated into a user page.
- Add the route `/p/:slug` that loads a stored page and renders it. Pages shown in navigation appear in a sidebar section.
- The routes `/artefacts` and `/artefacts/:id` load the system pages `artefact-list` and `artefact-detail` from the API. **BREAKING** (internal): the TypeScript constants `artefactList.ts` and `artefactDetail.ts` are deleted, and the section helpers in `recordSections.ts` move into the shipped JSON where only those pages use them. Print, edit and new pages stay in code.
- Add a page editor in the UI: a JSON editor with a live preview that reports widget and binding problems using the widget registry and the stored query descriptions.
- Every registered widget may be used on a stored page. No role-based restriction is added.

## Capabilities

### New Capabilities
- `stored-pages`: storing, managing, validating (structure only) and loading page definitions; system pages synced from the release; the `/p/:slug` route and navigation entries; the page editor with preview.

### Modified Capabilities
- `widget-composition`: built-in list and detail pages are rendered from stored system pages rather than TypeScript constants, and a stored page must render the same as the built-in one it reproduces.

## Impact

- Backend: new `stored_pages` table (added to the regenerated `Initial` migration), `Domain`/`Application`/`Infrastructure` for pages, `Api/PageEndpoints.cs`, embedded system page JSON, a start-up sync like `SystemQuerySync`, OpenAPI regeneration.
- Frontend: `PageRoute` and `router.tsx` load pages from the API; new page editor and a navigation section in `AppShell`; `frontend/src/pages/definitions/artefactList.ts` and `artefactDetail.ts` removed; the generated API types updated.
- Not in scope: the query builder UI (art-zh1), role-based enforcement (art-3xg), strict server-side widget and binding validation (would need a shared widget-contract file), a visual page builder, and replacing a system page at its built-in route.

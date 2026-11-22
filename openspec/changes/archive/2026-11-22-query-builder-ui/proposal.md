## Why

Stored queries are the data layer that pages read, but the only way to make one is to write GraphQL by hand and POST it to `/api/queries`. People who build pages (stored pages are now editable) cannot get the data they want without knowing the schema and the query language. A query builder lets them assemble a query from building blocks (fields, filters, ordering, grouping, paging, variables), see what it returns, and keep the text as an optional view.

## What Changes

- Add a query editor with two views of one stored document: a Blocks view and a Text view. The GraphQL document stays the only stored form; blocks read and write it through its syntax tree, parsed and printed in the frontend.
- Blocks cover both roots of the schema (`artefacts` list and single `artefact`), the full selection tree including nested objects and lists, filters with and/or groups and an operator per field type, multi-field ordering, grouping, page size, and variables as filter and argument values.
- A document the blocks cannot represent (aliases, fragments, directives, unknown shapes) opens in the Text view with the reason shown; blocks are disabled and never rewrite it. Printing from blocks warns first when the document has comments, which printing drops.
- Add a Run panel: typed inputs for the query's variables, runs the unsaved document against real data, shows the result and the result paths with their types, and shows server errors with line and column.
- Add a query list and routes `/queries`, `/queries/new`, `/queries/:id` with a "Queries" navigation item, following the Pages pattern: system queries read-only with a duplicate action, user queries editable and deletable.
- Backend: publish the builder rules (`GET /api/queries/builder-schema`), derived from the live GraphQL schema and the field catalog, so the frontend offers only what the server accepts; and describe an unsaved document (`POST /api/queries/description`), returning its description or its validation problems.
- Fix stale wording in the `graphql-queries` listing requirement, which still compares results with the removed REST list.

## Capabilities

### New Capabilities
- `query-builder`: the query editor with its Blocks and Text views, the block model and which documents it can represent, the Run panel, and the query list and navigation.

### Modified Capabilities
- `stored-queries`: adds the builder rules and the description of an unsaved document.
- `graphql-queries`: the listing requirement compares results with the `artefact-list` system query instead of the removed REST list.

## Impact

- Backend: two read endpoints in `QueryEndpoints.cs` and a builder-schema builder next to `StoredQueryDescriber` (it reuses the schema index built from the schema SDL); tests; regenerated OpenAPI document and frontend API types.
- Frontend: new `graphql` dependency (bundle impact to be measured first), a query model and mapping module, block components, query list and editor pages, a Run panel, a navigation item.
- Not in scope: role-based enforcement (art-3xg; preview shows financial fields until then, as every other read does), result tables with sorting or export, roots other than `artefacts` and `artefact` (the schema has none), and query changes beyond what the existing save endpoint already validates.

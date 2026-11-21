# Tasks

## 1. Foundation

- [x] 1.1 Spike: add the newest stable Hot Chocolate 16.x (`HotChocolate.AspNetCore`, `HotChocolate.Data.EntityFramework`), map `/graphql` with a query-only schema, a depth rule, cost limits and the persisted operation pipeline backed by an in-memory document store that also accepts ad-hoc documents; verify with an integration test that an ad-hoc query, a persisted query by id, a mutation (rejected) and a too-deep query (rejected naming the limit) behave as specified, and record the pinned version and option names in the design if they differ
- [ ] 1.2 Add the read views `v_artefact_provenance`, `v_artefact_image`, `v_artefact_exhibition` and `v_artefact_description` with keyless entities and catalog attributes; verify that the column-sync test and a catalog test listing the four new entities pass
- [ ] 1.3 Split `ArtefactListQuery` into composable search, group-ordering, grouping and paging steps used by the REST list; verify that the existing list, search and grouping tests pass unchanged

## 2. Schema

- [ ] 2.1 `Artefact` type over `ArtefactRow` with historical dates, money, acquisition, `NamedRef`, `PartyRef`, place of production, signed `primaryImage`, and the root fields `artefact(id)` and `language`; verify with integration tests for a single artefact, an unknown id (null, no error), names in English and Italian with fallback, and a held-by-the-collection null holder
- [ ] 2.2 Batch DataLoaders for linked names (object types, regions, list entries, parties) and for provenance, images, exhibitions and descriptions, with image URLs signed in the resolver; verify the nested-list scenarios and a test that counts the SQL commands of a 25-artefact query with images and provenance and finds that it does not grow with the number of artefacts
- [ ] 2.3 `artefacts(...)` root field reusing the list steps, with `where`/`order` limited to the catalog's filterable and sortable fields, `where` applied before grouping, and the page size capped at 100 as an error; verify with a parity test against the REST list (search, group, sort, page), a filter on method and image count, an error for filtering on the primary image, and an error for a page size of 500
- [ ] 2.4 `@sensitive` directive on the fields whose catalog property is sensitive, and export of the schema SDL at build to `backend/openapi/schema.graphql`; verify with a test reading the published schema for the tag on the acquisition price and valuation, and that the SDL file is produced
- [ ] 2.5 Configure the limits (`GraphQL:MaxDepth`, `GraphQL:MaxFieldCost`, `GraphQL:MaxTypeCost`) through `IOptions` and tune them against the system queries; verify that the system queries run within them and that a query over the limits is rejected before any SQL runs

## 3. Stored queries

- [ ] 3.1 `stored_queries` table, entity and repository, with the unique normalised name mapped to a `name` validation error; regenerate the `Initial` migration with the table and the new views appended; verify that a fresh database migrates and that the integration tests start
- [ ] 3.2 Embedded system query files `artefact-list.graphql` and `artefact-detail.graphql`, and a startup step that upserts them and fails on an invalid document; verify with tests that every system file validates, that a changed document replaces the stored one at the next start, and that both are listed as system queries
- [ ] 3.3 Replace the spike's document store with one reading `stored_queries`, so `{ id, variables }` runs a stored query; verify the "run by identifier" and "wrong variable type" scenarios and that running a deleted query reports that it does not exist
- [ ] 3.4 `/api/queries` endpoints (list, get, create, update, delete, duplicate) with document validation (one query operation, schema, limits; errors keyed `document` with line and column) and 409 `system_query` for system rows; verify with integration tests covering every scenario of stored queries and user query management
- [ ] 3.5 `GET /api/queries/{id}/description` computing the field map and variables from the document and the schema (fragments, aliases, the shared type table); verify with a test on `artefact-list` (`artefacts.items[].primaryImage` image, `artefacts.items[].title` text, `artefacts.groups[]` list, variable `page` number) and a table test of the type rules shared with the frontend script
- [ ] 3.6 Regenerate the OpenAPI document and the frontend client; verify that `npm run build` typechecks

## 4. Frontend

- [ ] 4.1 `query` data source in the page model and renderer: description fetch and cache, variable coercion, POST to `/graphql` by id, GraphQL errors as error states, `require` path for not found, and binding validation against the description; verify with core tests for coercion from the URL, a binding to a path the query does not return (configuration error naming the path), a GraphQL error, and `require` showing the not-found state
- [ ] 4.2 Move the artefact list page to `artefact-list` with rebased bindings; verify that the list tests (table, grid, search, grouping, paging and the URL round trip) pass against query fixtures with no configuration errors
- [ ] 4.3 Move the detail and print pages to `artefact-detail` with `require: 'artefact'`; verify that the detail, catalogue-field and print tests pass against query fixtures with no configuration errors, and that an unknown id shows the not-found state

## 5. Acceptance

- [ ] 5.1 On a reset and seeded development database, open the list (grouped by dynasty, searched, paged), a detail page and its print preview in the running app; verify with headless captures at 1440 and 390 that they match the REST-backed pages captured from the previous commit, and that the browser's network log shows only `/graphql` reads for them
- [ ] 5.2 Create a bead for retiring the REST list endpoint once nothing uses it, and verify that `bd list` shows it
- [ ] 5.3 Run `dotnet test`, `npm test`, `npm run build`, `npm run lint` and `openspec validate graphql-query-layer --strict`; verify that all pass

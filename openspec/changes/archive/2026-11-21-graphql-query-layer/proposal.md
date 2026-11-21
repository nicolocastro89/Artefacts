# Proposal

## Why

Custom views are meant to be built from the same parts as the built-in ones, and stored as data. Today the built-in pages read from REST endpoints written for them (`listArtefacts`, `getArtefact`), so a user-built view could never reproduce them, and every new view would need new backend code. A query layer over the read views, which the built-in views use too, means a view chooses the data it needs without backend work. It has to come before stored page definitions (art-2cc), because the definitions refer to their data source.

## What Changes

- **GraphQL over the read views:**
  - A read-only GraphQL endpoint, `/graphql`, served from the Postgres read views.
  - It exposes artefacts with:
    - their fields, filtering and sorting on the catalog's filterable and sortable fields;
    - the free-text search and grouping the list already offers, plus paging;
    - the names of linked records in the reader's language;
    - signed image URLs;
    - the nested provenance entries, images, exhibitions and descriptions.
  - Linked names and nested lists are fetched in batches for a whole page, not once per artefact.
  - Depth, cost and page-size limits apply.
  - Writes stay on REST.
- **New read views** for provenance entries, images, exhibitions and descriptions, listed in the field catalog.
- **Stored queries:**
  - A stored query is a named GraphQL document, kept in the database.
  - **System queries** ship with the application: defined in the repository and loaded at startup. They are read-only, and a user can duplicate one to change it.
  - **User queries** are created, renamed, edited and deleted through `/api/queries`. Each document is validated against the schema when saved.
  - Any stored query runs by its identifier with variables, and the API describes the type of every value its result contains, so bindings can be checked.
- **Query data sources:** a container's data source can be a stored query, with variables taken from literals, the route or the URL. The artefact list, detail and print pages switch to system queries. The edit page keeps reading the REST detail, because its form writes through REST.
- **Multilingual catalogue entry:**
  - The English-only `catalogue_entry_en` column is replaced by a catalogue entry per language, stored with the translations beside the description. This reverses the single-English-entry decision of `extend-catalogue-fields`.
  - REST takes a map of entries and returns the map plus the entry resolved in the reader's language; the import maps a column to a language; the form, detail and print pages follow.
  - GraphQL exposes it like the description: the resolved entry, its language, and every entry.
- **Sensitivity marked in the schema:** fields tagged `financial` in the catalog carry the tag in the GraphQL schema too. As before, the tag is not enforced until roles exist.

## Capabilities

### New Capabilities
- `graphql-queries`: the read-only GraphQL schema and endpoint over the read views, covering fields, nested lists, names, image URLs, filtering, sorting, search, grouping, paging, language and limits.
- `stored-queries`: named GraphQL documents, both system and user, validated when saved, run by identifier, and described by the types of their results.

### Modified Capabilities
- `artefact-catalogue`: the catalogue entry is kept per language instead of in English only.
- `artefact-browsing`: the detail and print pages show the catalogue entry in the reader's language, and the form edits it per language.
- `artefact-import`: a catalogue-entry column is mapped to a language.
- `read-model`: adds the provenance, image, exhibition and description read views, which the field catalog describes.
- `widget-composition`: containers can read from a stored query, and bindings are checked against the query's result types. Built-in read-only pages use system queries.

## Impact

- **Backend:**
  - Hot Chocolate (newest stable 16.x) with its EF Core data integration in a new GraphQL area of the Api project.
  - Types and resolvers over `ReadDbContext`, and DataLoaders for the nested lists.
  - A `stored_queries` table and repository, a loader for system query files, a middleware that runs a stored query by its identifier, the `/api/queries` endpoints, and a result-type describer.
  - New views in `Read/Views`, appended to a regenerated `Initial` migration.
- **Frontend:**
  - A `query` data source: posts to `/graphql` by identifier and coerces variables.
  - Binding checks use the stored query's result types, fetched at runtime.
  - List, detail and print page definitions move to system queries, with bindings rebased on the query results.
- **Catalogue entry:** domain and write model (per-language entry on the translations), the artefact REST contract (`catalogueEntries` in, map and resolved entry out), the import mapping and its suggestion, and the frontend form, detail and print pages.
- **Unchanged:** REST read endpoints stay (the edit form, the import, the seed and the tests use them). Retiring the REST list endpoint is a follow-up.
- **Data:** the migration is regenerated and the development data is reset, since nothing is live, so the catalogue entry needs no data move.
- **Not included:** a query builder UI (art-zh1), stored page definitions (art-2cc), and enforcing sensitivity by role (art-3xg).

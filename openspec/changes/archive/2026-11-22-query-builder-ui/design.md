## Context

Stored queries have a table, a CRUD API with validation (line and column), a description endpoint for stored queries, system queries synced from embedded files, and a middleware that runs a stored document by id. Ad-hoc documents run through `/graphql` with the same depth and cost limits. The schema has two roots: `artefacts(search, sort, direction, group, page, pageSize, where, order)` and `artefact(id)`. Filters and ordering use flat read-view column names (`acquisitionMethod`, `creationDateEarliestYear`), while selections use nested objects. The page editor reads queries by id and checks bindings against their descriptions. There is no UI for queries.

`StoredQueryDescriber` already builds a `SchemaIndex` from the schema's SDL (objects, input types, enums). Both system queries use only plain selections, nested objects and lists, variables, and a `# name:` comment; no aliases, fragments or directives.

## Goals / Non-Goals

**Goals:**
- Build, read and edit stored queries from blocks, with text as a second view of the same document.
- Offer only what the server accepts, and keep that in step with the schema automatically.
- Try an unsaved query against real data.

**Non-Goals:**
- Role-based restrictions (art-3xg): preview shows financial fields, as every other read does today.
- Result tables with sorting or export; roots beyond `artefacts` and `artefact`; saving anything other than the document.

## Decisions

**1. The document is the only stored form; blocks are a view over its syntax tree.** No change to the `stored_queries` table, the save endpoint, the middleware, the description endpoint or how pages read queries. Alternatives: a block model stored beside the document (system and hand-written queries could not open in blocks, and the two forms could drift) and two separate modes (rejected: blocks would not be a way to edit existing queries).

**2. Parse and print in the frontend with the `graphql` package, and the backend publishes the rules.** Edits are local and instant, with no round trip per change. To avoid two copies of the schema's rules, `GET /api/queries/builder-schema` is derived from the live schema, as the description endpoint is. A test compares it with the published schema. The server still validates every save and run. Alternative: parse and print on the backend (rejected: a call for every block change). Open check before committing: the bundle cost of the `graphql` package, measured by building with and without it; if it is large, import only `parse` and `print`, or lazy-load the editor route.

**3. Builder rules from the SDL, not from the field catalog alone.** The filter input types in the schema are the authority for what can be filtered and ordered, so the rules read `ArtefactFilterInput`, `ArtefactSortInput` and each field's `*OperationFilterInput` for operators. Labels come from the field catalog where a filter field belongs to a catalog field (the catalog maps fields to read-view columns) and are otherwise derived from the name. Sensitivity comes from the `@sensitive` directive. Limits come from `GraphOptions` and the page size maximum. The rules are a new `BuilderRulesBuilder` next to `StoredQueryDescriber`, reusing `SchemaIndex` (extended to expose fields, input fields and enum values).

**4. A block model between the syntax tree and the UI.** `QueryModel`: root (`artefacts` | `artefact`), selection tree, filter tree (groups of `and`/`or` holding conditions of field, operator, value), order list, search, grouping, page, page size, root arguments (`id`), and declared variables. `toModel(document)` returns either a model or the list of reasons it cannot (aliases, fragments, directives, more than one operation, a mutation, unknown fields or arguments, filter shapes the model has no block for, variables used in places blocks cannot express). `toDocument(model)` prints the document. A document that does not convert is edited as text, and the Blocks view says why. Round-trip tests run both system queries and a set of built documents through `toModel` then `toDocument`.

**5. Variables take the type of the place they are used.** A variable block declares a name; using it sets its type from the argument or operator (`search: String`, `page: Int`, `eq: String`). Using it in two places with different types is refused. The Run panel reads the types from the description of the unsaved document.

**6. Describe an unsaved document.** `POST /api/queries/description` takes a document and returns the existing description, or the validation problems with line and column, reusing `StoredQueryValidator` and `StoredQueryDescriber`. The Run panel and the editor's problem list use it. Running itself posts the document to `/graphql` (ad-hoc documents already run with the same limits), so no run endpoint is added.

**7. The Text view is a plain text area**, as in the page editor, with server errors shown beside it by line and column; no code-editor dependency. Switching views parses or prints; text is never rewritten by the editor. Printing from a block change warns first if the parsed document has comments (the `# name:` line is not stored in user documents, but users may add comments).

**8. Layout and navigation follow the Pages pattern**: `/queries` list, `/queries/new`, `/queries/:id`, a "Queries" navigation item, system queries read-only with a duplicate action, `message()` and `mapFieldErrors` for server errors. Blocks UI uses native form controls and the existing design primitives.

## Risks / Trade-offs

- [Bundle size of the `graphql` package] → measure first; import only `parse` and `print`; lazy-load the editor route.
- [Printing drops comments and normalises formatting] → warn before a block change rewrites a document with comments; text edits are never rewritten.
- [The rules drift from the schema] → derived from the schema at request time; a test compares them with the published schema.
- [Blocks do not cover everything GraphQL can express] → unsupported documents open as text with the reason; blocks never rewrite them.
- [Preview shows financial fields without a login] → the same as every other read; named in the spec and left to art-3xg.
- [Large selection trees are hard to use] → the tree starts collapsed and shows the count of selected fields per branch.

## Migration Plan

No data migration: the document is unchanged and nothing is added to the database. New endpoints and a new frontend route only. Rollback is reverting the change.

## Open Questions

- Which operators to show for each operator input type: all of the server's (including `nin`, `ngte`, `nlt`) or a plain-language subset (equals, not equals, greater than, ...). Start with a plain-language subset that maps to the server's operators, and show the rest only when a document already uses them.
- Whether filter values for list operators (`in`, `nin`) are edited as a comma-separated list or as chips.

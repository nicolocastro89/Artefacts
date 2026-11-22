## 1. Backend: builder rules and describing an unsaved document

- [x] 1.1 Extend `SchemaIndex` to expose object fields (type, list, nullability, directives), input fields and enum values, without changing what the describer returns
- [x] 1.2 Add `BuilderRulesBuilder` producing the builder rules from the schema SDL: roots and arguments, fields per type, filterable and sortable fields with their operators, enums, sensitivity tags, and limits from `GraphOptions`; labels from the field catalog where it has them
- [x] 1.3 Add `GET /api/queries/builder-schema`, `POST /api/queries/description`, and their DTOs in `QueryEndpoints.cs`; regenerate the OpenAPI document and frontend API types
- [x] 1.4 Integration tests: filterable and non-filterable fields with operators by type, published limits equal the enforced ones, the rules agree with the published schema, describe a valid document, describe an invalid one with line and column, nothing is stored

## 2. Frontend: parsing and the block model

- [x] 2.1 Measure the bundle cost of the `graphql` package (build with and without it), add it importing only `parse` and `print`, and lazy-load the editor routes if the cost is large
- [x] 2.2 Add the `QueryModel` types and `toModel(document)` returning the model or the reasons the document cannot be shown in blocks
- [x] 2.3 Add `toDocument(model)` and the variable rules (a variable takes the type of its place, conflicting types and removal of a variable in use are refused)
- [x] 2.4 Round-trip tests: both system queries, a document with each block kind, and documents with an alias, a fragment, a directive and a comment

## 3. Frontend: editor and run panel

- [x] 3.1 Add the query list at `/queries` and the "Queries" navigation item, with duplicate and delete, following the Pages page
- [x] 3.2 Add the editor at `/queries/new` and `/queries/:id` with the Blocks and Text views, read-only system queries with a duplicate action, and server errors with line and column
- [x] 3.3 Add the selection tree block, marking sensitive fields and the root choice (list or single artefact with its identifier)
- [x] 3.4 Add the filter, order, grouping, search and paging blocks, offering only the operators and fields the rules allow and the page size maximum
- [x] 3.5 Add the variable blocks and the comment warning before a block change rewrites a document
- [x] 3.6 Add the Run panel: typed variable inputs from the description of the unsaved document, run through `/graphql` in the reader's language, the result, the result paths with types, and errors with line and column
- [x] 3.7 Component tests: open `artefact-detail` and `artefact-list` copies in blocks, build a filter and see it in the text, text the blocks cannot show disables blocks with the reason, comment warning, variable refusal, run with a variable, error position

## 4. Acceptance

- [x] 4.1 Duplicate `artefact-list`, change a filter and the ordering in blocks, run it, and use the copy from a stored page
- [x] 4.2 Build a new query from blocks only (nested fields, an and/or filter with a variable, two orderings, grouping) and run it against seeded data
- [x] 4.3 Run backend and frontend tests, lint and build; `openspec validate query-builder-ui --strict`

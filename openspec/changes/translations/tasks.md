# Tasks

## 1. Domain

- [x] 1.1 Add the `Language` aggregate (code normalised to lower case, validated as `xx` or `xx-yy`, name, position) and verify with unit tests (code validation, normalisation)
- [x] 1.2 Add a per-language name collection to `Region` and `ObjectType` (replace all names; trim; blank entries dropped; at least one name required; normalised name per entry) replacing the single `Name`/`NormalizedName`; verify with unit tests (blank dropped, none left rejected, normalisation)
- [x] 1.3 Add per-language descriptions to `Artefact` (owned translation rows, replace all, blank dropped, length limit per description) replacing the single `Description`; verify with unit tests
- [x] 1.4 Add `Localized.Resolve(values, preferences)` returning the text and its language from the first preference that has a value; verify with unit tests (requested present, fallback, nothing)

## 2. Persistence

- [x] 2.1 Map `languages`, `region_names`, `object_type_names` and `artefact_translations` (composite keys, FK to `languages.code` with restrict, unique (language, normalized_name) for names) and seed `en`(1) and `it`(2) in the model; verify the generated DDL
- [x] 2.2 Add the SQL functions `region_name(uuid, text[])` and `object_type_name(uuid, text[])`, map them as EF database functions in both contexts, and update `Read/Views/v_artefact.sql` to drop the joined name columns (and `ArtefactRow` to match); verify the view-columns integration test passes
- [x] 2.3 Delete the migrations and regenerate `Initial` (append the expression indexes, the functions and the view); reset the development database and verify the API starts and migrates it
- [x] 2.4 Repositories: language list and usage check; region and object type lookup by normalised name within a language; search across all languages; translate FK violations on language removal and unique violations per language into domain errors; verify with integration tests

## 3. Application and API

- [x] 3.1 `RequestLanguages`: build the preference list from `lang` (comma list) or `Accept-Language` (q order), map regional codes to primary codes, drop unknown, append fallback order; cache the language list and refresh on change; add `Vary: Accept-Language`; verify with unit tests for both scenarios in the spec and an integration test that `lang` wins over the header
- [x] 3.2 Language endpoints (`GET`, `POST`, `PUT /order`, `DELETE` with 409 when in use); verify with integration tests (listed in order, add goes last, reorder changes fallback, removal in use rejected)
- [x] 3.3 Region and object type handlers and DTOs with per-language names (add with `names`, add-existing match within a language, replace names with collision check per language, unknown language and no-name errors, search across languages, ordered by resolved name); update the existing region and object type integration tests and add the cross-language search and "same text in different languages" cases
- [x] 3.4 Artefact write and detail with `descriptions` (unknown language rejected on `descriptions.xx`), detail with `language`, `description`, `descriptionLanguage`, `descriptions`, and provenance, place and object type names resolved in the preference order; verify integration tests for both description scenarios and an Italian-named provenance region
- [x] 3.5 List query: object type and region names through the SQL functions in items, group labels and group ordering; verify integration tests that grouping by object type in Italian labels and orders by Italian names, and that counts still span pages
- [x] 3.6 Regenerate the OpenAPI document and verify it describes the language endpoints and the `names`/`descriptions` maps

## 4. Frontend

- [x] 4.1 Regenerate the API client; add a languages query and a `useReaderLanguage()` hook (first of `navigator.languages` that is a content language, else first in fallback order); verify `npm run build` typechecks
- [x] 4.2 Reference lists: Regions and Object types tables with one editable name column per language and per-language add inputs; a Languages tab (order with move up and down, add, delete with in-use error); verify with component tests mirroring the spec scenarios
- [x] 4.3 Pickers search across languages and create inline names in the reader's language; verify with a component test that the POST body carries the name under the reader's language
- [x] 4.4 Form: one description field per language in fallback order, sent as `descriptions`; verify with a form test (English filled, Italian empty → only `en` sent)
- [x] 4.5 Detail and print: the resolved description with an "In <language>" marker when it differs from the request language; verify with a detail test and a headless print check
- [x] 4.6 Follow DESIGN.md for the per-language columns and the marker (condensed labels, no new colours); verify headless captures of the reference lists, form and detail at 1440 and 390 with no overflow

## 5. Seed, import plan and verification

- [x] 5.1 Seed English and Italian names for regions and object types, and descriptions in English, in Italian only, and in both; reset the development data and verify the seed runs and that listing in `lang=it` and `lang=en` shows the expected names and fallbacks
- [x] 5.2 Update the `import-artefacts` change so place and object type matching uses names in every language (spec "Place of production matching", design "Places") and verify `openspec validate import-artefacts --strict`
- [x] 5.3 Run `dotnet test`, `npm test`, `npm run build`, `npm run lint` and `openspec validate translations --strict`; verify all pass

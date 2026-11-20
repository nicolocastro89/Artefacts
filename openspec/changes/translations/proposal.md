# Proposal

## Why

The collection is described in Italian and English: the same region is "Turchia" in the workbooks and "Turkey" for an English reader, and the same holds for object types ("Tappeti" / "Carpets") and for descriptions. Today every name and description exists in one language only, so staff either pick one language and lose the other, or duplicate records. The import (`import-artefacts`) also needs it: workbooks name places and object types in Italian, and matching them against regions that are named in English is guesswork. Storing each value per language fixes both.

## What Changes

- **Languages are data.** A managed list of content languages (initially Italian and English), each with a code, a name and a position in a fallback order. Adding a language is adding a row, plus its translations: no code change.
- **Translatable values are stored per language**, with no "original" language. First scope:
  - region names;
  - object type names;
  - the artefact description.
  Other fields can follow the same pattern later.
- **Reading in a language:**
  - Every read endpoint accepts a `lang` parameter. When it is missing, the browser's preferred languages (`Accept-Language`) are used.
  - Each translatable field resolves to the first language that has a value: the requested languages first, then the configured fallback order.
  - The response says which language each resolved value is in, so a fallback is visible rather than silent.
- **Writing all languages at once.** Create and update requests carry every language's value for a translatable field together (for example `names: { it: "Turchia", en: "Turkey" }`), and responses used for editing return them all.
- **Search covers all languages; ordering uses one.** Searching regions or object types matches a name in any language. Sorting and the labels of list groups use the resolved language.
- **Uniqueness per language.** No two regions may share a name in the same language, and the same for object types. "Add" returns an existing entry when the name matches in that language.
- **Frontend:**
  - the region and object type lists show and edit one name per language;
  - the artefact form edits the description per language;
  - the detail page and print record show the resolved description and mark it when it is a fallback.
  The interface itself stays in English; translating the UI is a later change.

## Capabilities

### New Capabilities

- `content-languages`: the managed list of content languages, the fallback order, how the language of a request is determined, and how a translatable value resolves to one language.

### Modified Capabilities

- `region-reference`: regions have a name per language; search matches any language; uniqueness and "add existing" work per language.
- `object-types`: the same for object type names.
- `artefact-catalogue`: the description is stored per language; artefact reads resolve it, and the object type and region names they include, in the requested language.
- `artefact-browsing`: editing names and descriptions per language; showing the resolved language when it differs from the requested one; managing content languages.
- `read-model`: the artefact read view keeps identifiers for translated references (object types, regions); their names are resolved per request.

## Impact

- **Backend:**
  - a `Language` aggregate and table;
  - per-entity translation tables: `region_names`, `object_type_names`, and `artefact_translations` holding the description;
  - PostgreSQL functions that resolve a name in a preferred-language order, used by the list query and grouping;
  - request language resolution: query parameter, then `Accept-Language`, then fallback order;
  - DTOs and endpoints that take and return a value per language.
  - The single `Initial` migration is regenerated, and development data is reset and reseeded.
- **Frontend:** the reference lists edit one name per language, there is a Languages tab, the form has a description per language, the detail and print pages show the description's language, and the region and object type pickers send names in a chosen language.
- **Import (`import-artefacts`):** place and object type matching uses region and object type names in every language. That change's specs and design are updated to say so.
- **Seed:** regions and object types get Italian and English names; a few descriptions get both languages, and some only one, so that fallback shows.

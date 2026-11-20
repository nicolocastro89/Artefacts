# Proposal

## Why

The collection's existing records (seven workbooks, about 730 objects: ceramics, manuscripts, metalwork, miniatures, parchments, carpets, textiles) carry information the catalogue cannot hold today: what kind of object it is, the collection group it is filed under, where and under which dynasty it was made, the exhibitions it has been shown in, the English catalogue entry, who holds it and where it is stored, its current valuation, and acquisition details such as the auction lot. Prices also appear in withdrawn currencies (Italian lire). Staff browse the collection by these groupings, by type, origin and period, so the list must be able to group by them as well. The import (`import-artefacts`) depends on this change: it can only map into fields that exist.

## What Changes

- New catalogue fields on an artefact, all optional, all kept exactly as written:
  - **object type**, chosen from a managed reference list (Ceramics, Carpets, Textiles, …) that staff can add to, rename and delete when unused;
  - **collection group**, free text (the sheet's section heading, e.g. "CINA", "LAMPASSI");
  - **place of production**, the text as written ("Anatolia o Asia Centrale?") with an optional link to a region from the region reference list;
  - **dynasty or period name**, free text ("Ottomana", "Sung");
  - **exhibitions**, an ordered list of entries;
  - **catalogue entry (English)**, long text;
  - **holder** and **storage location**, free text;
  - **valuation**, an optional monetary value, classified as financially sensitive.
- Acquisition gains a **lot** and a free-text **note**, and prices accept withdrawn ISO 4217 currency codes (for example ITL, DEM, FRF) as well as active ones.
- Search also matches the collection group, place of production, dynasty, holder and storage location.
- The artefact list can **group** its results by object type, collection group, place of production (region), century (from the creation date), or dynasty. Each group shows a heading with its record count; grouping is part of the shareable URL.
- The detail page, the edit form and the print record show the new fields; the read view and field catalog expose them.

## Capabilities

### New Capabilities

- `object-types`: a managed reference list of object types (list, search, add, rename, delete when unused), mirroring the region reference list.

### Modified Capabilities

- `artefact-catalogue`: creation and update accept the new fields; acquisition gains lot and note and accepts withdrawn currency codes; listing supports grouping and wider search.
- `artefact-browsing`: list grouping, new fields on the detail page and in the form, object type management.
- `read-model`: the artefact read view and field catalog expose the new fields; valuation is tagged `financial`.
- `region-reference`: a region linked as an artefact's place of production counts as in use and cannot be deleted.

## Impact

- Backend: the Artefact aggregate and Acquisition value object, a new ObjectType aggregate, EF Core migration (new columns, an `exhibitions` child table, an `object_types` table), the `v_artefact` view, list query (grouping, search), currency validation, DTOs and endpoints; the generated OpenAPI document and frontend client.
- Frontend: list grouping control and grouped rendering, detail and print page definitions, form sections, an object types management page (or a second tab on the reference-data page).

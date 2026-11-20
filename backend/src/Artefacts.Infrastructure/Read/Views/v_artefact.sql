-- Flattened artefact read view. Columns must match ArtefactRow (verified by an integration test).
-- Names that exist per language (object types, regions) are not columns: queries resolve them with
-- region_name / object_type_name (localized_names.sql) in the reader's language.
-- Group keys: normalised text (lower case, whitespace collapsed) for free-text groupings, and the century
-- of the creation date (17 = 1601-1700; negative for BCE, -1 = 100-1 BCE).
CREATE OR REPLACE VIEW v_artefact AS
SELECT
    a.id,
    a.inventory_number,
    a.title,
    a.maker,
    a.creation_date_display,
    a.creation_date_earliest_year,
    a.creation_date_latest_year,
    a.creation_date_precision,
    a.medium,
    a.dimensions,
    a.object_type_id,
    a.collection_group,
    nullif(lower(regexp_replace(btrim(a.collection_group), '\s+', ' ', 'g')), '') AS collection_group_key,
    a.place_text,
    a.place_region_id,
    a.dynasty,
    nullif(lower(regexp_replace(btrim(a.dynasty), '\s+', ' ', 'g')), '') AS dynasty_key,
    CASE
        WHEN y.year IS NULL THEN NULL
        WHEN y.year > 0 THEN (y.year - 1) / 100 + 1
        WHEN y.year = 0 THEN -1
        ELSE -((-y.year - 1) / 100 + 1)
    END AS century,
    a.holder,
    a.storage_location,
    a.acquisition_method,
    a.acquisition_date,
    a.acquisition_source,
    a.acquisition_lot,
    a.acquisition_note,
    a.acquisition_price_amount,
    a.acquisition_price_currency,
    a.valuation_amount,
    a.valuation_currency,
    (SELECT count(*) FROM artefact_exhibitions ae WHERE ae.artefact_id = a.id)::int AS exhibition_count,
    pm.storage_key AS primary_image_key,
    (SELECT count(*) FROM artefact_images ai WHERE ai.artefact_id = a.id)::int AS image_count
FROM artefacts a
CROSS JOIN LATERAL (SELECT coalesce(a.creation_date_earliest_year, a.creation_date_latest_year) AS year) y
LEFT JOIN artefact_images pi ON pi.artefact_id = a.id AND pi.is_primary
LEFT JOIN media_assets pm ON pm.id = pi.media_asset_id;

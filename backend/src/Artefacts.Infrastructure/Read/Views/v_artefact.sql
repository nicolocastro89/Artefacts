-- Flattened artefact read view. Columns must match ArtefactRow (verified by an integration test).
-- Names of linked records (object types, regions, list entries, parties) are not columns: queries resolve
-- them with region_name / object_type_name / reference_entry_name (localized_names.sql) in the reader's
-- language, or join parties for their single name.
-- Measurements keep the value and unit as entered; *_metres uses the unit's current factor.
-- Group key: the century of the creation date (17 = 1601-1700; negative for BCE, -1 = 100-1 BCE).
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
    a.width_value,
    a.width_unit_id,
    a.width_value * u_w.factor AS width_metres,
    a.height_value,
    a.height_unit_id,
    a.height_value * u_h.factor AS height_metres,
    a.length_value,
    a.length_unit_id,
    a.length_value * u_l.factor AS length_metres,
    a.diameter_value,
    a.diameter_unit_id,
    a.diameter_value * u_d.factor AS diameter_metres,
    a.object_type_id,
    a.collection_group_id,
    a.place_text,
    a.place_region_id,
    a.dynasty_id,
    CASE
        WHEN y.year IS NULL THEN NULL
        WHEN y.year > 0 THEN (y.year - 1) / 100 + 1
        WHEN y.year = 0 THEN -1
        ELSE -((-y.year - 1) / 100 + 1)
    END AS century,
    a.holder_id,
    a.storage_location_id,
    a.acquisition_method,
    a.acquisition_date,
    a.acquisition_source_id,
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
LEFT JOIN units_of_measure u_w ON u_w.id = a.width_unit_id
LEFT JOIN units_of_measure u_h ON u_h.id = a.height_unit_id
LEFT JOIN units_of_measure u_l ON u_l.id = a.length_unit_id
LEFT JOIN units_of_measure u_d ON u_d.id = a.diameter_unit_id
LEFT JOIN artefact_images pi ON pi.artefact_id = a.id AND pi.is_primary
LEFT JOIN media_assets pm ON pm.id = pi.media_asset_id;

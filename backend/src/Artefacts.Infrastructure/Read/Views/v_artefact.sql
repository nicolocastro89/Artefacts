-- Flattened artefact read view. Columns must match ArtefactRow (verified by an integration test).
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
    a.acquisition_method,
    a.acquisition_date,
    a.acquisition_source,
    a.acquisition_price_amount,
    a.acquisition_price_currency,
    pm.storage_key AS primary_image_key,
    (SELECT count(*) FROM artefact_images ai WHERE ai.artefact_id = a.id)::int AS image_count
FROM artefacts a
LEFT JOIN artefact_images pi ON pi.artefact_id = a.id AND pi.is_primary
LEFT JOIN media_assets pm ON pm.id = pi.media_asset_id;

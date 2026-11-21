-- Read views of an artefact's lists. Columns must match their row types (verified by an integration test).
-- Names of linked records (regions) are not columns: queries resolve them in the reader's language.

-- Provenance entries, in the order entered (position).
CREATE OR REPLACE VIEW v_artefact_provenance AS
SELECT
    p.artefact_id,
    p.position,
    p.region_id,
    p.period_display,
    p.period_earliest_year,
    p.period_latest_year,
    p.period_precision,
    p.note
FROM artefact_provenance p;

-- Images with their media asset; ordered by attached_at, the first attached being shown first.
CREATE OR REPLACE VIEW v_artefact_image AS
SELECT
    i.artefact_id,
    i.media_asset_id,
    m.storage_key,
    m.original_file_name,
    m.format,
    m.size_bytes,
    m.uploaded_at,
    i.caption,
    i.is_primary,
    i.attached_at
FROM artefact_images i
JOIN media_assets m ON m.id = i.media_asset_id;

-- Exhibitions, in the order entered (position).
CREATE OR REPLACE VIEW v_artefact_exhibition AS
SELECT e.artefact_id, e.position, e.text
FROM artefact_exhibitions e;

-- Descriptions per language (only languages with a description).
CREATE OR REPLACE VIEW v_artefact_description AS
SELECT t.artefact_id, t.language, t.description
FROM artefact_translations t
WHERE t.description IS NOT NULL;

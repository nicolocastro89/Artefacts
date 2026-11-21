-- Name functions for queries over the read views.

-- Resolve the name of a region or object type in the first language of p_languages that has one.
-- p_languages is the request's preference list (requested languages, then every content language in
-- fallback order), so a name is found whenever the entry has any name.
CREATE OR REPLACE FUNCTION region_name(p_id uuid, p_languages text[]) RETURNS text
LANGUAGE sql STABLE AS $$
    SELECT n.name
    FROM region_names n
    WHERE n.region_id = p_id
    ORDER BY coalesce(array_position(p_languages, n.language::text), 2147483647), n.language
    LIMIT 1
$$;

CREATE OR REPLACE FUNCTION object_type_name(p_id uuid, p_languages text[]) RETURNS text
LANGUAGE sql STABLE AS $$
    SELECT n.name
    FROM object_type_names n
    WHERE n.object_type_id = p_id
    ORDER BY coalesce(array_position(p_languages, n.language::text), 2147483647), n.language
    LIMIT 1
$$;

-- Resolve the name of a list entry (dynasty, collection group, storage location): the single name of an
-- untranslated entry, otherwise its name in the first language of p_languages that has one.
CREATE OR REPLACE FUNCTION reference_entry_name(p_id uuid, p_languages text[]) RETURNS text
LANGUAGE sql STABLE AS $$
    SELECT coalesce(
        (SELECT e.name FROM reference_entries e WHERE e.id = p_id),
        (SELECT n.name
         FROM reference_entry_names n
         WHERE n.reference_entry_id = p_id
         ORDER BY coalesce(array_position(p_languages, n.language::text), 2147483647), n.language
         LIMIT 1))
$$;

-- Whether any name of a list entry (its single name, or a name in any language) matches an ILIKE pattern
-- (escape character '\'): free-text search over the linked entries.
CREATE OR REPLACE FUNCTION reference_entry_matches(p_id uuid, p_pattern text) RETURNS boolean
LANGUAGE sql STABLE AS $$
    SELECT EXISTS (SELECT 1 FROM reference_entries e WHERE e.id = p_id AND e.name ILIKE p_pattern ESCAPE '\')
        OR EXISTS (SELECT 1 FROM reference_entry_names n WHERE n.reference_entry_id = p_id AND n.name ILIKE p_pattern ESCAPE '\')
$$;

-- The name of a party (parties have a single, untranslated name).
CREATE OR REPLACE FUNCTION party_name(p_id uuid) RETURNS text
LANGUAGE sql STABLE AS $$
    SELECT p.name FROM parties p WHERE p.id = p_id
$$;

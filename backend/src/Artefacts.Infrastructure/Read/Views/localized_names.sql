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

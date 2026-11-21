# Spec Delta

## MODIFIED Requirements

### Requirement: Classification, origin and holding fields
The system SHALL allow an artefact to record, in addition to its identity, description and catalogue entry: an object type from the object type reference list; a collection group from the collection group list; a place of production as text exactly as written, with an optional reference to a region from the region reference list; a dynasty from the dynasty list; an ordered list of exhibitions, each a free-text entry; a holder, which is a party; a storage location from the storage location list; and a valuation, which is an optional monetary value with the same rules as an acquisition price. All of these fields are optional. An artefact without a holder is held by the collection itself. Text fields SHALL be stored exactly as entered, apart from removing leading and trailing whitespace. The system MUST reject a reference to an object type, collection group, dynasty, storage location, party or place-of-production region that does not exist.

#### Scenario: Artefact with classification and origin
- **WHEN** a user saves an artefact with object type "Ceramics", collection group "CINA", place of production "Cina" linked to the region "China", and dynasty "Sung"
- **THEN** later reads of the artefact return those values, with the names of the object type, collection group, region and dynasty

#### Scenario: Uncertain place kept as written
- **WHEN** a user enters the place of production "Anatolia o Asia Centrale?" without linking a region
- **THEN** the artefact returns the place of production "Anatolia o Asia Centrale?" and no region

#### Scenario: Exhibitions keep their order
- **WHEN** a user saves the exhibitions "Arte Ottomana 1450-1600, Genova, 1999" followed by "Exhibition of Persian Art, New York, 1940"
- **THEN** the artefact returns both entries in that order

#### Scenario: Valuation with withdrawn currency
- **WHEN** a user records a valuation of 350000000 ITL
- **THEN** the artefact returns the valuation with amount 350000000 and currency "ITL"

#### Scenario: Unknown object type
- **WHEN** a user saves an artefact that references an object type identifier that does not exist
- **THEN** the system rejects the request with a validation error that names the object type field

#### Scenario: Holder and storage location
- **WHEN** a user saves an artefact held by the party "Rossetti Fine Arts srl" at the storage location "Beta2"
- **THEN** the artefact returns the holder "Rossetti Fine Arts srl" and the storage location "Beta2"

#### Scenario: Held by the collection
- **WHEN** a user saves an artefact without a holder
- **THEN** the artefact returns no holder, meaning it is held by the collection itself

#### Scenario: Unknown dynasty
- **WHEN** a user saves an artefact that references a dynasty identifier that does not exist
- **THEN** the system rejects the request with a validation error that names the dynasty field

### Requirement: Artefact reads in the request's language
Reading a single artefact or the artefact list SHALL resolve every translated value it includes in the request's language: the object type name, the region names of provenance entries and of the place of production, the description, the catalogue entry, and, when the list is grouped by object type or origin, the group labels. Ordering by group follows the resolved labels.

#### Scenario: Provenance region named in the reader's language
- **WHEN** a provenance entry references the region with names "Turchia" (it) and "Turkey" (en) and the artefact is read with `lang=it`
- **THEN** the entry's region name is "Turchia"

#### Scenario: Catalogue entry in the reader's language
- **WHEN** an artefact has catalogue entries in Italian and English and is read with `lang=en`
- **THEN** the returned catalogue entry is the English one, with language "en"

#### Scenario: Group labels in the reader's language
- **WHEN** the list is grouped by object type and read with `lang=it`
- **THEN** the group for the object type "Carpets" / "Tappeti" is labelled "Tappeti"

## ADDED Requirements

### Requirement: Catalogue entry per language
The system SHALL store an artefact's catalogue entry (long free text) separately for each content language, in the same way as its description. Create and update requests carry the catalogue entries of all languages together; reading an artefact returns the entry resolved in the request's language with its language, and, for editing, the entries in every language. Each entry keeps its line breaks and text exactly as entered, apart from removing leading and trailing whitespace. A catalogue entry for a language that is not a content language MUST be rejected.

#### Scenario: Catalogue entry in two languages
- **WHEN** a user saves an artefact with the catalogue entries { "it": "Coppa in celadon", "en": "Celadon bowl" }
- **THEN** reading it with `lang=it` returns "Coppa in celadon" in language "it", and with `lang=en` returns "Celadon bowl" in language "en"

#### Scenario: Catalogue entry only in one language
- **WHEN** an artefact has only an English catalogue entry and it is read with `lang=it`
- **THEN** the English entry is returned with language "en"

#### Scenario: Unknown language
- **WHEN** a user saves a catalogue entry for a language that is not a content language
- **THEN** the system rejects the request with a validation error that names the catalogue entries field

# Spec Delta

## ADDED Requirements

### Requirement: Description per language
The system SHALL store an artefact's description separately for each content language. Create and update requests carry the descriptions of all languages together; reading an artefact returns the description resolved in the request's language with its language, and, for editing, the descriptions in every language. Each description keeps its line breaks and text exactly as entered, apart from removing leading and trailing whitespace.

#### Scenario: Description in two languages
- **WHEN** a user saves an artefact with the descriptions { "it": "Piatto con fenice", "en": "Dish with a phoenix" }
- **THEN** reading it with `lang=it` returns "Piatto con fenice" in language "it", and with `lang=en` returns "Dish with a phoenix" in language "en"

#### Scenario: Description only in one language
- **WHEN** an artefact has only an Italian description and it is read with `lang=en`
- **THEN** the Italian description is returned with language "it"

### Requirement: Artefact reads in the request's language
Reading a single artefact or the artefact list SHALL resolve every translated value it includes in the request's language: the object type name, the region names of provenance entries and of the place of production, the description, and, when the list is grouped by object type or origin, the group labels. Ordering by group follows the resolved labels.

#### Scenario: Provenance region named in the reader's language
- **WHEN** a provenance entry references the region with names "Turchia" (it) and "Turkey" (en) and the artefact is read with `lang=it`
- **THEN** the entry's region name is "Turchia"

#### Scenario: Group labels in the reader's language
- **WHEN** the list is grouped by object type and read with `lang=it`
- **THEN** the group for the object type "Carpets" / "Tappeti" is labelled "Tappeti"

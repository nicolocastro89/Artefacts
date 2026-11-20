# Spec Delta

## Purpose

Defines the languages that collection content can be recorded in, and how a value that exists in several languages is read and written, so that names and descriptions can be kept in Italian, English and later languages without changing the system.

## ADDED Requirements

### Requirement: Managed list of content languages
The system SHALL keep a list of content languages, each with a unique language code (such as "it" or "en"), a display name, and a position in the fallback order. Initially the list contains English and Italian, with English first. A user SHALL be able to add a language and change the fallback order. Removing a language SHALL be rejected while any value is recorded in it.

#### Scenario: Languages listed in fallback order
- **WHEN** a client requests the content languages
- **THEN** the system returns English and Italian, English first

#### Scenario: Add a language without code changes
- **WHEN** a user adds the language "fr" named "French"
- **THEN** the language is listed last in the fallback order and values can be recorded in it

#### Scenario: Language in use cannot be removed
- **WHEN** a user removes Italian while a region has an Italian name
- **THEN** the system rejects the removal and reports that the language is in use

### Requirement: Language of a request
The system SHALL determine the preferred languages of a read request from its `lang` parameter when present (one code, or several separated by commas in order of preference). Otherwise it SHALL use the request's `Accept-Language` header in its quality order. Codes that are not content languages are ignored; a regional code such as "en-GB" matches "en".

#### Scenario: Parameter wins over the browser
- **WHEN** a request has `lang=it` and an `Accept-Language` of "en-GB,en;q=0.9"
- **THEN** Italian is the preferred language

#### Scenario: Browser language used when no parameter is given
- **WHEN** a request has no `lang` parameter and an `Accept-Language` of "it-IT,it;q=0.9,en;q=0.8"
- **THEN** Italian is preferred, then English

### Requirement: Resolving a translatable value
A translatable value SHALL be stored separately for each language, with no language marked as the original. When read, it SHALL resolve to the value in the first language that has one, taking the request's preferred languages first and then the fallback order. Where a translatable field is returned in its own right (an artefact's description, the name of a region or object type in its reference list), the response SHALL state the language of the resolved value; names embedded in other records (a provenance entry's region, a group label) are returned as text only. A value with no text in any language resolves to nothing.

#### Scenario: Requested language present
- **WHEN** a region has the names "Turchia" (it) and "Turkey" (en) and a client reads it with `lang=it`
- **THEN** the region's name is "Turchia" in language "it"

#### Scenario: Fallback is visible
- **WHEN** an artefact's description exists only in Italian and a client reads it with `lang=en`
- **THEN** the description is the Italian text and its language is reported as "it"

### Requirement: Writing all languages together
Requests that create or update a translatable value SHALL carry the values for all languages together, keyed by language code. A language left out or given as blank text has no value in that language afterwards. The system MUST reject values keyed by a code that is not a content language, with a validation error naming that key.

#### Scenario: Both languages saved at once
- **WHEN** a user saves a region with the names { "it": "Turchia", "en": "Turkey" }
- **THEN** later reads return "Turchia" for Italian and "Turkey" for English

#### Scenario: Unknown language rejected
- **WHEN** a user saves a region with a name keyed "xx"
- **THEN** the system rejects the request with a validation error on "names.xx"

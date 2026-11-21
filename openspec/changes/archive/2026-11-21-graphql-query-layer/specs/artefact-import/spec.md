# Spec Delta

## ADDED Requirements

### Requirement: Catalogue entry language in the mapping
When a user maps a column to the catalogue entry, the system SHALL also record the language of that text, and SHALL import it as the artefact's catalogue entry in that language. The suggested mapping SHALL propose a language for a column from its heading (a heading in English suggests English, one in Italian suggests Italian) and English when the heading does not say; the user SHALL be able to change it. The language is saved with a template. Columns in several languages are mapped as one column per language.

#### Scenario: English column
- **WHEN** a column headed "English catalogue entry" is mapped to the catalogue entry
- **THEN** the suggested language is English and each imported artefact gets that text as its English catalogue entry

#### Scenario: Italian column chosen by the user
- **WHEN** a user maps a column to the catalogue entry and chooses Italian
- **THEN** each imported artefact gets that text as its Italian catalogue entry and no English one

#### Scenario: Two languages from two columns
- **WHEN** one column is mapped to the catalogue entry in English and another in Italian
- **THEN** each imported artefact has both catalogue entries

#### Scenario: Template keeps the language
- **WHEN** a user saves a mapping with a catalogue entry column in Italian as a template and reuses it for another workbook
- **THEN** the mapping starts with that column mapped to the Italian catalogue entry

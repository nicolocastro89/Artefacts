# Spec Delta

## MODIFIED Requirements

### Requirement: Artefact fields and linked records
An artefact SHALL expose every field of the artefact read view. Each linked record (object type, collection group, place of production region, dynasty, storage location, holder and acquisition source) SHALL appear as an object with its identifier and its name resolved in the request's language. Parties also carry their kind. Historical dates SHALL carry their display text, earliest and latest year, and precision. Monetary values SHALL carry an amount and a currency. Measurements (width, height, length and diameter) SHALL carry their value, their value in metres, and their unit as an object with its identifier, its name resolved in the request's language, its abbreviation and its factor. The primary image SHALL carry its file details and caption, and signed URLs for each rendition. Values the artefact does not have SHALL be null.

#### Scenario: Names in the reader's language
- **WHEN** a client queries an artefact's dynasty with `lang=en` and the dynasty has the English name "Safavid" and the Italian name "Safavide"
- **THEN** the dynasty's name is "Safavid"

#### Scenario: Image URLs are signed renditions
- **WHEN** a client queries the URLs of an artefact's primary image
- **THEN** the result has thumb, web and print URLs that the image service accepts

#### Scenario: Held by the collection
- **WHEN** a client queries the holder of an artefact without a holder
- **THEN** the holder is null

#### Scenario: Measurement with its unit
- **WHEN** a client queries the height of an artefact whose height is 7 cm, with `lang=it`
- **THEN** the height has the value 7, the value in metres 0.07, and the unit "centimetro" with the abbreviation "cm" and the factor 0.01

#### Scenario: Missing measurement
- **WHEN** a client queries the width of an artefact without a width
- **THEN** the width is null

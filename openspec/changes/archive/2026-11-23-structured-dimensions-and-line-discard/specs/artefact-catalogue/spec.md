# Spec Delta

## ADDED Requirements

### Requirement: Structured measurements
The system SHALL allow an artefact to record, in addition to its dimensions text, four optional measurements: width, height, length and diameter. Each measurement SHALL be a number greater than zero with at most four decimal places, together with a unit from the units of measure list. Each measurement has its own unit. The dimensions text SHALL be kept exactly as entered and is independent of the measurements: either can be present without the other. Reads SHALL return the dimensions text and each measurement with its unit (identifier, resolved name and abbreviation) and its value converted to metres. The system MUST reject a measurement whose number is zero, negative or missing, or whose unit is missing or does not exist.

#### Scenario: Text and measurements together
- **WHEN** a user saves an artefact with the dimensions text "diam 17,6 x h 7", a diameter of 17.6 cm and a height of 7 cm
- **THEN** later reads return the text "diam 17,6 x h 7", the diameter 17.6 cm (0.176 m) and the height 7 cm (0.07 m), and no width or length

#### Scenario: Different units per measurement
- **WHEN** a user saves a height of 820 mm and a width of 25 in
- **THEN** the artefact returns the height 820 mm (0.82 m) and the width 25 in (0.635 m)

#### Scenario: Measurement without a unit
- **WHEN** a user saves a height of 7 without a unit
- **THEN** the system rejects the artefact with a validation error on the height

#### Scenario: Text only
- **WHEN** a user saves the dimensions text "82 × 64 cm (framed 96 × 78 cm)" without measurements
- **THEN** the artefact is saved with that text and no measurements

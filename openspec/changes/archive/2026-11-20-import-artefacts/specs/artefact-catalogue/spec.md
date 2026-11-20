# Spec Delta

## MODIFIED Requirements

### Requirement: Historical dates with precision
The system SHALL record historical dates (an artefact's creation date, and provenance periods) as three parts: the display text exactly as entered, a computed earliest and latest year, and a precision (exact year, decade, century, or approximate). The system SHALL accept at least these forms:
- a year ("1654");
- a circa year, before or after the number ("c. 1650", "1470 circa");
- a decade ("1680s");
- a century ("17th century"), also written as a Roman numeral, with or without "secolo" or "sec." ("XV", "XV secolo", "IX sec.");
- a part of a century: "early", "mid" and "late", or in Italian "inizio", "metà" and "fine", each covering a third of the century; and "prima metà" and "seconda metà", covering a half ("late 17th century", "seconda metà XV");
- a range of centuries or century parts joined by "-", "/", a space, or two parts written in sequence ("XI-XII", "IX/X", "XV XVI", "fine XII inizio XIII");
- a year range, including a shortened second year ("1650-1660", "1450 - 75", "1463-4");
- a century followed by a year range in brackets, where the bracketed range is used ("XV (1440-1446)");
- a bounded year ("before 1720", "after 1800").

The system MUST reject text it cannot interpret, unless the user enters the earliest and latest years explicitly.

#### Scenario: Circa date
- **WHEN** a user enters a creation date of "c. 1650"
- **THEN** the system stores the display text "c. 1650", a year range that brackets 1650, and precision "approximate"

#### Scenario: Circa after the year
- **WHEN** a user enters "1470 circa"
- **THEN** the system stores the same range as "c. 1470" and precision "approximate"

#### Scenario: Part-century date
- **WHEN** a user enters "late 17th century"
- **THEN** the system stores a range of 1667 to 1700 and precision "century"
- **AND** the date is displayed as "late 17th century"

#### Scenario: Roman-numeral century with Italian qualifier
- **WHEN** a user enters "seconda metà XV"
- **THEN** the system stores a range of 1451 to 1500 and precision "century"
- **AND** the date is displayed as "seconda metà XV"

#### Scenario: Span across two centuries
- **WHEN** a user enters "fine XII inizio XIII"
- **THEN** the system stores a range of 1167 to 1233 and precision "century"

#### Scenario: Shortened year range
- **WHEN** a user enters "1450 - 75"
- **THEN** the system stores a range of 1450 to 1475 and precision "exact year"

#### Scenario: Bracketed range refines a century
- **WHEN** a user enters "XV (1440-1446)"
- **THEN** the system stores a range of 1440 to 1446

#### Scenario: Uninterpretable text with explicit range
- **WHEN** a user enters "during the reign of Louis XIV" together with an earliest year of 1643 and a latest year of 1715
- **THEN** the system stores the display text unchanged together with the given range

#### Scenario: Uninterpretable text without explicit range
- **WHEN** a user enters "unknown era" with no explicit range
- **THEN** the system rejects the date with a validation error

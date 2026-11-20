# artefact-media Specification

## Purpose

Lets users attach images to artefacts, keeps the uploaded originals intact as archival copies, and serves each image at sizes suited to thumbnails, on-screen viewing and printing.

## Requirements

### Requirement: Upload an image to an artefact
The system SHALL allow a user to upload one or more image files to an existing artefact. Accepted formats are JPEG, PNG, TIFF and WebP. The system MUST identify the format from the file content, not from the file name or the declared content type, and MUST reject files in any other format or larger than the configured maximum size, which defaults to 200 MB. The uploaded original SHALL be stored byte-for-byte unchanged.

#### Scenario: Valid TIFF upload
- **WHEN** a user uploads a 60 MB TIFF file to an artefact
- **THEN** the system stores the original unchanged and the artefact lists the new image

#### Scenario: Disguised file rejected
- **WHEN** a user uploads a PDF file renamed to "photo.jpg"
- **THEN** the system rejects the upload with an unsupported-format error and stores nothing

#### Scenario: Oversized file rejected
- **WHEN** a user uploads a file larger than the configured maximum
- **THEN** the system rejects the upload with a size error and stores nothing

#### Scenario: Upload to unknown artefact
- **WHEN** a user uploads an image to an artefact identifier that does not exist
- **THEN** the system responds that the artefact was not found

### Requirement: Primary image
Each artefact with at least one image SHALL have exactly one primary image. The first image uploaded to an artefact SHALL become its primary image automatically. A user SHALL be able to make any of the artefact's images its primary image.

#### Scenario: First upload becomes primary
- **WHEN** an artefact with no images receives its first image
- **THEN** that image is the artefact's primary image

#### Scenario: Change primary
- **WHEN** a user makes the artefact's second image primary
- **THEN** that image is primary and the previous primary image no longer is

#### Scenario: Deleting the primary image
- **WHEN** a user deletes the primary image and other images remain
- **THEN** the earliest remaining uploaded image becomes primary

### Requirement: Sized image URLs
For every image, the system SHALL return URLs for three sizes: `thumb` (for grids and lists), `web` (for on-screen viewing) and `print` (for high-resolution print output). Every size SHALL be displayable by standard browsers, whatever the original format, and SHALL respect the original's orientation metadata. The URLs MUST be signed by the system, so that a client cannot request arbitrary sizes or arbitrary source files.

#### Scenario: TIFF shown in browser
- **WHEN** the frontend displays the `web` URL of an image uploaded as TIFF
- **THEN** the browser receives an image format it can display

#### Scenario: Tampered URL rejected
- **WHEN** a client changes the size or source part of a signed image URL
- **THEN** the image service refuses the request

### Requirement: Image metadata and removal
The system SHALL record each image's original file name, detected format, size in bytes, upload time and an optional caption. A user SHALL be able to edit the caption and to delete an image. Deleting an image SHALL remove it from the artefact and delete the stored original.

#### Scenario: Caption edit
- **WHEN** a user sets the caption "Recto, after 2021 conservation"
- **THEN** the image returns that caption

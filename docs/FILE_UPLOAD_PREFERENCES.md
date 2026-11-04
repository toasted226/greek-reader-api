# File Upload Implementation Preferences

## Database & Migrations
- While in development, new tables can be added to the existing v1 migration (`Data/Migrations/001_initial_schemas.sql`) rather than creating new migration files.
- For TOAST compression on large bytea columns where app-level compression is used (gzip), set `STORAGE EXTERNAL` to avoid double compression. In migrations this requires `ALTER TABLE ... ALTER COLUMN ... SET STORAGE EXTERNAL` (cannot always be specified inline with constraints).

## Code Organization
- Filename normalization is a utility/helper and belongs in `Utilities/` (not `Services/`, which is for more important business logic).
- Request/Response models should be moved out of endpoint files into the `Models/` folder for better organization.
- Repository methods like `CreateAsync` should take model instances as parameters rather than individual fields.

## File Upload Implementation
- Store the F# normalized text (via `Api.Normalise` from `GreekReader.Interop`) in the database, not the raw text.
- Store file content compressed (gzip) in a separate table to avoid scanning content in metadata lookups.
- File metadata should include: normalized display name (underscores/dashes replaced with spaces, first letter of each word capitalized), original filename, upload time, and file size in bytes.
- When returning file metadata in lists, don't include text or analyzed responses; use a dedicated metadata response model.
- Endpoints dealing with user files must require auth context and verify user ownership.
- After normalization and analysis, return both file metadata and analysis results from upload/retrieval endpoints as appropriate.

## Response Models
- Prefer records with init properties (`{ get; init; }`) over positional records for response/request models for more explicit initialization.
- Instantiate records using object initializer syntax with named properties rather than positional constructor arguments.

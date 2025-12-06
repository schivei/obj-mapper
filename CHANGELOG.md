# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Stored Procedure Support**: Extract and generate code for stored procedures
  - Output type detection: None, Scalar, or Tabular
  - EF Core: DbContext extension methods with sync/async variants
  - Dapper: IDbConnection extension methods
  - Result type classes for tabular procedures with `[Keyless]` attribute
  - Database-agnostic parameter creation

- **View Extraction**: Extract and generate entities for database views
  - Support for SQL Server, PostgreSQL, MySQL, SQLite
  - Views treated as read-only entities

- **Legacy Relationship Inference** (`--legacy`): Infer relationships from naming patterns
  - Detects patterns: `table_id`, `tableId`, `fk_table`, `table_fk`
  - Useful for databases without explicit foreign keys
  - Supports plural/singular table name matching

- **Performance Options**:
  - `--no-checks`: Disable data sampling queries for faster extraction
  - `--no-views`: Skip view extraction
  - `--no-procs`: Skip stored procedure extraction
  - `--no-udfs`: Skip user-defined function extraction
  - `--no-rel`: Disable relationship mapping

- **Name-Based Type Inference**: Fast inference without database queries
  - Boolean patterns: `is_*`, `has_*`, `can_*`, `*_flag`, `*_enabled`
  - GUID patterns: `*_uuid`, `*_guid`, `correlation_id`, `tracking_id`

- **Rich Console Output**: Enhanced terminal experience using Spectre.Console
  - ASCII art header
  - Progress bars with time estimates
  - Colored output with status icons
  - Configuration and statistics tables
  - Detailed execution log saved to temp file

### Changed

- Type inference now applies name-based inference first (fast), then data sampling only for uninferred columns
- Stored procedure code generation uses database-specific parameter types
- GUID name pattern detection is now more precise to avoid false positives

### Fixed

- Fixed NuGet release pipeline secret handling
- Fixed relationship and index mapping for existing databases
- Fixed always-false conditions in TypeMapper
- Removed unused variable assignments

## [1.0.0] - Initial Release

### Added

- Database schema extraction from PostgreSQL, SQL Server, MySQL, SQLite
- EF Core entity and configuration generation
- Dapper entity and repository generation
- ML-based type inference for column mapping
- Scalar UDF extraction and code generation
- Type converters for DateOnly, TimeOnly, DateTimeOffset
- GUID column detection for char(36)/varchar(36)
- Boolean column detection for tinyint/smallint
- Full relationship mapping (1:1, 1:N, N:1, N:M)
- Index mapping with composite key support
- Multi-language pluralization support
- Global and local configuration files
- CSV file input mode
- Database connection string mode

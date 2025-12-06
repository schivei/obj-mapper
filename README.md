# omap

Database reverse engineering dotnet tool - generates entity mappings from CSV schema files or database connections for EF Core and Dapper. Features ML-based type inference, scalar UDF support, and automatic type converters.

[![Automatic Dependency Submission](https://github.com/schivei/obj-mapper/actions/workflows/dependency-graph/auto-submission/badge.svg)](https://github.com/schivei/obj-mapper/actions/workflows/dependency-graph/auto-submission)
[![CI](https://github.com/schivei/obj-mapper/actions/workflows/ci.yml/badge.svg)](https://github.com/schivei/obj-mapper/actions/workflows/ci.yml)
[![CodeQL](https://github.com/schivei/obj-mapper/actions/workflows/codeql.yml/badge.svg)](https://github.com/schivei/obj-mapper/actions/workflows/codeql.yml)
[![Copilot code review](https://github.com/schivei/obj-mapper/actions/workflows/copilot-pull-request-reviewer/copilot-pull-request-reviewer/badge.svg)](https://github.com/schivei/obj-mapper/actions/workflows/copilot-pull-request-reviewer/copilot-pull-request-reviewer)
[![Copilot coding agent](https://github.com/schivei/obj-mapper/actions/workflows/copilot-swe-agent/copilot/badge.svg)](https://github.com/schivei/obj-mapper/actions/workflows/copilot-swe-agent/copilot)
[![Release](https://github.com/schivei/obj-mapper/actions/workflows/release.yml/badge.svg)](https://github.com/schivei/obj-mapper/actions/workflows/release.yml)
[![NuGet](https://img.shields.io/nuget/v/ObjMapper?style=flat)](https://www.nuget.org/packages/ObjMapper/)
![GitHub Pre-Release](https://img.shields.io/github/v/release/schivei/obj-mapper?include_prereleases)

## Features

- **Database Schema Extraction**: Extract schema directly from PostgreSQL, SQL Server, MySQL, SQLite databases
- **ML-Based Type Inference**: Intelligent column type mapping using machine learning and pattern matching
- **Scalar UDF Support**: Extract and generate code for user-defined scalar functions
- **Type Converters**: Automatic generation of ValueConverters (EF Core) and TypeHandlers (Dapper) for DateOnly, TimeOnly, DateTimeOffset
- **GUID Column Detection**: Automatically detect varchar(36) columns that contain GUIDs
- **Boolean Column Detection**: Detect small integer columns (tinyint, smallint) that represent boolean values
- **Relationship Mapping**: Full support for 1:1, 1:N, N:1, and N:M relationships
- **Index Mapping**: Support for simple and composite indexes

## Installation

```bash
dotnet tool install --global ObjMapper
```

Or install as a local tool:

```bash
dotnet new tool-manifest
dotnet tool install ObjMapper
```

## Usage

The tool supports two modes of operation:

### Mode 1: CSV Files
```bash
omap <csv-file> -t <mapping-type> -d <database-type> [options]
```

### Mode 2: Database Connection
```bash
omap --connection-string "<connection-string>" -t <mapping-type> [options]
```

When using a connection string, the schema is extracted directly from the database, eliminating the need for CSV files. The database type is auto-detected from the connection string.

### Arguments

- `csv`: CSV file with schema information (optional if using --connection-string)
  - Columns: `schema`, `table`, `column`, `nullable`, `type`, `comment`

### Options

- `--connection-string, --cs`: Database connection string (alternative to CSV files)
  - Schema will be extracted directly from the database
  - Database type is auto-detected when possible

- `-s, --schema`: Database schema to extract (optional, used with --connection-string)
  - Default: `public` for PostgreSQL, `dbo` for SQL Server, database name for MySQL

- `-t, --type`: Type of mapping to generate (required)
  - `efcore`: Entity Framework Core entities and configurations
  - `dapper`: Dapper entities and repositories

- `-d, --database`: Database type (required for CSV mode, auto-detected for connection string mode)
  - `mysql`: MySQL
  - `postgre` or `postgresql`: PostgreSQL
  - `sqlserver` or `mssql`: SQL Server
  - `oracle`: Oracle
  - `sqlite`: SQLite

- `-f, --foreignkeys`: CSV file with relationships (optional)
  - Columns: `name`, `schema_from`, `schema_to`, `table_from`, `table_to`, `key`, `foreign`
  - Supports composite keys (comma-separated in `key` and `foreign` columns)
  - Supports cross-schema relationships

- `-i, --indexes`: CSV file with indexes (optional)
  - Columns: `schema`, `table`, `name`, `key`, `type`
  - Supports composite indexes (comma-separated in `key` column)
  - Type can be: `unique`, `btree`, `hash`, `fulltext`, etc.

- `-o, --output`: Output directory for generated files (default: current directory)

- `-n, --namespace`: Namespace for generated classes (default: `Generated`)

- `-c, --context`: Name of the database context class (default: `AppDbContext`)

- `-e, --entity-mode`: Entity generation mode (default: `class`)
  - `class` or `cls`: Generate as classes
  - `record` or `rec`: Generate as records
  - `struct` or `str`: Generate as structs
  - `record_struct` or `rtr`: Generate as record structs

- `-l, --locale`: Locale for pluralization (default: `en-us`)
  - Supported locales: `en-us`, `en-gb`, `en`, `pt-br`, `pt-pt`, `pt`, `es-es`, `es-mx`, `es`, `fr-fr`, `fr-ca`, `fr`, `de-de`, `de`, `it-it`, `it`, `nl-nl`, `nl`, `ru-ru`, `ru`, `pl-pl`, `pl`, `tr-tr`, `tr`, `ja-jp`, `ja`, `ko-kr`, `ko`, `zh-cn`, `zh-tw`, `zh`

- `--no-pluralize`: Disable pluralization/singularization

- `--no-inference`: Disable ML-based type inference (enabled by default)
  - Type inference analyzes column names, types, and comments to determine the best C# type
  - Automatically detects boolean columns, GUIDs, and date/time types

## Configuration

The tool supports configuration files at two levels:

### Global Configuration

Located at `~/.omap/config.json`. Created automatically on first run.

```json
{
  "locale": "en-us",
  "noPluralizer": false,
  "namespace": "MyApp.Data",
  "database": "postgresql",
  "type": "efcore",
  "entityMode": "class",
  "context": "AppDbContext"
}
```

### Local Configuration

Place a `.omap/config.json` file in your project directory (or any parent directory). Local settings override global settings.

### Priority

Settings are applied in this order (later overrides earlier):
1. Default values
2. Global configuration (`~/.omap/config.json`)
3. Local configuration (`.omap/config.json` found recursively)
4. Command-line arguments

### Configuration Commands

```bash
# Set a configuration value (global by default)
omap config set locale pt-br
omap config set namespace MyApp.Data
omap config set database postgresql

# Set a local configuration value
omap config set locale pt-br --local

# Remove a configuration value
omap config unset namespace
omap config unset namespace --local

# List all configuration values
omap config list

# Show configuration file paths
omap config path
omap config path --local
omap config path --global
```

Available configuration keys:
- `locale` / `l` - Locale for pluralization
- `namespace` / `n` - Namespace for generated classes
- `database` / `d` - Database type
- `type` / `t` - Mapping type (efcore/dapper)
- `entity-mode` / `e` - Entity generation mode
- `context` / `c` - Database context name
- `no-pluralize` - Disable pluralization (true/false)

## Examples

### Generate EF Core mappings from CSV

```bash
omap schema.csv -t efcore -d postgresql -o ./Generated -n MyApp.Data
```

### Generate from database connection

```bash
# PostgreSQL
omap --cs "Host=localhost;Database=mydb;Username=user;Password=pass" -t efcore -o ./Generated

# MySQL
omap --cs "Server=localhost;Database=mydb;User=user;Password=pass" -t dapper -o ./Generated

# SQL Server
omap --cs "Server=localhost;Database=mydb;User Id=user;Password=pass;TrustServerCertificate=True" -t efcore -o ./Generated

# SQLite
omap --cs "Data Source=mydb.sqlite" -t efcore -o ./Generated

# With schema filter
omap --cs "Host=localhost;Database=mydb;Username=user;Password=pass" -t efcore -s sales -o ./Generated
```

### Generate Dapper mappings with relationships and indexes

```bash
omap schema.csv -t dapper -d mysql -f relationships.csv -i indexes.csv -o ./Generated -n MyApp.Data -c MyDbContext
```

### Generate as records

```bash
omap schema.csv -t efcore -d sqlserver -e record -o ./Generated -n MyApp.Data
```

### Use Portuguese pluralization

```bash
omap schema.csv -t efcore -d postgresql -l pt-br -o ./Generated -n MyApp.Data
```

### Disable pluralization

```bash
omap schema.csv -t efcore -d postgresql --no-pluralize -o ./Generated -n MyApp.Data
```

## CSV File Formats

### Schema CSV (schema.csv)

```csv
schema,table,column,nullable,type,comment
public,users,id,false,int,Primary key
public,users,name,false,varchar(100),User name
public,users,email,true,varchar(255),Email address
public,users,created_at,false,timestamp,Creation timestamp
public,orders,id,false,int,Primary key
public,orders,user_id,false,int,Foreign key to users
public,orders,total,false,decimal(10,2),Order total
```

### Relationships CSV (relationships.csv)

```csv
name,schema_from,schema_to,table_from,table_to,key,foreign
fk_orders_users,public,public,orders,users,id,user_id
```

For composite keys:

```csv
name,schema_from,schema_to,table_from,table_to,key,foreign
fk_composite,public,public,order_items,orders,"order_id,product_id","order_id,product_id"
```

### Indexes CSV (indexes.csv)

```csv
schema,table,name,key,type
public,users,idx_users_email,email,unique
public,orders,idx_orders_user_id,user_id,btree
```

## Output Structure

The tool generates the following files (all types are `partial`):

```
output/
├── Entities/
│   ├── User.cs
│   └── Order.cs
├── Configurations/
│   ├── UserConfiguration.cs      # EF Core only
│   ├── OrderConfiguration.cs     # EF Core only
│   ├── UserRepository.cs         # Dapper only
│   └── OrderRepository.cs        # Dapper only
├── Converters/                   # Generated when using DateOnly, TimeOnly, DateTimeOffset
│   ├── DateOnlyConverter.cs      # EF Core ValueConverter
│   ├── TimeOnlyConverter.cs      # EF Core ValueConverter
│   ├── DateTimeOffsetConverter.cs # EF Core ValueConverter
│   └── DapperTypeHandlers.cs     # Dapper TypeHandlers + registration
├── Functions/                    # Generated when scalar UDFs exist
│   ├── ScalarFunctions.cs        # EF Core [DbFunction] stubs
│   └── ScalarFunctionRepository.cs # Dapper ExecuteScalar wrappers
└── AppDbContext.cs
```

## Type Inference

Type inference is enabled by default and uses ML.NET combined with pattern matching to determine the best C# type for each column. Use `--no-inference` to disable.

### Pattern-Based Inference

The tool recognizes common column naming patterns:

| Pattern | Inferred Type |
|---------|---------------|
| `is_*`, `has_*`, `*_flag`, `active`, `enabled`, `deleted` | `bool` |
| `uuid`, `*_guid`, `*_uuid`, `correlation_id`, `tracking_id` | `Guid` |
| `*_at`, `*_date`, `created`, `updated`, `deleted_at` | `DateTime` / `DateOnly` |
| `*_time` | `TimeOnly` |

### Boolean Column Detection (Connection String Mode)

When using `--connection-string` (or `--cs`), the tool queries small integer columns (tinyint, smallint, bit) to check if they only contain NULL, 0, or 1 values. If so, they are mapped to `bool`.

```sql
-- Example: Column "is_active TINYINT" with values {0, 1} → bool
-- The tool runs: SELECT DISTINCT is_active FROM users WHERE is_active IS NOT NULL
```

### GUID Column Detection (Connection String Mode)

When using `--connection-string` (or `--cs`), varchar(36) and char(36) columns are analyzed for valid GUID values:
- Requires at least 10 valid GUID values
- Must not contain any blank or whitespace-only values
- If conditions are met, the column is mapped to `Guid`

```csharp
// Column "tracking_id VARCHAR(36)" with valid GUIDs → Guid
public Guid TrackingId { get; set; }
```

### CSV Mode

In CSV mode, `char(36)` columns are automatically mapped to `Guid`. Additional inference based on column names is still applied.

## Scalar User-Defined Functions

The tool extracts scalar user-defined functions (UDFs) from the database and generates appropriate code:

### EF Core

Generates static methods with `[DbFunction]` attribute:

```csharp
public static partial class ScalarFunctions
{
    [DbFunction("calculate_tax", "dbo")]
    public static decimal CalculateTax(decimal amount, decimal rate)
        => throw new NotSupportedException("This method is for use in LINQ queries only.");
}
```

Register in your DbContext:

```csharp
modelBuilder.HasDbFunction(typeof(ScalarFunctions).GetMethod(nameof(ScalarFunctions.CalculateTax)));
```

### Dapper

Generates repository methods with sync and async variants:

```csharp
public partial class ScalarFunctionRepository(IDbConnection connection)
{
    public decimal CalculateTax(decimal amount, decimal rate)
    {
        return connection.ExecuteScalar<decimal>(
            "SELECT dbo.calculate_tax(@Amount, @Rate)",
            new { Amount = amount, Rate = rate });
    }
    
    public async Task<decimal> CalculateTaxAsync(decimal amount, decimal rate)
    {
        return await connection.ExecuteScalarAsync<decimal>(
            "SELECT dbo.calculate_tax(@Amount, @Rate)",
            new { Amount = amount, Rate = rate });
    }
}
```

## Type Converters

The tool automatically generates type converters for types not natively supported by all database drivers:

### DateOnly, TimeOnly, DateTimeOffset

Some database drivers don't natively support `DateOnly`, `TimeOnly`, or `DateTimeOffset`. The tool generates:

**EF Core ValueConverters:**

```csharp
public class DateOnlyConverter : ValueConverter<DateOnly, DateTime>
{
    public DateOnlyConverter() : base(
        d => d.ToDateTime(TimeOnly.MinValue),
        d => DateOnly.FromDateTime(d))
    { }
}
```

**Dapper TypeHandlers:**

```csharp
public class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override DateOnly Parse(object value) => DateOnly.FromDateTime((DateTime)value);
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
        parameter.DbType = DbType.Date;
    }
}

// Registration helper
public static class DapperTypeHandlerRegistration
{
    public static void RegisterAll()
    {
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        SqlMapper.AddTypeHandler(new TimeOnlyTypeHandler());
        SqlMapper.AddTypeHandler(new DateTimeOffsetTypeHandler());
    }
}
```

### DateTimeOffset Mapping

The tool maps database types to `DateTimeOffset` when appropriate:

| Database | Type | C# Type |
|----------|------|---------|
| PostgreSQL | `timestamptz` | `DateTimeOffset` |
| SQL Server | `datetimeoffset` | `DateTimeOffset` |

## Pluralization

The tool supports pluralization and singularization in multiple languages:

| Language | Locales |
|----------|---------|
| English | `en-us`, `en-gb`, `en` |
| Portuguese | `pt-br`, `pt-pt`, `pt` |
| Spanish | `es-es`, `es-mx`, `es` |
| French | `fr-fr`, `fr-ca`, `fr` |
| German | `de-de`, `de` |
| Italian | `it-it`, `it` |
| Dutch | `nl-nl`, `nl` |
| Russian | `ru-ru`, `ru` |
| Polish | `pl-pl`, `pl` |
| Turkish | `tr-tr`, `tr` |
| Japanese | `ja-jp`, `ja` |
| Korean | `ko-kr`, `ko` |
| Chinese | `zh-cn`, `zh-tw`, `zh` |

Note: Japanese, Korean, and Chinese don't typically have plural forms, so pluralization is disabled for these languages.

## Versioning

The tool uses semantic versioning with support for preview versions:
- Release: `1.0.0`
- Beta: `1.0.0-beta1`
- Release Candidate: `1.0.0-rc1`

## License

MIT License - see [LICENSE](LICENSE) for details.

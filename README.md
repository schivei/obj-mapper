# obj-mapper

Database reverse engineering dotnet tool - generates entity mappings from CSV schema files for EF Core and Dapper.

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

```bash
obj-mapper <csv-file> -t <mapping-type> -d <database-type> [options]
```

### Arguments

- `csv`: CSV file with schema information (required)
  - Columns: `schema`, `table`, `column`, `nullable`, `type`, `comment`

### Options

- `-t, --type`: Type of mapping to generate (required)
  - `efcore`: Entity Framework Core entities and configurations
  - `dapper`: Dapper entities and repositories

- `-d, --database`: Database type (required)
  - `mysql`: MySQL
  - `postgre` or `postgresql`: PostgreSQL
  - `sqlserver` or `mssql`: SQL Server
  - `oracle`: Oracle
  - `sqlite`: SQLite

- `-f, --foreignkeys`: CSV file with relationships (optional)
  - Columns: `from`, `to`, `keys`, `foreignkeys`

- `-o, --output`: Output directory for generated files (default: current directory)

- `-n, --namespace`: Namespace for generated classes (default: `Generated`)

- `-c, --context`: Name of the database context class (default: `AppDbContext`)

## Examples

### Generate EF Core mappings

```bash
obj-mapper schema.csv -t efcore -d postgresql -o ./Generated -n MyApp.Data
```

### Generate Dapper mappings with relationships

```bash
obj-mapper schema.csv -t dapper -d mysql -f relationships.csv -o ./Generated -n MyApp.Data -c MyDbContext
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
from,to,keys,foreignkeys
orders,users,id,user_id
```

## Output Structure

The tool generates the following files:

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
└── AppDbContext.cs
```

## License

MIT License - see [LICENSE](LICENSE) for details.

using System.CommandLine;
using ObjMapper.Generators;
using ObjMapper.Models;
using ObjMapper.Parsers;
using ObjMapper.Services;

// Ensure global config exists
ConfigurationService.EnsureGlobalConfigExists();

// Load effective configuration
var config = ConfigurationService.LoadEffectiveConfig();

// Create root command
var rootCommand = new RootCommand("Database reverse engineering dotnet tool - generates entity mappings from CSV schema files or database connections");

// ============================================
// Config subcommand
// ============================================
var configCommand = new Command("config", "Manage omap configuration");

// Config set command
var configSetCommand = new Command("set", "Set a configuration value");
var configKeyArg = new Argument<string>("key") { Description = "Configuration key (locale, namespace, database, type, entity-mode, context, no-pluralize)" };
var configValueArg = new Argument<string>("value") { Description = "Configuration value" };
var configLocalOption = new Option<bool>("--local") { Description = "Force local configuration (in .omap folder near solution/project)" };

configSetCommand.Arguments.Add(configKeyArg);
configSetCommand.Arguments.Add(configValueArg);
configSetCommand.Options.Add(configLocalOption);

configSetCommand.SetAction((parseResult) =>
{
    var key = parseResult.GetValue(configKeyArg)!;
    var value = parseResult.GetValue(configValueArg)!;
    var local = parseResult.GetValue(configLocalOption);
    
    var (success, path) = ConfigurationService.SetConfigValue(key, value, local);
    if (success)
    {
        Console.WriteLine($"Configuration '{key}' set to '{value}'");
        Console.WriteLine($"Saved to: {path}");
    }
    else
    {
        Console.Error.WriteLine($"Failed to set configuration: {path}");
    }
});

// Config unset command
var configUnsetCommand = new Command("unset", "Remove a configuration value");
var configUnsetKeyArg = new Argument<string>("key") { Description = "Configuration key to remove" };
var configUnsetLocalOption = new Option<bool>("--local") { Description = "Remove from local configuration" };

configUnsetCommand.Arguments.Add(configUnsetKeyArg);
configUnsetCommand.Options.Add(configUnsetLocalOption);

configUnsetCommand.SetAction((parseResult) =>
{
    var key = parseResult.GetValue(configUnsetKeyArg)!;
    var local = parseResult.GetValue(configUnsetLocalOption);
    
    var (success, path) = ConfigurationService.UnsetConfigValue(key, local);
    if (success)
    {
        Console.WriteLine($"Configuration '{key}' removed");
        Console.WriteLine($"Updated: {path}");
    }
    else
    {
        Console.Error.WriteLine($"Failed to unset configuration: {path}");
    }
});

// Config list command
var configListCommand = new Command("list", "List all configuration values");
configListCommand.SetAction((_) =>
{
    Console.WriteLine("omap Configuration");
    Console.WriteLine("==================");
    Console.WriteLine();
    Console.WriteLine($"Global config: {ConfigurationService.GlobalConfigPath}");
    Console.WriteLine($"Local config:  {ConfigurationService.GetLocalConfigPath()}");
    Console.WriteLine();
    Console.WriteLine("Current settings:");
    Console.WriteLine();

    var configs = ConfigurationService.ListConfig();
    var maxKeyLen = configs.Keys.Max(k => k.Length);
    
    foreach (var (key, (value, source)) in configs)
    {
        var displayValue = value ?? "(not set)";
        Console.WriteLine($"  {key.PadRight(maxKeyLen + 2)} = {displayValue,-20} [{source}]");
    }
});

// Config path command
var configPathCommand = new Command("path", "Show configuration file paths");
var configPathLocalOption = new Option<bool>("--local") { Description = "Show local configuration path" };
var configPathGlobalOption = new Option<bool>("--global") { Description = "Show global configuration path" };

configPathCommand.Options.Add(configPathLocalOption);
configPathCommand.Options.Add(configPathGlobalOption);

configPathCommand.SetAction((parseResult) =>
{
    var local = parseResult.GetValue(configPathLocalOption);
    var global = parseResult.GetValue(configPathGlobalOption);
    
    if (local || (!local && !global))
    {
        Console.WriteLine($"Local:  {ConfigurationService.GetLocalConfigPath()}");
    }
    if (global || (!local && !global))
    {
        Console.WriteLine($"Global: {ConfigurationService.GlobalConfigPath}");
    }
});

configCommand.Subcommands.Add(configSetCommand);
configCommand.Subcommands.Add(configUnsetCommand);
configCommand.Subcommands.Add(configListCommand);
configCommand.Subcommands.Add(configPathCommand);

rootCommand.Subcommands.Add(configCommand);

// ============================================
// Main generate command options
// ============================================

// Define options - CSV file is now optional
var schemaFileArg = new Argument<FileInfo?>("csv")
{
    Description = "CSV file with schema information (optional if using --connection-string)",
    Arity = ArgumentArity.ZeroOrOne,
    DefaultValueFactory = _ => null
};

// Connection string option (alternative to CSV)
var connectionStringOption = new Option<string?>("--connection-string", "--cs")
{
    Description = "Database connection string (alternative to CSV files). Schema will be extracted directly from the database."
};

// Schema filter for connection string mode
var schemaFilterOption = new Option<string?>("--schema", "-s")
{
    Description = "Database schema to extract (e.g., 'public' for PostgreSQL, 'dbo' for SQL Server). Used with --connection-string."
};

var mappingTypeOption = new Option<string>("--type", "-t")
{
    Description = "Type of mapping to generate",
    Required = true
};
mappingTypeOption.Validators.Add(result =>
{
    var value = result.GetValue(mappingTypeOption);
    if (value != null && !value.Equals("efcore", StringComparison.OrdinalIgnoreCase) && 
        !value.Equals("dapper", StringComparison.OrdinalIgnoreCase))
    {
        result.AddError("Mapping type must be 'efcore' or 'dapper'");
    }
});

var databaseTypeOption = new Option<string?>("--database", "-d")
{
    Description = "Database type (mysql, postgre, sqlserver, oracle, sqlite). Auto-detected when using --connection-string."
};
databaseTypeOption.Validators.Add(result =>
{
    var value = result.GetValue(databaseTypeOption);
    var validTypes = new[] { "mysql", "postgre", "postgresql", "sqlserver", "mssql", "oracle", "sqlite" };
    if (value != null && !validTypes.Contains(value.ToLowerInvariant()))
    {
        result.AddError($"Database type must be one of: {string.Join(", ", validTypes)}");
    }
});

var relationshipsFileOption = new Option<FileInfo?>("--foreignkeys", "-f")
{
    Description = "CSV file with relationships (columns: name, schema_from, schema_to, table_from, table_to, key, foreign)"
};

var indexesFileOption = new Option<FileInfo?>("--indexes", "-i")
{
    Description = "CSV file with indexes (columns: schema, table, name, key, type)"
};

var outputDirOption = new Option<DirectoryInfo>("--output", "-o")
{
    Description = "Output directory for generated files",
    DefaultValueFactory = _ => new DirectoryInfo(Directory.GetCurrentDirectory())
};

var namespaceOption = new Option<string>("--namespace", "-n")
{
    Description = "Namespace for generated classes",
    DefaultValueFactory = _ => config.Namespace ?? "Generated"
};

var contextNameOption = new Option<string>("--context", "-c")
{
    Description = "Name of the database context class",
    DefaultValueFactory = _ => config.Context ?? "AppDbContext"
};

var entityModeOption = new Option<string>("--entity-mode", "-e")
{
    Description = "Entity generation mode: class|cls (default), record|rec, struct|str, record_struct|rtr",
    DefaultValueFactory = _ => config.EntityMode ?? "class"
};
entityModeOption.Validators.Add(result =>
{
    var value = result.GetValue(entityModeOption);
    var validModes = new[] { "class", "cls", "record", "rec", "struct", "str", "record_struct", "rtr" };
    if (value != null && !validModes.Contains(value.ToLowerInvariant()))
    {
        result.AddError($"Entity mode must be one of: {string.Join(", ", validModes)}");
    }
});

var localeOption = new Option<string>("--locale", "-l")
{
    Description = $"Locale for pluralization ({string.Join(", ", PluralizerService.SupportedLocales.Take(10))}...)",
    DefaultValueFactory = _ => config.Locale ?? "en-us"
};
localeOption.Validators.Add(result =>
{
    var value = result.GetValue(localeOption);
    if (value != null && !PluralizerService.SupportedLocales.Contains(value.ToLowerInvariant().Replace('_', '-')))
    {
        result.AddError($"Locale must be one of: {string.Join(", ", PluralizerService.SupportedLocales)}");
    }
});

var noPluralizeOption = new Option<bool>("--no-pluralize")
{
    Description = "Disable pluralization/singularization",
    DefaultValueFactory = _ => config.NoPluralizer
};

// Add validation for required inputs
rootCommand.Validators.Add(result =>
{
    var csvFile = result.GetValue(schemaFileArg);
    var connString = result.GetValue(connectionStringOption);
    var dbType = result.GetValue(databaseTypeOption);
    
    // Must have either CSV file or connection string
    if (csvFile == null && string.IsNullOrEmpty(connString))
    {
        result.AddError("Either a CSV file or --connection-string must be provided.");
        return;
    }
    
    // If using CSV, database type is required
    if (csvFile != null && string.IsNullOrEmpty(connString) && string.IsNullOrEmpty(dbType))
    {
        result.AddError("Database type (-d/--database) is required when using CSV files.");
    }
});

// Add options to root command
rootCommand.Arguments.Add(schemaFileArg);
rootCommand.Options.Add(connectionStringOption);
rootCommand.Options.Add(schemaFilterOption);
rootCommand.Options.Add(mappingTypeOption);
rootCommand.Options.Add(databaseTypeOption);
rootCommand.Options.Add(relationshipsFileOption);
rootCommand.Options.Add(indexesFileOption);
rootCommand.Options.Add(outputDirOption);
rootCommand.Options.Add(namespaceOption);
rootCommand.Options.Add(contextNameOption);
rootCommand.Options.Add(entityModeOption);
rootCommand.Options.Add(localeOption);
rootCommand.Options.Add(noPluralizeOption);

// Set handler
rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var options = new CommandOptions
    {
        SchemaFile = parseResult.GetValue(schemaFileArg),
        ConnectionString = parseResult.GetValue(connectionStringOption),
        SchemaFilter = parseResult.GetValue(schemaFilterOption),
        MappingType = parseResult.GetValue(mappingTypeOption)!,
        DatabaseType = parseResult.GetValue(databaseTypeOption) ?? string.Empty,
        RelationshipsFile = parseResult.GetValue(relationshipsFileOption),
        IndexesFile = parseResult.GetValue(indexesFileOption),
        OutputDir = parseResult.GetValue(outputDirOption)!,
        Namespace = parseResult.GetValue(namespaceOption)!,
        ContextName = parseResult.GetValue(contextNameOption)!,
        EntityMode = parseResult.GetValue(entityModeOption)!,
        Locale = parseResult.GetValue(localeOption)!,
        NoPluralizer = parseResult.GetValue(noPluralizeOption)
    };

    await ExecuteAsync(options);
});

return await rootCommand.Parse(args).InvokeAsync();

static async Task ExecuteAsync(CommandOptions options)
{
    Console.WriteLine("ObjMapper - Database Reverse Engineering Tool");
    Console.WriteLine("=============================================");
    Console.WriteLine();

    // Configure NamingHelper with locale settings
    NamingHelper.Configure(options.Locale, options.NoPluralizer);

    DatabaseType dbType;
    DatabaseSchema schema;

    if (options.UseConnectionString)
    {
        // Connection string mode - extract schema from database
        Console.WriteLine("Mode: Database Connection");
        Console.WriteLine($"Connection: {MaskConnectionString(options.ConnectionString!)}");
        
        // Try to auto-detect database type if not specified
        if (string.IsNullOrEmpty(options.DatabaseType))
        {
            var detected = SchemaExtractorFactory.DetectDatabaseType(options.ConnectionString!);
            if (detected == null)
            {
                Console.Error.WriteLine("Error: Could not auto-detect database type. Please specify -d/--database option.");
                return;
            }
            dbType = detected.Value;
            Console.WriteLine($"Auto-detected database type: {dbType}");
        }
        else
        {
            dbType = ParseDatabaseType(options.DatabaseType);
        }
        
        Console.WriteLine($"Schema filter: {options.SchemaFilter ?? "(default)"}");
        Console.WriteLine();
        
        // Extract schema from database
        try
        {
            var extractor = SchemaExtractorFactory.Create(dbType);
            
            Console.WriteLine("Testing database connection...");
            if (!await extractor.TestConnectionAsync(options.ConnectionString!))
            {
                Console.Error.WriteLine("Error: Could not connect to database. Please check your connection string.");
                return;
            }
            Console.WriteLine("Connection successful!");
            Console.WriteLine();
            
            Console.WriteLine("Extracting schema from database...");
            schema = await extractor.ExtractSchemaAsync(options.ConnectionString!, options.SchemaFilter);
            Console.WriteLine($"Found {schema.Tables.Count} tables.");
            Console.WriteLine($"Found {schema.Relationships.Count} relationships.");
            var totalIndexes = schema.Tables.Sum(t => t.Indexes.Count);
            Console.WriteLine($"Found {totalIndexes} indexes.");
        }
        catch (NotSupportedException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error extracting schema: {ex.Message}");
            return;
        }
    }
    else
    {
        // CSV mode - parse schema from files
        Console.WriteLine("Mode: CSV Files");
        
        // Validate input files
        if (options.SchemaFile == null || !options.SchemaFile.Exists)
        {
            Console.Error.WriteLine($"Error: Schema file not found: {options.SchemaFile?.FullName ?? "(not specified)"}");
            return;
        }

        if (options.RelationshipsFile != null && !options.RelationshipsFile.Exists)
        {
            Console.Error.WriteLine($"Error: Relationships file not found: {options.RelationshipsFile.FullName}");
            return;
        }

        if (options.IndexesFile != null && !options.IndexesFile.Exists)
        {
            Console.Error.WriteLine($"Error: Indexes file not found: {options.IndexesFile.FullName}");
            return;
        }

        dbType = ParseDatabaseType(options.DatabaseType!);

        Console.WriteLine($"Schema file: {options.SchemaFile.FullName}");
        Console.WriteLine($"Relationships file: {options.RelationshipsFile?.FullName ?? "None"}");
        Console.WriteLine($"Indexes file: {options.IndexesFile?.FullName ?? "None"}");
        Console.WriteLine();

        // Parse CSV files
        var parser = new CsvSchemaParser();
        
        Console.WriteLine("Parsing schema file...");
        var columns = parser.ParseSchemaFile(options.SchemaFile.FullName);
        Console.WriteLine($"Found {columns.Count} columns.");

        List<RelationshipInfo>? relationships = null;
        if (options.RelationshipsFile != null)
        {
            Console.WriteLine("Parsing relationships file...");
            relationships = parser.ParseRelationshipsFile(options.RelationshipsFile.FullName);
            Console.WriteLine($"Found {relationships.Count} relationships.");
        }

        List<IndexInfo>? indexes = null;
        if (options.IndexesFile != null)
        {
            Console.WriteLine("Parsing indexes file...");
            indexes = parser.ParseIndexesFile(options.IndexesFile.FullName);
            Console.WriteLine($"Found {indexes.Count} indexes.");
        }

        // Build schema
        schema = parser.BuildSchema(columns, relationships, indexes);
        Console.WriteLine($"Found {schema.Tables.Count} tables.");
    }

    var mapType = ParseMappingType(options.MappingType);
    var entType = ParseEntityTypeMode(options.EntityMode);

    Console.WriteLine();
    Console.WriteLine($"Mapping type: {mapType}");
    Console.WriteLine($"Database type: {dbType}");
    Console.WriteLine($"Entity mode: {entType}");
    Console.WriteLine($"Locale: {options.Locale}");
    Console.WriteLine($"Pluralization: {(options.NoPluralizer ? "Disabled" : "Enabled")}");
    Console.WriteLine($"Output directory: {options.OutputDir.FullName}");
    Console.WriteLine($"Namespace: {options.Namespace}");
    Console.WriteLine($"Context name: {options.ContextName}");
    Console.WriteLine();

    // Create generator
    ICodeGenerator generator = mapType switch
    {
        MappingType.EfCore => new EfCoreGenerator(dbType, options.Namespace) { EntityTypeMode = entType },
        MappingType.Dapper => new DapperGenerator(dbType, options.Namespace) { EntityTypeMode = entType },
        _ => throw new InvalidOperationException($"Unknown mapping type: {mapType}")
    };

    // Create output directories
    var entitiesDir = Path.Combine(options.OutputDir.FullName, "Entities");
    var configurationsDir = Path.Combine(options.OutputDir.FullName, "Configurations");
    
    Directory.CreateDirectory(entitiesDir);
    Directory.CreateDirectory(configurationsDir);

    // Generate entities
    Console.WriteLine("Generating entities...");
    var entities = generator.GenerateEntities(schema);
    foreach (var (fileName, content) in entities)
    {
        var filePath = Path.Combine(entitiesDir, fileName);
        await File.WriteAllTextAsync(filePath, content);
        Console.WriteLine($"  Created: {filePath}");
    }

    // Generate configurations
    Console.WriteLine("Generating configurations...");
    var configurations = generator.GenerateConfigurations(schema);
    foreach (var (fileName, content) in configurations)
    {
        var filePath = Path.Combine(configurationsDir, fileName);
        await File.WriteAllTextAsync(filePath, content);
        Console.WriteLine($"  Created: {filePath}");
    }

    // Generate DbContext
    Console.WriteLine("Generating database context...");
    var dbContextContent = generator.GenerateDbContext(schema, options.ContextName);
    var dbContextPath = Path.Combine(options.OutputDir.FullName, $"{options.ContextName}.cs");
    await File.WriteAllTextAsync(dbContextPath, dbContextContent);
    Console.WriteLine($"  Created: {dbContextPath}");

    Console.WriteLine();
    Console.WriteLine("Generation completed successfully!");
}

static string MaskConnectionString(string connectionString)
{
    // Mask password in connection string for display
    var patterns = new[]
    {
        @"(Password\s*=\s*)([^;]+)",
        @"(Pwd\s*=\s*)([^;]+)",
        @"(password\s*=\s*)([^;]+)"
    };
    
    var result = connectionString;
    foreach (var pattern in patterns)
    {
        result = System.Text.RegularExpressions.Regex.Replace(
            result, pattern, "$1****", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
    return result;
}

static DatabaseType ParseDatabaseType(string dbType)
{
    return dbType.ToLowerInvariant() switch
    {
        "mysql" => DatabaseType.MySql,
        "postgre" or "postgresql" => DatabaseType.PostgreSql,
        "sqlserver" or "mssql" => DatabaseType.SqlServer,
        "oracle" => DatabaseType.Oracle,
        "sqlite" => DatabaseType.Sqlite,
        _ => throw new InvalidOperationException($"Unknown database type: {dbType}")
    };
}

static MappingType ParseMappingType(string mapType)
{
    return mapType.ToLowerInvariant() switch
    {
        "efcore" => MappingType.EfCore,
        "dapper" => MappingType.Dapper,
        _ => throw new InvalidOperationException($"Unknown mapping type: {mapType}")
    };
}

static EntityTypeMode ParseEntityTypeMode(string entityMode)
{
    return entityMode.ToLowerInvariant() switch
    {
        "class" or "cls" => EntityTypeMode.Class,
        "record" or "rec" => EntityTypeMode.Record,
        "struct" or "str" => EntityTypeMode.Struct,
        "record_struct" or "rtr" => EntityTypeMode.RecordStruct,
        _ => EntityTypeMode.Class
    };
}

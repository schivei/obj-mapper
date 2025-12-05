using System.CommandLine;
using System.CommandLine.Binding;
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
var configKeyArg = new Argument<string>("key", "Configuration key (locale, namespace, database, type, entity-mode, context, no-pluralize)");
var configValueArg = new Argument<string>("value", "Configuration value");
var configLocalOption = new Option<bool>("--local", "Force local configuration (in .omap folder near solution/project)");

configSetCommand.AddArgument(configKeyArg);
configSetCommand.AddArgument(configValueArg);
configSetCommand.AddOption(configLocalOption);

configSetCommand.SetHandler((string key, string value, bool local) =>
{
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
}, configKeyArg, configValueArg, configLocalOption);

// Config unset command
var configUnsetCommand = new Command("unset", "Remove a configuration value");
var configUnsetKeyArg = new Argument<string>("key", "Configuration key to remove");
var configUnsetLocalOption = new Option<bool>("--local", "Remove from local configuration");

configUnsetCommand.AddArgument(configUnsetKeyArg);
configUnsetCommand.AddOption(configUnsetLocalOption);

configUnsetCommand.SetHandler((string key, bool local) =>
{
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
}, configUnsetKeyArg, configUnsetLocalOption);

// Config list command
var configListCommand = new Command("list", "List all configuration values");
configListCommand.SetHandler(() =>
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
var configPathLocalOption = new Option<bool>("--local", "Show local configuration path");
var configPathGlobalOption = new Option<bool>("--global", "Show global configuration path");

configPathCommand.AddOption(configPathLocalOption);
configPathCommand.AddOption(configPathGlobalOption);

configPathCommand.SetHandler((bool local, bool global) =>
{
    if (local || (!local && !global))
    {
        Console.WriteLine($"Local:  {ConfigurationService.GetLocalConfigPath()}");
    }
    if (global || (!local && !global))
    {
        Console.WriteLine($"Global: {ConfigurationService.GlobalConfigPath}");
    }
}, configPathLocalOption, configPathGlobalOption);

configCommand.AddCommand(configSetCommand);
configCommand.AddCommand(configUnsetCommand);
configCommand.AddCommand(configListCommand);
configCommand.AddCommand(configPathCommand);

rootCommand.AddCommand(configCommand);

// ============================================
// Main generate command options
// ============================================

// Define options - CSV file is now optional
var schemaFileOption = new Argument<FileInfo?>(
    name: "csv",
    description: "CSV file with schema information (optional if using --connection-string)",
    getDefaultValue: () => null)
{
    Arity = ArgumentArity.ZeroOrOne
};

// Connection string option (alternative to CSV)
var connectionStringOption = new Option<string?>(
    aliases: ["--connection-string", "--cs"],
    description: "Database connection string (alternative to CSV files). Schema will be extracted directly from the database.")
{
    IsRequired = false
};

// Schema filter for connection string mode
var schemaFilterOption = new Option<string?>(
    aliases: ["--schema", "-s"],
    description: "Database schema to extract (e.g., 'public' for PostgreSQL, 'dbo' for SQL Server). Used with --connection-string.")
{
    IsRequired = false
};

var mappingTypeOption = new Option<string>(
    aliases: ["-t", "--type"],
    description: "Type of mapping to generate")
{
    IsRequired = true
};
mappingTypeOption.AddValidator(result =>
{
    var value = result.GetValueOrDefault<string>();
    if (value != null && !value.Equals("efcore", StringComparison.OrdinalIgnoreCase) && 
        !value.Equals("dapper", StringComparison.OrdinalIgnoreCase))
    {
        result.ErrorMessage = "Mapping type must be 'efcore' or 'dapper'";
    }
});

var databaseTypeOption = new Option<string?>(
    aliases: ["-d", "--database"],
    description: "Database type (mysql, postgre, sqlserver, oracle, sqlite). Auto-detected when using --connection-string.")
{
    IsRequired = false
};
databaseTypeOption.AddValidator(result =>
{
    var value = result.GetValueOrDefault<string>();
    var validTypes = new[] { "mysql", "postgre", "postgresql", "sqlserver", "mssql", "oracle", "sqlite" };
    if (value != null && !validTypes.Contains(value.ToLowerInvariant()))
    {
        result.ErrorMessage = $"Database type must be one of: {string.Join(", ", validTypes)}";
    }
});

var relationshipsFileOption = new Option<FileInfo?>(
    aliases: ["-f", "--foreignkeys"],
    description: "CSV file with relationships (columns: name, schema_from, schema_to, table_from, table_to, key, foreign)")
{
    IsRequired = false
};

var indexesFileOption = new Option<FileInfo?>(
    aliases: ["-i", "--indexes"],
    description: "CSV file with indexes (columns: schema, table, name, key, type)")
{
    IsRequired = false
};

var outputDirOption = new Option<DirectoryInfo>(
    aliases: ["-o", "--output"],
    description: "Output directory for generated files",
    getDefaultValue: () => new DirectoryInfo(Directory.GetCurrentDirectory()));

var namespaceOption = new Option<string>(
    aliases: ["-n", "--namespace"],
    description: "Namespace for generated classes",
    getDefaultValue: () => config.Namespace ?? "Generated");

var contextNameOption = new Option<string>(
    aliases: ["-c", "--context"],
    description: "Name of the database context class",
    getDefaultValue: () => config.Context ?? "AppDbContext");

var entityModeOption = new Option<string>(
    aliases: ["-e", "--entity-mode"],
    description: "Entity generation mode: class|cls (default), record|rec, struct|str, record_struct|rtr",
    getDefaultValue: () => config.EntityMode ?? "class");
entityModeOption.AddValidator(result =>
{
    var value = result.GetValueOrDefault<string>();
    var validModes = new[] { "class", "cls", "record", "rec", "struct", "str", "record_struct", "rtr" };
    if (value != null && !validModes.Contains(value.ToLowerInvariant()))
    {
        result.ErrorMessage = $"Entity mode must be one of: {string.Join(", ", validModes)}";
    }
});

var localeOption = new Option<string>(
    aliases: ["-l", "--locale"],
    description: $"Locale for pluralization ({string.Join(", ", PluralizerService.SupportedLocales.Take(10))}...)",
    getDefaultValue: () => config.Locale ?? "en-us");
localeOption.AddValidator(result =>
{
    var value = result.GetValueOrDefault<string>();
    if (value != null && !PluralizerService.SupportedLocales.Contains(value.ToLowerInvariant().Replace('_', '-')))
    {
        result.ErrorMessage = $"Locale must be one of: {string.Join(", ", PluralizerService.SupportedLocales)}";
    }
});

var noPluralizeOption = new Option<bool>(
    aliases: ["--no-pluralize"],
    description: "Disable pluralization/singularization",
    getDefaultValue: () => config.NoPluralizer);

// Add validation for required inputs
rootCommand.AddValidator(result =>
{
    var csvFile = result.GetValueForArgument(schemaFileOption);
    var connString = result.GetValueForOption(connectionStringOption);
    var dbType = result.GetValueForOption(databaseTypeOption);
    
    // Must have either CSV file or connection string
    if (csvFile == null && string.IsNullOrEmpty(connString))
    {
        result.ErrorMessage = "Either a CSV file or --connection-string must be provided.";
        return;
    }
    
    // If using CSV, database type is required
    if (csvFile != null && string.IsNullOrEmpty(connString) && string.IsNullOrEmpty(dbType))
    {
        result.ErrorMessage = "Database type (-d/--database) is required when using CSV files.";
    }
});

// Add options to root command
rootCommand.AddArgument(schemaFileOption);
rootCommand.AddOption(connectionStringOption);
rootCommand.AddOption(schemaFilterOption);
rootCommand.AddOption(mappingTypeOption);
rootCommand.AddOption(databaseTypeOption);
rootCommand.AddOption(relationshipsFileOption);
rootCommand.AddOption(indexesFileOption);
rootCommand.AddOption(outputDirOption);
rootCommand.AddOption(namespaceOption);
rootCommand.AddOption(contextNameOption);
rootCommand.AddOption(entityModeOption);
rootCommand.AddOption(localeOption);
rootCommand.AddOption(noPluralizeOption);

// Create binder for options
var optionsBinder = new CommandOptionsBinder(
    schemaFileOption, connectionStringOption, schemaFilterOption,
    mappingTypeOption, databaseTypeOption,
    relationshipsFileOption, indexesFileOption, outputDirOption,
    namespaceOption, contextNameOption, entityModeOption,
    localeOption, noPluralizeOption);

// Set handler
rootCommand.SetHandler(async (CommandOptions options) =>
{
    await ExecuteAsync(options);
}, optionsBinder);

return await rootCommand.InvokeAsync(args);

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

/// <summary>
/// Binder for command options.
/// </summary>
sealed class CommandOptionsBinder : BinderBase<CommandOptions>
{
    private readonly Argument<FileInfo?> _schemaFile;
    private readonly Option<string?> _connectionString;
    private readonly Option<string?> _schemaFilter;
    private readonly Option<string> _mappingType;
    private readonly Option<string?> _databaseType;
    private readonly Option<FileInfo?> _relationshipsFile;
    private readonly Option<FileInfo?> _indexesFile;
    private readonly Option<DirectoryInfo> _outputDir;
    private readonly Option<string> _namespace;
    private readonly Option<string> _contextName;
    private readonly Option<string> _entityMode;
    private readonly Option<string> _locale;
    private readonly Option<bool> _noPluralizer;

    public CommandOptionsBinder(
        Argument<FileInfo?> schemaFile,
        Option<string?> connectionString,
        Option<string?> schemaFilter,
        Option<string> mappingType,
        Option<string?> databaseType,
        Option<FileInfo?> relationshipsFile,
        Option<FileInfo?> indexesFile,
        Option<DirectoryInfo> outputDir,
        Option<string> @namespace,
        Option<string> contextName,
        Option<string> entityMode,
        Option<string> locale,
        Option<bool> noPluralizer)
    {
        _schemaFile = schemaFile;
        _connectionString = connectionString;
        _schemaFilter = schemaFilter;
        _mappingType = mappingType;
        _databaseType = databaseType;
        _relationshipsFile = relationshipsFile;
        _indexesFile = indexesFile;
        _outputDir = outputDir;
        _namespace = @namespace;
        _contextName = contextName;
        _entityMode = entityMode;
        _locale = locale;
        _noPluralizer = noPluralizer;
    }

    protected override CommandOptions GetBoundValue(BindingContext bindingContext)
    {
        return new CommandOptions
        {
            SchemaFile = bindingContext.ParseResult.GetValueForArgument(_schemaFile),
            ConnectionString = bindingContext.ParseResult.GetValueForOption(_connectionString),
            SchemaFilter = bindingContext.ParseResult.GetValueForOption(_schemaFilter),
            MappingType = bindingContext.ParseResult.GetValueForOption(_mappingType)!,
            DatabaseType = bindingContext.ParseResult.GetValueForOption(_databaseType) ?? string.Empty,
            RelationshipsFile = bindingContext.ParseResult.GetValueForOption(_relationshipsFile),
            IndexesFile = bindingContext.ParseResult.GetValueForOption(_indexesFile),
            OutputDir = bindingContext.ParseResult.GetValueForOption(_outputDir)!,
            Namespace = bindingContext.ParseResult.GetValueForOption(_namespace)!,
            ContextName = bindingContext.ParseResult.GetValueForOption(_contextName)!,
            EntityMode = bindingContext.ParseResult.GetValueForOption(_entityMode)!,
            Locale = bindingContext.ParseResult.GetValueForOption(_locale)!,
            NoPluralizer = bindingContext.ParseResult.GetValueForOption(_noPluralizer)
        };
    }
}

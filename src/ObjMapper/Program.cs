using System.CommandLine;
using System.CommandLine.Binding;
using ObjMapper.Generators;
using ObjMapper.Models;
using ObjMapper.Parsers;

// Create root command
var rootCommand = new RootCommand("Database reverse engineering dotnet tool - generates entity mappings from CSV schema files");

// Define options
var schemaFileOption = new Argument<FileInfo>(
    name: "csv",
    description: "CSV file with schema information (columns: schema, table, column, nullable, type, comment)")
{
    Arity = ArgumentArity.ExactlyOne
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

var databaseTypeOption = new Option<string>(
    aliases: ["-d", "--database"],
    description: "Database type (mysql, postgre, sqlserver, oracle, sqlite)")
{
    IsRequired = true
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
    getDefaultValue: () => "Generated");

var contextNameOption = new Option<string>(
    aliases: ["-c", "--context"],
    description: "Name of the database context class",
    getDefaultValue: () => "AppDbContext");

var entityModeOption = new Option<string>(
    aliases: ["-e", "--entity-mode"],
    description: "Entity generation mode: class|cls (default), record|rec, struct|str, record_struct|rtr",
    getDefaultValue: () => "class");
entityModeOption.AddValidator(result =>
{
    var value = result.GetValueOrDefault<string>();
    var validModes = new[] { "class", "cls", "record", "rec", "struct", "str", "record_struct", "rtr" };
    if (value != null && !validModes.Contains(value.ToLowerInvariant()))
    {
        result.ErrorMessage = $"Entity mode must be one of: {string.Join(", ", validModes)}";
    }
});

// Add options to root command
rootCommand.AddArgument(schemaFileOption);
rootCommand.AddOption(mappingTypeOption);
rootCommand.AddOption(databaseTypeOption);
rootCommand.AddOption(relationshipsFileOption);
rootCommand.AddOption(indexesFileOption);
rootCommand.AddOption(outputDirOption);
rootCommand.AddOption(namespaceOption);
rootCommand.AddOption(contextNameOption);
rootCommand.AddOption(entityModeOption);

// Create binder for options
var optionsBinder = new CommandOptionsBinder(
    schemaFileOption, mappingTypeOption, databaseTypeOption,
    relationshipsFileOption, indexesFileOption, outputDirOption,
    namespaceOption, contextNameOption, entityModeOption);

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

    // Validate input files
    if (!options.SchemaFile.Exists)
    {
        Console.Error.WriteLine($"Error: Schema file not found: {options.SchemaFile.FullName}");
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

    // Parse database type
    var dbType = ParseDatabaseType(options.DatabaseType);
    var mapType = ParseMappingType(options.MappingType);
    var entType = ParseEntityTypeMode(options.EntityMode);

    Console.WriteLine($"Schema file: {options.SchemaFile.FullName}");
    Console.WriteLine($"Relationships file: {options.RelationshipsFile?.FullName ?? "None"}");
    Console.WriteLine($"Indexes file: {options.IndexesFile?.FullName ?? "None"}");
    Console.WriteLine($"Mapping type: {mapType}");
    Console.WriteLine($"Database type: {dbType}");
    Console.WriteLine($"Entity mode: {entType}");
    Console.WriteLine($"Output directory: {options.OutputDir.FullName}");
    Console.WriteLine($"Namespace: {options.Namespace}");
    Console.WriteLine($"Context name: {options.ContextName}");
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
    var schema = parser.BuildSchema(columns, relationships, indexes);
    Console.WriteLine($"Found {schema.Tables.Count} tables.");
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
    private readonly Argument<FileInfo> _schemaFile;
    private readonly Option<string> _mappingType;
    private readonly Option<string> _databaseType;
    private readonly Option<FileInfo?> _relationshipsFile;
    private readonly Option<FileInfo?> _indexesFile;
    private readonly Option<DirectoryInfo> _outputDir;
    private readonly Option<string> _namespace;
    private readonly Option<string> _contextName;
    private readonly Option<string> _entityMode;

    public CommandOptionsBinder(
        Argument<FileInfo> schemaFile,
        Option<string> mappingType,
        Option<string> databaseType,
        Option<FileInfo?> relationshipsFile,
        Option<FileInfo?> indexesFile,
        Option<DirectoryInfo> outputDir,
        Option<string> @namespace,
        Option<string> contextName,
        Option<string> entityMode)
    {
        _schemaFile = schemaFile;
        _mappingType = mappingType;
        _databaseType = databaseType;
        _relationshipsFile = relationshipsFile;
        _indexesFile = indexesFile;
        _outputDir = outputDir;
        _namespace = @namespace;
        _contextName = contextName;
        _entityMode = entityMode;
    }

    protected override CommandOptions GetBoundValue(BindingContext bindingContext)
    {
        return new CommandOptions
        {
            SchemaFile = bindingContext.ParseResult.GetValueForArgument(_schemaFile),
            MappingType = bindingContext.ParseResult.GetValueForOption(_mappingType)!,
            DatabaseType = bindingContext.ParseResult.GetValueForOption(_databaseType)!,
            RelationshipsFile = bindingContext.ParseResult.GetValueForOption(_relationshipsFile),
            IndexesFile = bindingContext.ParseResult.GetValueForOption(_indexesFile),
            OutputDir = bindingContext.ParseResult.GetValueForOption(_outputDir)!,
            Namespace = bindingContext.ParseResult.GetValueForOption(_namespace)!,
            ContextName = bindingContext.ParseResult.GetValueForOption(_contextName)!,
            EntityMode = bindingContext.ParseResult.GetValueForOption(_entityMode)!
        };
    }
}

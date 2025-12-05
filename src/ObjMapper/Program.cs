using System.CommandLine;
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
    description: "CSV file with relationships (columns: from, to, keys, foreignkeys)")
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

// Add options to root command
rootCommand.AddArgument(schemaFileOption);
rootCommand.AddOption(mappingTypeOption);
rootCommand.AddOption(databaseTypeOption);
rootCommand.AddOption(relationshipsFileOption);
rootCommand.AddOption(outputDirOption);
rootCommand.AddOption(namespaceOption);
rootCommand.AddOption(contextNameOption);

// Set handler
rootCommand.SetHandler(async (schemaFile, mappingType, databaseType, relationshipsFile, outputDir, namespaceName, contextName) =>
{
    await ExecuteAsync(schemaFile, mappingType, databaseType, relationshipsFile, outputDir, namespaceName, contextName);
}, schemaFileOption, mappingTypeOption, databaseTypeOption, relationshipsFileOption, outputDirOption, namespaceOption, contextNameOption);

return await rootCommand.InvokeAsync(args);

static async Task ExecuteAsync(
    FileInfo schemaFile, 
    string mappingType, 
    string databaseType, 
    FileInfo? relationshipsFile,
    DirectoryInfo outputDir,
    string namespaceName,
    string contextName)
{
    Console.WriteLine("ObjMapper - Database Reverse Engineering Tool");
    Console.WriteLine("=============================================");
    Console.WriteLine();

    // Validate input files
    if (!schemaFile.Exists)
    {
        Console.Error.WriteLine($"Error: Schema file not found: {schemaFile.FullName}");
        return;
    }

    if (relationshipsFile != null && !relationshipsFile.Exists)
    {
        Console.Error.WriteLine($"Error: Relationships file not found: {relationshipsFile.FullName}");
        return;
    }

    // Parse database type
    var dbType = ParseDatabaseType(databaseType);
    var mapType = ParseMappingType(mappingType);

    Console.WriteLine($"Schema file: {schemaFile.FullName}");
    Console.WriteLine($"Relationships file: {relationshipsFile?.FullName ?? "None"}");
    Console.WriteLine($"Mapping type: {mapType}");
    Console.WriteLine($"Database type: {dbType}");
    Console.WriteLine($"Output directory: {outputDir.FullName}");
    Console.WriteLine($"Namespace: {namespaceName}");
    Console.WriteLine($"Context name: {contextName}");
    Console.WriteLine();

    // Parse CSV files
    var parser = new CsvSchemaParser();
    
    Console.WriteLine("Parsing schema file...");
    var columns = parser.ParseSchemaFile(schemaFile.FullName);
    Console.WriteLine($"Found {columns.Count} columns.");

    List<RelationshipInfo>? relationships = null;
    if (relationshipsFile != null)
    {
        Console.WriteLine("Parsing relationships file...");
        relationships = parser.ParseRelationshipsFile(relationshipsFile.FullName);
        Console.WriteLine($"Found {relationships.Count} relationships.");
    }

    // Build schema
    var schema = parser.BuildSchema(columns, relationships);
    Console.WriteLine($"Found {schema.Tables.Count} tables.");
    Console.WriteLine();

    // Create generator
    ICodeGenerator generator = mapType switch
    {
        MappingType.EfCore => new EfCoreGenerator(dbType, namespaceName),
        MappingType.Dapper => new DapperGenerator(dbType, namespaceName),
        _ => throw new InvalidOperationException($"Unknown mapping type: {mapType}")
    };

    // Create output directories
    var entitiesDir = Path.Combine(outputDir.FullName, "Entities");
    var configurationsDir = Path.Combine(outputDir.FullName, "Configurations");
    
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
    var dbContextContent = generator.GenerateDbContext(schema, contextName);
    var dbContextPath = Path.Combine(outputDir.FullName, $"{contextName}.cs");
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

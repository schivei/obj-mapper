using System.CommandLine;
using ObjMapper.Generators;
using ObjMapper.Models;
using ObjMapper.Parsers;
using ObjMapper.Services;
using ObjMapper.Services.ConsoleOutput;

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

var noInferenceOption = new Option<bool>("--no-inference")
{
    Description = "Disable type inference for column type mapping. Type inference is enabled by default and analyzes column names, comments, and data to determine best C# types (e.g., char(36) -> Guid, tinyint with 0/1 values -> bool)."
};

var noChecksOption = new Option<bool>("--no-checks")
{
    Description = "Disable data sampling queries for type verification. When set, type inference uses only column metadata (name, type, comment) which is much faster but may be less accurate for edge cases."
};

var noViewsOption = new Option<bool>("--no-views")
{
    Description = "Disable view mapping. Views will not be extracted from the database."
};

var noProcsOption = new Option<bool>("--no-procs")
{
    Description = "Disable stored procedure mapping. Stored procedures will not be extracted from the database."
};

var noUdfsOption = new Option<bool>("--no-udfs")
{
    Description = "Disable user-defined function mapping. Scalar functions will not be extracted from the database."
};

var noRelOption = new Option<bool>("--no-rel")
{
    Description = "Disable relationship mapping. Foreign key relationships will not be extracted. Cannot be used with --legacy."
};

var legacyOption = new Option<bool>("--legacy")
{
    Description = "Enable legacy relationship inference. Infers relationships from column/table naming patterns when no foreign keys exist. Cannot be used with --no-rel."
};

// Add validation for required inputs
rootCommand.Validators.Add(result =>
{
    var csvFile = result.GetValue(schemaFileArg);
    var connString = result.GetValue(connectionStringOption);
    var dbType = result.GetValue(databaseTypeOption);
    var noRel = result.GetValue(noRelOption);
    var legacy = result.GetValue(legacyOption);
    
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
    
    // --no-rel and --legacy are mutually exclusive
    if (noRel && legacy)
    {
        result.AddError("Options --no-rel and --legacy cannot be used together.");
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
rootCommand.Options.Add(noInferenceOption);
rootCommand.Options.Add(noChecksOption);
rootCommand.Options.Add(noViewsOption);
rootCommand.Options.Add(noProcsOption);
rootCommand.Options.Add(noUdfsOption);
rootCommand.Options.Add(noRelOption);
rootCommand.Options.Add(legacyOption);

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
        NoPluralizer = parseResult.GetValue(noPluralizeOption),
        NoInference = parseResult.GetValue(noInferenceOption),
        NoChecks = parseResult.GetValue(noChecksOption),
        NoViews = parseResult.GetValue(noViewsOption),
        NoProcs = parseResult.GetValue(noProcsOption),
        NoUdfs = parseResult.GetValue(noUdfsOption),
        NoRel = parseResult.GetValue(noRelOption),
        Legacy = parseResult.GetValue(legacyOption)
    };

    await ExecuteAsync(options);
});

return await rootCommand.Parse(args).InvokeAsync();

static async Task ExecuteAsync(CommandOptions options)
{
    using var console = new ConsoleOutputService();
    var result = new ExecutionResult();
    
    try
    {
        console.WriteHeader();
        
        // Configure NamingHelper with locale settings
        NamingHelper.Configure(options.Locale, options.NoPluralizer);

        DatabaseType dbType;
        DatabaseSchema schema;

        if (options.UseConnectionString)
        {
            // Connection string mode - extract schema from database
            console.WriteSection("Database Connection Mode");
            
            var configDict = new Dictionary<string, string>
            {
                ["Connection"] = MaskConnectionString(options.ConnectionString!),
                ["Schema Filter"] = options.SchemaFilter ?? "(default)",
                ["Type Inference"] = options.UseTypeInference ? "Enabled" : "Disabled"
            };
            
            // Try to auto-detect database type if not specified
            if (string.IsNullOrEmpty(options.DatabaseType))
            {
                var detected = SchemaExtractorFactory.DetectDatabaseType(options.ConnectionString!);
                if (detected == null)
                {
                    result.SetCriticalError("Could not auto-detect database type. Please specify -d/--database option.");
                    console.WriteError(result.Errors[^1]);
                    console.WriteSummary(false);
                    return;
                }
                dbType = detected.Value;
                configDict["Database Type"] = $"{dbType} (auto-detected)";
            }
            else
            {
                dbType = ParseDatabaseType(options.DatabaseType);
                configDict["Database Type"] = dbType.ToString();
            }
            
            console.WriteConfiguration(configDict);
            
            // Extract schema from database
            try
            {
                var extractor = SchemaExtractorFactory.Create(dbType);
                
                var connected = await console.WithSpinnerAsync("Testing database connection...", async () =>
                {
                    return await extractor.TestConnectionAsync(options.ConnectionString!);
                });
                
                if (!connected)
                {
                    result.SetCriticalError("Could not connect to database. Please check your connection string.");
                    console.WriteError(result.Errors[^1]);
                    console.WriteSummary(false);
                    return;
                }
                console.WriteSuccess("Database connection successful!");
                
                var extractionOptions = SchemaExtractionOptions.FromCommandOptions(options);
                
                schema = await console.WithSpinnerAsync("Extracting schema from database...", async () =>
                {
                    return await extractor.ExtractSchemaAsync(options.ConnectionString!, extractionOptions);
                });
                
                var totalIndexes = schema.Tables.Sum(t => t.Indexes.Count);
                var stats = new Dictionary<string, int>
                {
                    ["Tables"] = schema.Tables.Count,
                    ["Relationships"] = schema.Relationships.Count,
                    ["Indexes"] = totalIndexes,
                    ["Scalar Functions"] = schema.ScalarFunctions.Count,
                    ["Stored Procedures"] = schema.StoredProcedures.Count
                };
                
                if (options.UseTypeInference)
                {
                    var inferredBooleans = schema.Tables.Sum(t => t.Columns.Count(c => c.InferredAsBoolean));
                    var inferredGuids = schema.Tables.Sum(t => t.Columns.Count(c => c.InferredAsGuid));
                    if (inferredBooleans > 0) stats["Inferred Booleans"] = inferredBooleans;
                    if (inferredGuids > 0) stats["Inferred GUIDs"] = inferredGuids;
                }
                
                console.WriteSection("Schema Statistics");
                console.WriteStatistics(stats);
            }
            catch (NotSupportedException ex)
            {
                result.SetCriticalError(ex.Message);
                console.WriteError(ex.Message);
                console.WriteSummary(false);
                return;
            }
            catch (Exception ex)
            {
                result.SetCriticalError($"Error extracting schema: {ex.Message}");
                console.WriteError($"Error extracting schema: {ex.Message}");
                console.WriteSummary(false);
                return;
            }
        }
        else
        {
            // CSV mode - parse schema from files
            console.WriteSection("CSV Files Mode");
            
            // Validate input files
            if (options.SchemaFile == null || !options.SchemaFile.Exists)
            {
                result.SetCriticalError($"Schema file not found: {options.SchemaFile?.FullName ?? "(not specified)"}");
                console.WriteError(result.Errors[^1]);
                console.WriteSummary(false);
                return;
            }

            if (options.RelationshipsFile != null && !options.RelationshipsFile.Exists)
            {
                result.SetCriticalError($"Relationships file not found: {options.RelationshipsFile.FullName}");
                console.WriteError(result.Errors[^1]);
                console.WriteSummary(false);
                return;
            }

            if (options.IndexesFile != null && !options.IndexesFile.Exists)
            {
                result.SetCriticalError($"Indexes file not found: {options.IndexesFile.FullName}");
                console.WriteError(result.Errors[^1]);
                console.WriteSummary(false);
                return;
            }

            dbType = ParseDatabaseType(options.DatabaseType!);

            var configDict = new Dictionary<string, string>
            {
                ["Schema File"] = options.SchemaFile.FullName,
                ["Relationships File"] = options.RelationshipsFile?.FullName ?? "None",
                ["Indexes File"] = options.IndexesFile?.FullName ?? "None",
                ["Database Type"] = dbType.ToString()
            };
            console.WriteConfiguration(configDict);

            // Parse CSV files
            var parser = new CsvSchemaParser();
            
            var columns = await console.WithSpinnerAsync("Parsing schema file...", () =>
            {
                return Task.FromResult(parser.ParseSchemaFile(options.SchemaFile.FullName));
            });
            console.WriteInfo($"Found {columns.Count} columns");

            List<RelationshipInfo>? relationships = null;
            if (options.RelationshipsFile != null)
            {
                relationships = await console.WithSpinnerAsync("Parsing relationships file...", () =>
                {
                    return Task.FromResult(parser.ParseRelationshipsFile(options.RelationshipsFile.FullName));
                });
                console.WriteInfo($"Found {relationships.Count} relationships");
            }

            List<IndexInfo>? indexes = null;
            if (options.IndexesFile != null)
            {
                indexes = await console.WithSpinnerAsync("Parsing indexes file...", () =>
                {
                    return Task.FromResult(parser.ParseIndexesFile(options.IndexesFile.FullName));
                });
                console.WriteInfo($"Found {indexes.Count} indexes");
            }

            // Build schema
            schema = parser.BuildSchema(columns, relationships, indexes);
            
            var stats = new Dictionary<string, int>
            {
                ["Tables"] = schema.Tables.Count,
                ["Columns"] = columns.Count,
                ["Relationships"] = relationships?.Count ?? 0,
                ["Indexes"] = indexes?.Count ?? 0
            };
            
            console.WriteSection("Schema Statistics");
            console.WriteStatistics(stats);
        }

        var mapType = ParseMappingType(options.MappingType);
        var entType = ParseEntityTypeMode(options.EntityMode);

        console.WriteSection("Generation Settings");
        var genConfigDict = new Dictionary<string, string>
        {
            ["Mapping Type"] = mapType.ToString(),
            ["Database Type"] = dbType.ToString(),
            ["Entity Mode"] = entType.ToString(),
            ["Locale"] = options.Locale,
            ["Pluralization"] = options.NoPluralizer ? "Disabled" : "Enabled",
            ["Output Directory"] = options.OutputDir.FullName,
            ["Namespace"] = options.Namespace,
            ["Context Name"] = options.ContextName
        };
        console.WriteConfiguration(genConfigDict);

        // Create generator
        ICodeGenerator generator = mapType switch
        {
            MappingType.EfCore => new EfCoreGenerator(dbType, options.Namespace, options.UseTypeInference) { EntityTypeMode = entType },
            MappingType.Dapper => new DapperGenerator(dbType, options.Namespace, options.UseTypeInference) { EntityTypeMode = entType },
            _ => throw new InvalidOperationException($"Unknown mapping type: {mapType}")
        };

        // Define output directories (will be created only when writing files)
        var entitiesDir = Path.Combine(options.OutputDir.FullName, "Entities");
        var configurationsDir = Path.Combine(options.OutputDir.FullName, "Configurations");

        console.WriteSection("Generating Code");

        // Generate all code in memory first
        Dictionary<string, string> entities;
        Dictionary<string, string> configurations;
        Dictionary<string, string> scalarFunctions;
        Dictionary<string, string> storedProceduresDict;
        string dbContextContent;
        
        try
        {
            entities = generator.GenerateEntities(schema);
        }
        catch (Exception ex)
        {
            result.AddError($"Error generating entities: {ex.Message}");
            console.WriteWarning($"Error generating entities: {ex.Message}");
            entities = [];
        }
        
        try
        {
            configurations = generator.GenerateConfigurations(schema);
        }
        catch (Exception ex)
        {
            result.AddError($"Error generating configurations: {ex.Message}");
            console.WriteWarning($"Error generating configurations: {ex.Message}");
            configurations = [];
        }
        
        try
        {
            scalarFunctions = generator.GenerateScalarFunctions(schema);
        }
        catch (Exception ex)
        {
            result.AddError($"Error generating scalar functions: {ex.Message}");
            console.WriteWarning($"Error generating scalar functions: {ex.Message}");
            scalarFunctions = [];
        }
        
        try
        {
            storedProceduresDict = generator.GenerateStoredProcedures(schema);
        }
        catch (Exception ex)
        {
            result.AddError($"Error generating stored procedures: {ex.Message}");
            console.WriteWarning($"Error generating stored procedures: {ex.Message}");
            storedProceduresDict = [];
        }
        
        try
        {
            dbContextContent = generator.GenerateDbContext(schema, options.ContextName);
        }
        catch (Exception ex)
        {
            result.SetCriticalError($"Error generating DbContext: {ex.Message}");
            console.WriteError($"Error generating DbContext: {ex.Message}");
            console.WriteSummary(false);
            return;
        }

        // Add all generated files to result
        foreach (var (fileName, content) in entities)
        {
            result.AddGeneratedFile(Path.Combine(entitiesDir, fileName), content);
        }
        
        foreach (var (fileName, content) in configurations)
        {
            result.AddGeneratedFile(Path.Combine(configurationsDir, fileName), content);
        }
        
        result.AddGeneratedFile(Path.Combine(options.OutputDir.FullName, $"{options.ContextName}.cs"), dbContextContent);
        
        foreach (var (fileName, content) in scalarFunctions)
        {
            result.AddGeneratedFile(Path.Combine(options.OutputDir.FullName, fileName), content);
        }
        
        foreach (var (fileName, content) in storedProceduresDict)
        {
            result.AddGeneratedFile(Path.Combine(options.OutputDir.FullName, fileName), content);
        }

        // Write files to disk only if no critical errors
        if (result.CanWriteFiles)
        {
            var totalFiles = result.FilesCount;
            var currentStep = 0;
            
            await console.WithProgressAsync("Writing files...", totalFiles, async (updateProgress) =>
            {
                foreach (var (filePath, content) in result.GeneratedFiles)
                {
                    currentStep++;
                    var fileName = Path.GetFileName(filePath);
                    updateProgress(currentStep, $"Writing: {fileName}");
                    
                    var directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    await File.WriteAllTextAsync(filePath, content);
                }
            });
            
            // Show warnings if any
            if (result.HasWarnings)
            {
                console.WriteSection("Warnings");
                foreach (var warning in result.Warnings)
                {
                    console.WriteWarning(warning);
                }
            }
            
            // Show non-critical errors if any
            if (result.HasErrors)
            {
                console.WriteSection("Errors (non-critical)");
                foreach (var error in result.Errors)
                {
                    console.WriteWarning(error);
                }
            }
            
            console.WriteSummary(true, result.FilesCount);
        }
        else
        {
            console.WriteError("Critical error occurred. No files were written to disk.");
            console.WriteSummary(false);
        }
    }
    catch (Exception ex)
    {
        result.SetCriticalError($"Unexpected error: {ex.Message}");
        console.WriteError($"Unexpected error: {ex.Message}");
        console.WriteSummary(false);
    }
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

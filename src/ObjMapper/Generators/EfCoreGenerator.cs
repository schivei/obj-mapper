using System.Text;
using ObjMapper.Models;
using ObjMapper.Services;

namespace ObjMapper.Generators;

/// <summary>
/// Generates EF Core entities, configurations, and DbContext.
/// </summary>
/// <param name="databaseType">The database type for type mapping.</param>
/// <param name="namespaceName">The namespace for generated classes.</param>
/// <param name="useTypeInference">Whether to use ML-based type inference.</param>
public class EfCoreGenerator(DatabaseType databaseType, string namespaceName = "Generated", bool useTypeInference = false) : ICodeGenerator
{
    private readonly TypeMapper _typeMapper = new(databaseType) { UseTypeInference = useTypeInference };
    private readonly string _namespace = namespaceName;
    private readonly DatabaseType _databaseType = databaseType;

    public EntityTypeMode EntityTypeMode { get; set; } = EntityTypeMode.Class;

    public Dictionary<string, string> GenerateEntities(DatabaseSchema schema)
    {
        var entities = new Dictionary<string, string>();
        var filteredTables = FilterDuplicateTables(schema.Tables);

        foreach (var table in filteredTables)
        {
            var entityName = NamingHelper.ToEntityName(table.Name);
            var code = GenerateEntityClass(table, schema, entityName);
            entities[$"{entityName}.cs"] = code;
        }

        return entities;
    }

    public Dictionary<string, string> GenerateConfigurations(DatabaseSchema schema)
    {
        var configurations = new Dictionary<string, string>();
        var filteredTables = FilterDuplicateTables(schema.Tables);

        foreach (var table in filteredTables)
        {
            var entityName = NamingHelper.ToEntityName(table.Name);
            var code = GenerateConfigurationClass(table, schema, entityName);
            configurations[$"{entityName}Configuration.cs"] = code;
        }

        return configurations;
    }

    public string GenerateDbContext(DatabaseSchema schema, string contextName)
    {
        var sb = new StringBuilder();
        var filteredTables = FilterDuplicateTables(schema.Tables);
        
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        
        // Add System.Reflection using if there are scalar functions
        if (schema.ScalarFunctions.Count > 0)
        {
            sb.AppendLine("using System.Reflection;");
        }
        
        sb.AppendLine();
        sb.AppendLine($"namespace {_namespace};");
        sb.AppendLine();
        sb.AppendLine($"public partial class {contextName} : DbContext");
        sb.AppendLine("{");
        sb.AppendLine($"    public {contextName}(DbContextOptions<{contextName}> options) : base(options)");
        sb.AppendLine("    {");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate DbSet properties
        foreach (var table in filteredTables)
        {
            var entityName = NamingHelper.ToEntityName(table.Name);
            var collectionName = NamingHelper.ToCollectionName(entityName);
            sb.AppendLine($"    public DbSet<{entityName}> {collectionName} {{ get; set; }} = null!;");
        }

        sb.AppendLine();
        sb.AppendLine("    protected override void OnModelCreating(ModelBuilder modelBuilder)");
        sb.AppendLine("    {");
        sb.AppendLine("        base.OnModelCreating(modelBuilder);");
        sb.AppendLine();

        // Apply configurations
        foreach (var table in filteredTables)
        {
            var entityName = NamingHelper.ToEntityName(table.Name);
            sb.AppendLine($"        modelBuilder.ApplyConfiguration(new {entityName}Configuration());");
        }

        // Register scalar functions
        if (schema.ScalarFunctions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("        // Register scalar functions");
            foreach (var func in schema.ScalarFunctions)
            {
                var methodName = NamingHelper.ToPascalCase(func.Name);
                sb.AppendLine($"        modelBuilder.HasDbFunction(typeof(DbFunctions).GetMethod(nameof(DbFunctions.{methodName}), BindingFlags.Public | BindingFlags.Static)");
                sb.AppendLine($"            ?? throw new InvalidOperationException(\"Scalar function method '{methodName}' not found in DbFunctions class.\"));");
            }
        }

        sb.AppendLine();
        sb.AppendLine("        OnModelCreatingPartial(modelBuilder);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);");
        sb.AppendLine("}");

        return sb.ToString();
    }

    public Dictionary<string, string> GenerateScalarFunctions(DatabaseSchema schema)
    {
        var functions = new Dictionary<string, string>();
        
        if (schema.ScalarFunctions.Count == 0)
            return functions;
        
        var code = GenerateDbFunctionsClass(schema);
        functions["DbFunctions.cs"] = code;
        
        return functions;
    }
    
    private string GenerateDbFunctionsClass(DatabaseSchema schema)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_namespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Database scalar function mappings for EF Core.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static partial class DbFunctions");
        sb.AppendLine("{");
        
        foreach (var func in schema.ScalarFunctions)
        {
            var methodName = NamingHelper.ToPascalCase(func.Name);
            var returnType = _typeMapper.MapToCSharpType(func.ReturnType, true);
            
            // Generate XML comment
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Maps to database function {func.FullName}.");
            sb.AppendLine("    /// </summary>");
            
            // Generate DbFunction attribute
            if (!string.IsNullOrEmpty(func.Schema))
            {
                sb.AppendLine($"    [DbFunction(\"{func.Name}\", \"{func.Schema}\")]");
            }
            else
            {
                sb.AppendLine($"    [DbFunction(\"{func.Name}\")]");
            }
            
            // Generate method signature
            var parameters = func.Parameters
                .OrderBy(p => p.OrdinalPosition)
                .Select(p => $"{_typeMapper.MapToCSharpType(p.DataType, true)} {NamingHelper.ToCamelCase(p.Name)}")
                .ToList();
            
            var parameterList = string.Join(", ", parameters);
            
            sb.AppendLine($"    public static {returnType} {methodName}({parameterList})");
            sb.AppendLine("    {");
            sb.AppendLine("        throw new NotSupportedException(\"This method can only be used in LINQ-to-Entities queries.\");");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }

    public Dictionary<string, string> GenerateStoredProcedures(DatabaseSchema schema)
    {
        var procedures = new Dictionary<string, string>();
        
        if (schema.StoredProcedures.Count == 0)
            return procedures;
        
        // Generate result types for tabular procedures
        var resultTypes = GenerateStoredProcedureResultTypes(schema);
        if (!string.IsNullOrEmpty(resultTypes))
        {
            procedures["StoredProcedureResultTypes.cs"] = resultTypes;
        }
        
        // Generate the stored procedures extension methods
        var code = GenerateStoredProceduresClass(schema);
        procedures["StoredProcedures.cs"] = code;
        
        return procedures;
    }
    
    private string GenerateStoredProceduresClass(DatabaseSchema schema)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine(GetDbClientUsing());
        sb.AppendLine("using System.Data;");
        sb.AppendLine("using System.Data.Common;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_namespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Extension methods for executing stored procedures.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static partial class StoredProcedureExtensions");
        sb.AppendLine("{");
        
        // Add helper method for creating parameters
        GenerateCreateParameterHelper(sb);
        
        foreach (var proc in schema.StoredProcedures)
        {
            GenerateStoredProcedureMethod(sb, proc);
        }
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }

    private void GenerateCreateParameterHelper(StringBuilder sb)
    {
        var dbParamType = GetDbParameterType();
        sb.AppendLine($"    private static {dbParamType} CreateParameter(string name, object? value)");
        sb.AppendLine("    {");
        sb.AppendLine($"        return new {dbParamType}(name, value ?? DBNull.Value);");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private string GetDbClientUsing()
    {
        return _databaseType switch
        {
            DatabaseType.MySql => "using MySqlConnector;",
            DatabaseType.PostgreSql => "using Npgsql;",
            DatabaseType.SqlServer => "using Microsoft.Data.SqlClient;",
            DatabaseType.Oracle => "using Oracle.ManagedDataAccess.Client;",
            DatabaseType.Sqlite => "using Microsoft.Data.Sqlite;",
            _ => "using Microsoft.Data.SqlClient;"
        };
    }

    private string GetDbParameterType()
    {
        return _databaseType switch
        {
            DatabaseType.MySql => "MySqlParameter",
            DatabaseType.PostgreSql => "NpgsqlParameter",
            DatabaseType.SqlServer => "SqlParameter",
            DatabaseType.Oracle => "OracleParameter",
            DatabaseType.Sqlite => "SqliteParameter",
            _ => "SqlParameter"
        };
    }
    
    private void GenerateStoredProcedureMethod(StringBuilder sb, StoredProcedureInfo proc)
    {
        var methodName = NamingHelper.ToPascalCase(proc.Name);
        var asyncMethodName = $"{methodName}Async";
        
        // Generate XML comment
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Executes stored procedure {proc.FullName}.");
        sb.AppendLine("    /// </summary>");
        
        // Build parameters
        var inputParams = proc.Parameters.Where(p => !p.IsOutput).OrderBy(p => p.OrdinalPosition).ToList();
        var outputParams = proc.Parameters.Where(p => p.IsOutput).OrderBy(p => p.OrdinalPosition).ToList();
        
        var methodParams = new List<string> { "this DbContext context" };
        methodParams.AddRange(inputParams.Select(p => 
            $"{_typeMapper.MapToCSharpType(p.DataType, true)} {NamingHelper.ToCamelCase(p.Name)}"));
        
        var methodParamList = string.Join(", ", methodParams);
        
        switch (proc.OutputType)
        {
            case StoredProcedureOutputType.None:
                GenerateVoidProcedure(sb, proc, methodName, asyncMethodName, methodParamList, inputParams);
                break;
            case StoredProcedureOutputType.Scalar:
                GenerateScalarProcedure(sb, proc, methodName, asyncMethodName, methodParamList, inputParams, outputParams);
                break;
            case StoredProcedureOutputType.Tabular:
                GenerateTabularProcedure(sb, proc, methodName, asyncMethodName, methodParamList, inputParams);
                break;
        }
    }
    
    private void GenerateVoidProcedure(StringBuilder sb, StoredProcedureInfo proc, string methodName, 
        string asyncMethodName, string methodParamList, List<StoredProcedureParameter> inputParams)
    {
        // Sync method
        sb.AppendLine($"    public static void {methodName}({methodParamList})");
        sb.AppendLine("    {");
        GenerateExecuteSql(sb, proc, inputParams, "ExecuteSqlRaw");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Async method
        sb.AppendLine($"    public static async Task {asyncMethodName}({methodParamList}, CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        GenerateExecuteSql(sb, proc, inputParams, "ExecuteSqlRawAsync", true);
        sb.AppendLine("    }");
        sb.AppendLine();
    }
    
    private void GenerateScalarProcedure(StringBuilder sb, StoredProcedureInfo proc, string methodName, 
        string asyncMethodName, string methodParamList, List<StoredProcedureParameter> inputParams,
        List<StoredProcedureParameter> outputParams)
    {
        var outputParam = outputParams.FirstOrDefault();
        var returnType = outputParam != null 
            ? _typeMapper.MapToCSharpType(outputParam.DataType, true) 
            : "object?";
        
        // Sync method
        sb.AppendLine($"    public static {returnType} {methodName}({methodParamList})");
        sb.AppendLine("    {");
        GenerateScalarExecute(sb, proc, inputParams, outputParam, returnType);
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Async method
        sb.AppendLine($"    public static async Task<{returnType}> {asyncMethodName}({methodParamList}, CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        GenerateScalarExecuteAsync(sb, proc, inputParams, outputParam, returnType);
        sb.AppendLine("    }");
        sb.AppendLine();
    }
    
    private void GenerateTabularProcedure(StringBuilder sb, StoredProcedureInfo proc, string methodName, 
        string asyncMethodName, string methodParamList, List<StoredProcedureParameter> inputParams)
    {
        var resultTypeName = $"{NamingHelper.ToPascalCase(proc.Name)}Result";
        
        // Sync method
        sb.AppendLine($"    public static List<{resultTypeName}> {methodName}({methodParamList})");
        sb.AppendLine("    {");
        GenerateTabularExecute(sb, proc, inputParams, resultTypeName);
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Async method  
        sb.AppendLine($"    public static async Task<List<{resultTypeName}>> {asyncMethodName}({methodParamList}, CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        GenerateTabularExecuteAsync(sb, proc, inputParams, resultTypeName);
        sb.AppendLine("    }");
        sb.AppendLine();
    }
    
    private void GenerateExecuteSql(StringBuilder sb, StoredProcedureInfo proc, 
        List<StoredProcedureParameter> inputParams, string executeMethod, bool isAsync = false)
    {
        var paramNames = inputParams.Select(p => $"@{p.Name}").ToList();
        var sql = GetStoredProcedureCallSyntax(proc.FullName, paramNames);
        
        sb.AppendLine($"        var sql = \"{sql}\";");
        
        if (inputParams.Count > 0)
        {
            sb.AppendLine("        var parameters = new object[]");
            sb.AppendLine("        {");
            foreach (var param in inputParams)
            {
                var camelName = NamingHelper.ToCamelCase(param.Name);
                sb.AppendLine($"            CreateParameter(\"@{param.Name}\", {camelName}),");
            }
            sb.AppendLine("        };");
            var awaitPrefix = isAsync ? "await " : "";
            var asyncSuffix = isAsync ? ", cancellationToken" : "";
            sb.AppendLine($"        {awaitPrefix}context.Database.{executeMethod}(sql, parameters{asyncSuffix});");
        }
        else
        {
            var awaitPrefix = isAsync ? "await " : "";
            var asyncSuffix = isAsync ? ", cancellationToken" : "";
            sb.AppendLine($"        {awaitPrefix}context.Database.{executeMethod}(sql{asyncSuffix});");
        }
    }

    private string GetStoredProcedureCallSyntax(string fullName, List<string> paramNames)
    {
        var paramList = string.Join(", ", paramNames);
        
        return _databaseType switch
        {
            DatabaseType.MySql => $"CALL {fullName}({paramList})",
            DatabaseType.PostgreSql => $"CALL {fullName}({paramList})",
            DatabaseType.Oracle => $"BEGIN {fullName}({paramList}); END;",
            _ => $"EXEC {fullName} {paramList}" // SQL Server, SQLite
        };
    }
    
    private void GenerateScalarExecute(StringBuilder sb, StoredProcedureInfo proc,
        List<StoredProcedureParameter> inputParams, StoredProcedureParameter? outputParam, string returnType)
    {
        var dbParamType = GetDbParameterType();
        
        sb.AppendLine("        using var connection = context.Database.GetDbConnection();");
        sb.AppendLine("        using var command = connection.CreateCommand();");
        sb.AppendLine($"        command.CommandText = \"{proc.FullName}\";");
        sb.AppendLine("        command.CommandType = CommandType.StoredProcedure;");
        sb.AppendLine();
        
        foreach (var param in inputParams)
        {
            var camelName = NamingHelper.ToCamelCase(param.Name);
            sb.AppendLine($"        command.Parameters.Add(CreateParameter(\"@{param.Name}\", {camelName}));");
        }
        
        if (outputParam != null)
        {
            sb.AppendLine($"        var outputParameter = new {dbParamType}(\"@{outputParam.Name}\", DBNull.Value);");
            sb.AppendLine("        outputParameter.Direction = ParameterDirection.Output;");
            sb.AppendLine("        command.Parameters.Add(outputParameter);");
        }
        
        sb.AppendLine();
        sb.AppendLine("        connection.Open();");
        sb.AppendLine("        command.ExecuteNonQuery();");
        sb.AppendLine();
        
        if (outputParam != null)
        {
            sb.AppendLine($"        return ({returnType})outputParameter.Value;");
        }
        else
        {
            sb.AppendLine("        return default;");
        }
    }
    
    private void GenerateScalarExecuteAsync(StringBuilder sb, StoredProcedureInfo proc,
        List<StoredProcedureParameter> inputParams, StoredProcedureParameter? outputParam, string returnType)
    {
        var dbParamType = GetDbParameterType();
        
        sb.AppendLine("        await using var connection = context.Database.GetDbConnection();");
        sb.AppendLine("        await using var command = connection.CreateCommand();");
        sb.AppendLine($"        command.CommandText = \"{proc.FullName}\";");
        sb.AppendLine("        command.CommandType = CommandType.StoredProcedure;");
        sb.AppendLine();
        
        foreach (var param in inputParams)
        {
            var camelName = NamingHelper.ToCamelCase(param.Name);
            sb.AppendLine($"        command.Parameters.Add(CreateParameter(\"@{param.Name}\", {camelName}));");
        }
        
        if (outputParam != null)
        {
            sb.AppendLine($"        var outputParameter = new {dbParamType}(\"@{outputParam.Name}\", DBNull.Value);");
            sb.AppendLine("        outputParameter.Direction = ParameterDirection.Output;");
            sb.AppendLine("        command.Parameters.Add(outputParameter);");
        }
        
        sb.AppendLine();
        sb.AppendLine("        await connection.OpenAsync(cancellationToken);");
        sb.AppendLine("        await command.ExecuteNonQueryAsync(cancellationToken);");
        sb.AppendLine();
        
        if (outputParam != null)
        {
            sb.AppendLine($"        return ({returnType})outputParameter.Value;");
        }
        else
        {
            sb.AppendLine("        return default;");
        }
    }
    
    private void GenerateTabularExecute(StringBuilder sb, StoredProcedureInfo proc, 
        List<StoredProcedureParameter> inputParams, string resultTypeName)
    {
        var paramNames = inputParams.Select(p => $"@{p.Name}").ToList();
        var sql = GetStoredProcedureCallSyntax(proc.FullName, paramNames);
        
        sb.AppendLine($"        var sql = \"{sql}\";");
        
        if (inputParams.Count > 0)
        {
            sb.AppendLine("        var parameters = new object[]");
            sb.AppendLine("        {");
            foreach (var param in inputParams)
            {
                var camelName = NamingHelper.ToCamelCase(param.Name);
                sb.AppendLine($"            CreateParameter(\"@{param.Name}\", {camelName}),");
            }
            sb.AppendLine("        };");
            sb.AppendLine($"        return context.Set<{resultTypeName}>().FromSqlRaw(sql, parameters).ToList();");
        }
        else
        {
            sb.AppendLine($"        return context.Set<{resultTypeName}>().FromSqlRaw(sql).ToList();");
        }
    }
    
    private void GenerateTabularExecuteAsync(StringBuilder sb, StoredProcedureInfo proc, 
        List<StoredProcedureParameter> inputParams, string resultTypeName)
    {
        var paramNames = inputParams.Select(p => $"@{p.Name}").ToList();
        var sql = GetStoredProcedureCallSyntax(proc.FullName, paramNames);
        
        sb.AppendLine($"        var sql = \"{sql}\";");
        
        if (inputParams.Count > 0)
        {
            sb.AppendLine("        var parameters = new object[]");
            sb.AppendLine("        {");
            foreach (var param in inputParams)
            {
                var camelName = NamingHelper.ToCamelCase(param.Name);
                sb.AppendLine($"            CreateParameter(\"@{param.Name}\", {camelName}),");
            }
            sb.AppendLine("        };");
            sb.AppendLine($"        return await context.Set<{resultTypeName}>().FromSqlRaw(sql, parameters).ToListAsync(cancellationToken);");
        }
        else
        {
            sb.AppendLine($"        return await context.Set<{resultTypeName}>().FromSqlRaw(sql).ToListAsync(cancellationToken);");
        }
    }
    
    private string GenerateStoredProcedureResultTypes(DatabaseSchema schema)
    {
        var tabularProcs = schema.StoredProcedures
            .Where(p => p.OutputType == StoredProcedureOutputType.Tabular && p.ResultColumns.Count > 0)
            .ToList();
            
        if (tabularProcs.Count == 0)
            return string.Empty;
        
        var sb = new StringBuilder();
        
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_namespace};");
        sb.AppendLine();
        
        foreach (var proc in tabularProcs)
        {
            var typeName = $"{NamingHelper.ToPascalCase(proc.Name)}Result";
            
            sb.AppendLine($"/// <summary>");
            sb.AppendLine($"/// Result type for stored procedure {proc.FullName}.");
            sb.AppendLine($"/// </summary>");
            sb.AppendLine($"[Keyless]");
            sb.AppendLine($"public class {typeName}");
            sb.AppendLine("{");
            
            foreach (var col in proc.ResultColumns.OrderBy(c => c.OrdinalPosition))
            {
                var propName = NamingHelper.ToPascalCase(col.Name);
                var propType = _typeMapper.MapToCSharpType(col.DataType, col.IsNullable);
                
                sb.AppendLine($"    public {propType} {propName} {{ get; set; }}");
            }
            
            sb.AppendLine("}");
            sb.AppendLine();
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// Filters tables to remove duplicates that would generate the same entity name.
    /// When duplicates are found, the pluralized version is kept (preference for table names like "users" over "user").
    /// Other duplicates are removed entirely.
    /// </summary>
    /// <param name="tables">List of tables to filter.</param>
    /// <returns>Filtered list of tables with no duplicates.</returns>
    private static List<TableInfo> FilterDuplicateTables(List<TableInfo> tables)
    {
        // Group tables by their entity name (which singularizes the table name)
        // and also by their collection name (pluralized form)
        var entityGroups = tables
            .GroupBy(t => NamingHelper.ToEntityName(t.Name), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<TableInfo>();

        foreach (var group in entityGroups)
        {
            if (group.Count() == 1)
            {
                // No duplicate, just add the table
                result.Add(group.First());
            }
            else
            {
                // Multiple tables map to the same entity name
                // Prefer the pluralized version (the one where table name != entity name)
                // This means "users" is preferred over "user" since ToEntityName("users") = "User"
                var pluralizedTable = group.FirstOrDefault(t => 
                    !t.Name.Equals(NamingHelper.ToEntityName(t.Name), StringComparison.OrdinalIgnoreCase));
                
                if (pluralizedTable != null)
                {
                    result.Add(pluralizedTable);
                }
                else
                {
                    // If no pluralized version found, just take the first one
                    result.Add(group.First());
                }
            }
        }

        // Second pass: check for collection name duplicates and remove them
        var collectionGroups = result
            .GroupBy(t => NamingHelper.ToCollectionName(NamingHelper.ToEntityName(t.Name)), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var finalResult = new List<TableInfo>();

        foreach (var group in collectionGroups)
        {
            if (group.Count() == 1)
            {
                finalResult.Add(group.First());
            }
            else
            {
                // Multiple tables would generate the same collection name
                // Prefer the one with pluralized table name
                var pluralizedTable = group.FirstOrDefault(t => 
                    !t.Name.Equals(NamingHelper.ToEntityName(t.Name), StringComparison.OrdinalIgnoreCase));
                
                if (pluralizedTable != null)
                {
                    finalResult.Add(pluralizedTable);
                }
                else
                {
                    finalResult.Add(group.First());
                }
            }
        }

        return finalResult;
    }

    /// <summary>
    /// Builds a mapping from column names to unique property names for a given table.
    /// This ensures consistent property names are used across entity and configuration generation.
    /// </summary>
    private static Dictionary<string, string> BuildColumnToPropertyMap(TableInfo table, string entityName)
    {
        var propertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { entityName };
        var columnToProperty = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var column in table.Columns)
        {
            var propertyName = GetUniquePropertyName(NamingHelper.ToPascalCase(column.Column), entityName, propertyNames);
            propertyNames.Add(propertyName);
            columnToProperty[column.Column] = propertyName;
        }

        return columnToProperty;
    }

    private string GenerateEntityClass(TableInfo table, DatabaseSchema schema, string entityName)
    {
        var sb = new StringBuilder();

        // Build column to property mapping
        var columnToProperty = BuildColumnToPropertyMap(table, entityName);
        var propertyNames = new HashSet<string>(columnToProperty.Values, StringComparer.OrdinalIgnoreCase);
        propertyNames.Add(entityName);

        sb.AppendLine($"namespace {_namespace};");
        sb.AppendLine();
        
        // Add XML comment if any column has a comment
        if (table.Columns.Exists(c => !string.IsNullOrEmpty(c.Comment)))
        {
            sb.AppendLine("/// <summary>");
            sb.AppendLine($"/// Entity for table {table.Name}.");
            sb.AppendLine("/// </summary>");
        }

        // Generate the type declaration based on EntityTypeMode
        var typeKeyword = GetTypeKeyword();
        sb.AppendLine($"public partial {typeKeyword} {entityName}");
        sb.AppendLine("{");

        // Generate properties for columns
        foreach (var column in table.Columns)
        {
            var propertyName = columnToProperty[column.Column];
            var csharpType = _typeMapper.MapColumnToCSharpType(column);

            // Add comment if present
            if (!string.IsNullOrEmpty(column.Comment))
            {
                AppendXmlComment(sb, column.Comment, "    ");
            }

            if (csharpType == "string" && !column.Nullable)
            {
                sb.AppendLine($"    public {csharpType} {propertyName} {{ get; set; }} = string.Empty;");
            }
            else if (csharpType == "byte[]" && !column.Nullable)
            {
                sb.AppendLine($"    public {csharpType} {propertyName} {{ get; set; }} = [];");
            }
            else
            {
                sb.AppendLine($"    public {csharpType} {propertyName} {{ get; set; }}");
            }
        }

        // Generate navigation properties for outgoing relationships (foreign keys)
        foreach (var rel in table.OutgoingRelationships)
        {
            var relatedEntityName = NamingHelper.ToEntityName(rel.TableTo);
            var propertyName = GetUniquePropertyName(relatedEntityName, entityName, propertyNames);
            propertyNames.Add(propertyName);
            
            sb.AppendLine();
            sb.AppendLine($"    public virtual {relatedEntityName}? {propertyName} {{ get; set; }}");
        }

        // Generate navigation properties for incoming relationships (inverse navigation)
        foreach (var rel in table.IncomingRelationships)
        {
            var relatedEntityName = NamingHelper.ToEntityName(rel.TableFrom);
            var collectionName = GetUniquePropertyName(NamingHelper.ToCollectionName(relatedEntityName), entityName, propertyNames);
            propertyNames.Add(collectionName);
            
            sb.AppendLine();
            sb.AppendLine($"    public virtual ICollection<{relatedEntityName}> {collectionName} {{ get; set; }} = [];");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GenerateConfigurationClass(TableInfo table, DatabaseSchema schema, string entityName)
    {
        var sb = new StringBuilder();
        
        // Build column to property mapping (same logic as entity generation)
        var columnToProperty = BuildColumnToPropertyMap(table, entityName);
        var propertyNames = new HashSet<string>(columnToProperty.Values, StringComparer.OrdinalIgnoreCase);
        propertyNames.Add(entityName);

        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine("using Microsoft.EntityFrameworkCore.Metadata.Builders;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_namespace};");
        sb.AppendLine();
        sb.AppendLine($"public partial class {entityName}Configuration : IEntityTypeConfiguration<{entityName}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public void Configure(EntityTypeBuilder<{entityName}> builder)");
        sb.AppendLine("    {");

        // Table mapping
        if (!string.IsNullOrEmpty(table.Schema))
        {
            sb.AppendLine($"        builder.ToTable(\"{table.Name}\", \"{table.Schema}\");");
        }
        else
        {
            sb.AppendLine($"        builder.ToTable(\"{table.Name}\");");
        }

        sb.AppendLine();

        // Detect and configure primary key
        var potentialPrimaryKeys = table.Columns
            .Where(c => c.Column.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                       c.Column.EndsWith("_id", StringComparison.OrdinalIgnoreCase) && 
                       c.Column.StartsWith(table.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (potentialPrimaryKeys.Count > 0)
        {
            var pkColumn = potentialPrimaryKeys[0];
            var pkPropertyName = columnToProperty[pkColumn.Column];
            sb.AppendLine($"        builder.HasKey(e => e.{pkPropertyName});");
            sb.AppendLine();
        }

        // Configure each column
        foreach (var column in table.Columns)
        {
            var propertyName = columnToProperty[column.Column];
            
            sb.Append($"        builder.Property(e => e.{propertyName})");
            sb.AppendLine();
            sb.AppendLine($"            .HasColumnName(\"{column.Column}\")");

            if (!column.Nullable)
            {
                sb.AppendLine("            .IsRequired()");
            }

            // Add column type for specific database types
            if (NeedsColumnType(column.Type))
            {
                sb.AppendLine($"            .HasColumnType(\"{column.Type}\")");
            }

            if (!string.IsNullOrEmpty(column.Comment))
            {
                sb.AppendLine($"            .HasComment(\"{EscapeString(column.Comment)}\")");
            }

            // Remove the last newline and add semicolon
            sb.Length -= Environment.NewLine.Length;
            sb.AppendLine(";");
            sb.AppendLine();
        }

        // Configure indexes
        foreach (var index in table.Indexes)
        {
            var keyProperties = index.Keys.Select(k => 
            {
                // Use the column to property mapping
                if (columnToProperty.TryGetValue(k, out var propName))
                {
                    return $"e.{propName}";
                }
                return $"e.{NamingHelper.ToPascalCase(k)}";
            });
            var keysExpression = index.Keys.Length == 1 
                ? keyProperties.First() 
                : $"new {{ {string.Join(", ", keyProperties)} }}";
            
            sb.Append($"        builder.HasIndex(e => {keysExpression})");
            
            if (!string.IsNullOrEmpty(index.Name))
            {
                sb.AppendLine();
                sb.Append($"            .HasDatabaseName(\"{index.Name}\")");
            }
            
            if (index.IsUnique)
            {
                sb.AppendLine();
                sb.Append("            .IsUnique()");
            }
            
            sb.AppendLine(";");
            sb.AppendLine();
        }

        // Configure relationships
        foreach (var rel in table.OutgoingRelationships)
        {
            var relatedEntityName = NamingHelper.ToEntityName(rel.TableTo);
            var navPropertyName = GetUniquePropertyName(relatedEntityName, entityName, propertyNames);
            var inverseCollectionName = NamingHelper.ToCollectionName(entityName);

            // Handle composite keys
            if (rel.ForeignKeys.Length == 1)
            {
                // Use the column to property mapping for foreign key
                var foreignKeyProperty = columnToProperty.TryGetValue(rel.ForeignKeys[0], out var fkProp) 
                    ? fkProp 
                    : NamingHelper.ToPascalCase(rel.ForeignKeys[0]);
                var principalKeyProperty = NamingHelper.ToPascalCase(rel.Keys[0]);

                sb.AppendLine($"        builder.HasOne(e => e.{navPropertyName})");
                sb.AppendLine($"            .WithMany(e => e.{inverseCollectionName})");
                sb.AppendLine($"            .HasForeignKey(e => e.{foreignKeyProperty})");
                sb.AppendLine($"            .HasPrincipalKey(e => e.{principalKeyProperty});");
            }
            else
            {
                var foreignKeyProperties = rel.ForeignKeys.Select(k => 
                {
                    var propName = columnToProperty.TryGetValue(k, out var fkProp) 
                        ? fkProp 
                        : NamingHelper.ToPascalCase(k);
                    return $"e.{propName}";
                });
                var principalKeyProperties = rel.Keys.Select(k => $"e.{NamingHelper.ToPascalCase(k)}");

                sb.AppendLine($"        builder.HasOne(e => e.{navPropertyName})");
                sb.AppendLine($"            .WithMany(e => e.{inverseCollectionName})");
                sb.AppendLine($"            .HasForeignKey(e => new {{ {string.Join(", ", foreignKeyProperties)} }})");
                sb.AppendLine($"            .HasPrincipalKey(e => new {{ {string.Join(", ", principalKeyProperties)} }});");
            }
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GetTypeKeyword()
    {
        return EntityTypeMode switch
        {
            EntityTypeMode.Class => "class",
            EntityTypeMode.Record => "record",
            EntityTypeMode.Struct => "struct",
            EntityTypeMode.RecordStruct => "record struct",
            _ => "class"
        };
    }

    /// <summary>
    /// Gets a unique property name that doesn't conflict with the entity name or other properties.
    /// </summary>
    /// <param name="proposedName">The proposed property name.</param>
    /// <param name="entityName">The name of the containing entity.</param>
    /// <param name="existingNames">Set of existing property names (including entity name).</param>
    /// <returns>A unique property name.</returns>
    private static string GetUniquePropertyName(string proposedName, string entityName, HashSet<string> existingNames)
    {
        // If the property name equals the entity name, add a suffix
        if (proposedName.Equals(entityName, StringComparison.OrdinalIgnoreCase))
        {
            proposedName = $"{proposedName}Value";
        }
        
        // If there's still a conflict with existing names, add numeric suffix
        var baseName = proposedName;
        var counter = 1;
        while (existingNames.Contains(proposedName))
        {
            proposedName = $"{baseName}{counter}";
            counter++;
        }
        
        return proposedName;
    }

    private static bool NeedsColumnType(string dbType)
    {
        var type = dbType.ToUpperInvariant();
        return type.Contains('(') || 
               type.Contains("DECIMAL") || 
               type.Contains("NUMERIC") ||
               type.Contains("VARCHAR") ||
               type.Contains("NVARCHAR");
    }

    private static string EscapeString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r\n", "\\n")
            .Replace("\r", "\\n")
            .Replace("\n", "\\n");
    }

    /// <summary>
    /// Appends a properly formatted XML documentation comment to the StringBuilder.
    /// Handles multi-line comments by prefixing each line with the XML comment syntax.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="comment">The comment text (may contain newlines).</param>
    /// <param name="indent">The indentation to use (e.g., "    " for 4 spaces).</param>
    private static void AppendXmlComment(StringBuilder sb, string comment, string indent)
    {
        // Normalize line endings and split into lines
        var lines = comment
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Split('\n')
            .Select(line => EscapeXmlComment(line.Trim()))
            .Where(line => !string.IsNullOrEmpty(line))
            .ToArray();

        sb.AppendLine($"{indent}/// <summary>");
        
        foreach (var line in lines)
        {
            sb.AppendLine($"{indent}/// {line}");
        }
        
        sb.AppendLine($"{indent}/// </summary>");
    }

    /// <summary>
    /// Escapes special characters in XML comments.
    /// </summary>
    private static string EscapeXmlComment(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}

using System.Text;
using ObjMapper.Models;
using ObjMapper.Services;

namespace ObjMapper.Generators;

/// <summary>
/// Generates Dapper-compatible entities and repository context.
/// </summary>
/// <param name="databaseType">The database type for type mapping and connection creation.</param>
/// <param name="namespaceName">The namespace for generated classes.</param>
/// <param name="useTypeInference">Whether to use ML-based type inference.</param>
public class DapperGenerator(DatabaseType databaseType, string namespaceName = "Generated", bool useTypeInference = false) : ICodeGenerator
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
            var code = GenerateEntityClass(table, entityName);
            entities[$"{entityName}.cs"] = code;
        }

        return entities;
    }

    public Dictionary<string, string> GenerateConfigurations(DatabaseSchema schema)
    {
        // For Dapper, we generate repository classes instead of EF configurations
        var repositories = new Dictionary<string, string>();
        var filteredTables = FilterDuplicateTables(schema.Tables);

        foreach (var table in filteredTables)
        {
            var entityName = NamingHelper.ToEntityName(table.Name);
            var code = GenerateRepositoryClass(table, entityName);
            repositories[$"{entityName}Repository.cs"] = code;
        }

        return repositories;
    }

    public string GenerateDbContext(DatabaseSchema schema, string contextName)
    {
        var sb = new StringBuilder();
        var filteredTables = FilterDuplicateTables(schema.Tables);
        
        sb.AppendLine("using System.Data;");
        sb.AppendLine(GetConnectionUsing());
        sb.AppendLine();
        sb.AppendLine($"namespace {_namespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Database context for Dapper operations.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public partial class {contextName} : IDisposable");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly string _connectionString;");
        sb.AppendLine("    private IDbConnection? _connection;");
        sb.AppendLine();
        sb.AppendLine($"    public {contextName}(string connectionString)");
        sb.AppendLine("    {");
        sb.AppendLine("        _connectionString = connectionString;");
        sb.AppendLine("        OnContextCreatedPartial();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public IDbConnection Connection");
        sb.AppendLine("    {");
        sb.AppendLine("        get");
        sb.AppendLine("        {");
        sb.AppendLine("            if (_connection == null)");
        sb.AppendLine("            {");
        sb.AppendLine($"                _connection = {GetConnectionCreation()};");
        sb.AppendLine("                _connection.Open();");
        sb.AppendLine("                OnConnectionCreatedPartial(_connection);");
        sb.AppendLine("            }");
        sb.AppendLine("            return _connection;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate repository properties
        foreach (var table in filteredTables)
        {
            var entityName = NamingHelper.ToEntityName(table.Name);
            var collectionName = NamingHelper.ToCollectionName(entityName);
            var repositoryField = $"_{NamingHelper.ToCamelCase(entityName)}Repository";
            var repositoryProperty = $"{entityName}Repository";
            
            sb.AppendLine($"    private {repositoryProperty}? {repositoryField};");
            sb.AppendLine($"    public {repositoryProperty} {collectionName} => {repositoryField} ??= new {repositoryProperty}(Connection);");
            sb.AppendLine();
        }

        sb.AppendLine("    public void Dispose()");
        sb.AppendLine("    {");
        sb.AppendLine("        _connection?.Dispose();");
        sb.AppendLine("        _connection = null;");
        sb.AppendLine("        GC.SuppressFinalize(this);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Called after the context is created. Override to add custom initialization.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    partial void OnContextCreatedPartial();");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Called after the connection is created and opened. Override to configure the connection.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"connection\">The opened database connection.</param>");
        sb.AppendLine("    partial void OnConnectionCreatedPartial(IDbConnection connection);");
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
        
        // Generate stored procedures wrapper
        var code = GenerateStoredProceduresClass(schema);
        procedures["StoredProcedures.cs"] = code;
        
        return procedures;
    }
    
    private string GenerateStoredProceduresClass(DatabaseSchema schema)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("using System.Data;");
        sb.AppendLine("using Dapper;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_namespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Extension methods for executing stored procedures with Dapper.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static partial class StoredProcedureExtensions");
        sb.AppendLine("{");
        
        foreach (var proc in schema.StoredProcedures)
        {
            GenerateStoredProcedureMethod(sb, proc);
        }
        
        sb.AppendLine("}");
        
        return sb.ToString();
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
        
        var methodParams = new List<string> { "this IDbConnection connection" };
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
        // Build parameters object
        var paramObject = BuildDynamicParametersObject(inputParams);
        
        // Sync method
        sb.AppendLine($"    public static void {methodName}({methodParamList})");
        sb.AppendLine("    {");
        sb.AppendLine($"        connection.Execute(\"{proc.FullName}\", {paramObject}, commandType: CommandType.StoredProcedure);");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Async method
        sb.AppendLine($"    public static async Task {asyncMethodName}({methodParamList})");
        sb.AppendLine("    {");
        sb.AppendLine($"        await connection.ExecuteAsync(\"{proc.FullName}\", {paramObject}, commandType: CommandType.StoredProcedure);");
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
        
        var paramObject = BuildDynamicParametersObject(inputParams);
        
        // Sync method
        sb.AppendLine($"    public static {returnType} {methodName}({methodParamList})");
        sb.AppendLine("    {");
        sb.AppendLine($"        return connection.ExecuteScalar<{returnType}>(\"{proc.FullName}\", {paramObject}, commandType: CommandType.StoredProcedure);");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Async method
        sb.AppendLine($"    public static async Task<{returnType}> {asyncMethodName}({methodParamList})");
        sb.AppendLine("    {");
        sb.AppendLine($"        return await connection.ExecuteScalarAsync<{returnType}>(\"{proc.FullName}\", {paramObject}, commandType: CommandType.StoredProcedure);");
        sb.AppendLine("    }");
        sb.AppendLine();
    }
    
    private void GenerateTabularProcedure(StringBuilder sb, StoredProcedureInfo proc, string methodName, 
        string asyncMethodName, string methodParamList, List<StoredProcedureParameter> inputParams)
    {
        var resultTypeName = $"{NamingHelper.ToPascalCase(proc.Name)}Result";
        var paramObject = BuildDynamicParametersObject(inputParams);
        
        // Sync method
        sb.AppendLine($"    public static IEnumerable<{resultTypeName}> {methodName}({methodParamList})");
        sb.AppendLine("    {");
        sb.AppendLine($"        return connection.Query<{resultTypeName}>(\"{proc.FullName}\", {paramObject}, commandType: CommandType.StoredProcedure);");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Async method
        sb.AppendLine($"    public static async Task<IEnumerable<{resultTypeName}>> {asyncMethodName}({methodParamList})");
        sb.AppendLine("    {");
        sb.AppendLine($"        return await connection.QueryAsync<{resultTypeName}>(\"{proc.FullName}\", {paramObject}, commandType: CommandType.StoredProcedure);");
        sb.AppendLine("    }");
        sb.AppendLine();
    }
    
    private static string BuildDynamicParametersObject(List<StoredProcedureParameter> inputParams)
    {
        if (inputParams.Count == 0)
            return "null";
        
        var paramProps = string.Join(", ", inputParams.Select(p => NamingHelper.ToCamelCase(p.Name)));
        return $"new {{ {paramProps} }}";
    }
    
    private string GenerateStoredProcedureResultTypes(DatabaseSchema schema)
    {
        var tabularProcs = schema.StoredProcedures
            .Where(p => p.OutputType == StoredProcedureOutputType.Tabular && p.ResultColumns.Count > 0)
            .ToList();
            
        if (tabularProcs.Count == 0)
            return string.Empty;
        
        var sb = new StringBuilder();
        
        sb.AppendLine($"namespace {_namespace};");
        sb.AppendLine();
        
        foreach (var proc in tabularProcs)
        {
            var typeName = $"{NamingHelper.ToPascalCase(proc.Name)}Result";
            
            sb.AppendLine($"/// <summary>");
            sb.AppendLine($"/// Result type for stored procedure {proc.FullName}.");
            sb.AppendLine($"/// </summary>");
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
    
    private string GenerateDbFunctionsClass(DatabaseSchema schema)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("using System.Data;");
        sb.AppendLine("using Dapper;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_namespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Database scalar function wrappers for Dapper.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static partial class DbFunctions");
        sb.AppendLine("{");
        
        foreach (var func in schema.ScalarFunctions)
        {
            var methodName = NamingHelper.ToPascalCase(func.Name);
            var returnType = _typeMapper.MapToCSharpType(func.ReturnType, true);
            
            // Generate XML comment
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Calls database function {func.FullName}.");
            sb.AppendLine("    /// </summary>");
            
            // Generate method signature
            var parameters = func.Parameters
                .OrderBy(p => p.OrdinalPosition)
                .Select(p => new {
                    Name = NamingHelper.ToCamelCase(p.Name),
                    Type = _typeMapper.MapToCSharpType(p.DataType, true)
                })
                .ToList();
            
            var parameterDeclarations = string.Join(", ", 
                new[] { "IDbConnection connection" }
                    .Concat(parameters.Select(p => $"{p.Type} {p.Name}")));
            
            sb.AppendLine($"    public static {returnType} {methodName}({parameterDeclarations})");
            sb.AppendLine("    {");
            
            // Build the SQL call
            var fullFunctionName = !string.IsNullOrEmpty(func.Schema) 
                ? $"{func.Schema}.{func.Name}" 
                : func.Name;
            
            var paramPlaceholders = string.Join(", ", 
                parameters.Select(p => $"@{p.Name}"));
            
            var sql = $"SELECT {fullFunctionName}({paramPlaceholders})";
            
            if (parameters.Count > 0)
            {
                var paramObject = "new { " + string.Join(", ", parameters.Select(p => p.Name)) + " }";
                sb.AppendLine($"        return connection.ExecuteScalar<{returnType}>(\"{sql}\", {paramObject});");
            }
            else
            {
                sb.AppendLine($"        return connection.ExecuteScalar<{returnType}>(\"{sql}\");");
            }
            
            sb.AppendLine("    }");
            sb.AppendLine();
            
            // Generate async version
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Calls database function {func.FullName} asynchronously.");
            sb.AppendLine("    /// </summary>");
            
            sb.AppendLine($"    public static async Task<{returnType}> {methodName}Async({parameterDeclarations})");
            sb.AppendLine("    {");
            
            if (parameters.Count > 0)
            {
                var paramObject = "new { " + string.Join(", ", parameters.Select(p => p.Name)) + " }";
                sb.AppendLine($"        return await connection.ExecuteScalarAsync<{returnType}>(\"{sql}\", {paramObject});");
            }
            else
            {
                sb.AppendLine($"        return await connection.ExecuteScalarAsync<{returnType}>(\"{sql}\");");
            }
            
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        
        sb.AppendLine("}");
        
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
    /// This ensures consistent property names are used across entity and repository generation.
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

    private string GenerateEntityClass(TableInfo table, string entityName)
    {
        var sb = new StringBuilder();
        
        // Build column to property mapping
        var columnToProperty = BuildColumnToPropertyMap(table, entityName);

        sb.AppendLine("using System.ComponentModel.DataAnnotations;");
        sb.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_namespace};");
        sb.AppendLine();

        // Add table attribute
        if (!string.IsNullOrEmpty(table.Schema))
        {
            sb.AppendLine($"[Table(\"{table.Name}\", Schema = \"{table.Schema}\")]");
        }
        else
        {
            sb.AppendLine($"[Table(\"{table.Name}\")]");
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

            // Detect primary key
            var isPrimaryKey = column.Column.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                             (column.Column.EndsWith("_id", StringComparison.OrdinalIgnoreCase) && 
                              column.Column.StartsWith(table.Name, StringComparison.OrdinalIgnoreCase));

            // Add comment if present
            if (!string.IsNullOrEmpty(column.Comment))
            {
                AppendXmlComment(sb, column.Comment, "    ");
            }

            if (isPrimaryKey)
            {
                sb.AppendLine("    [Key]");
            }

            if (propertyName != column.Column)
            {
                sb.AppendLine($"    [Column(\"{column.Column}\")]");
            }

            if (!column.Nullable && csharpType == "string")
            {
                sb.AppendLine("    [Required]");
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

            sb.AppendLine();
        }

        // Remove the last empty line
        sb.Length -= Environment.NewLine.Length;
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GenerateRepositoryClass(TableInfo table, string entityName)
    {
        var sb = new StringBuilder();
        var tableName = GetFullTableName(table);
        
        // Build column to property mapping (same logic as entity generation)
        var columnToProperty = BuildColumnToPropertyMap(table, entityName);
        
        // Detect primary key column
        var pkColumn = table.Columns.FirstOrDefault(c => 
            c.Column.Equals("id", StringComparison.OrdinalIgnoreCase)) ?? 
            table.Columns.FirstOrDefault();
        var pkPropertyName = pkColumn != null 
            ? columnToProperty[pkColumn.Column]
            : "Id";
        var pkColumnName = pkColumn?.Column ?? "id";
        var pkType = pkColumn != null ? _typeMapper.MapToCSharpType(pkColumn.Type, false) : "int";

        sb.AppendLine("using System.Data;");
        sb.AppendLine("using Dapper;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_namespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Repository for {entityName} entity.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public partial class {entityName}Repository");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly IDbConnection _connection;");
        sb.AppendLine();
        sb.AppendLine($"    public {entityName}Repository(IDbConnection connection)");
        sb.AppendLine("    {");
        sb.AppendLine("        _connection = connection;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // GetAll method
        sb.AppendLine($"    public async Task<IEnumerable<{entityName}>> GetAllAsync()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return await _connection.QueryAsync<{entityName}>(\"SELECT * FROM {tableName}\");");
        sb.AppendLine("    }");
        sb.AppendLine();

        // GetById method
        sb.AppendLine($"    public async Task<{entityName}?> GetByIdAsync({pkType} id)");
        sb.AppendLine("    {");
        sb.AppendLine($"        return await _connection.QueryFirstOrDefaultAsync<{entityName}>(");
        sb.AppendLine($"            \"SELECT * FROM {tableName} WHERE {pkColumnName} = @Id\",");
        sb.AppendLine("            new { Id = id });");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Insert method
        sb.AppendLine($"    public async Task<int> InsertAsync({entityName} entity)");
        sb.AppendLine("    {");
        
        var insertColumns = table.Columns
            .Where(c => !c.Column.Equals("id", StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        if (insertColumns.Count > 0)
        {
            var columnNames = string.Join(", ", insertColumns.Select(c => c.Column));
            var paramNames = string.Join(", ", insertColumns.Select(c => $"@{columnToProperty[c.Column]}"));
            
            sb.AppendLine($"        const string sql = \"INSERT INTO {tableName} ({columnNames}) VALUES ({paramNames})\";");
            sb.AppendLine("        return await _connection.ExecuteAsync(sql, entity);");
        }
        else
        {
            sb.AppendLine($"        const string sql = \"INSERT INTO {tableName} DEFAULT VALUES\";");
            sb.AppendLine("        return await _connection.ExecuteAsync(sql);");
        }
        
        sb.AppendLine("    }");
        sb.AppendLine();

        // Update method
        sb.AppendLine($"    public async Task<int> UpdateAsync({entityName} entity)");
        sb.AppendLine("    {");
        
        var updateColumns = table.Columns
            .Where(c => !c.Column.Equals("id", StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        if (updateColumns.Count > 0)
        {
            var setClauses = string.Join(", ", updateColumns.Select(c => $"{c.Column} = @{columnToProperty[c.Column]}"));
            
            sb.AppendLine($"        const string sql = \"UPDATE {tableName} SET {setClauses} WHERE {pkColumnName} = @{pkPropertyName}\";");
            sb.AppendLine("        return await _connection.ExecuteAsync(sql, entity);");
        }
        else
        {
            sb.AppendLine("        return await Task.FromResult(0);");
        }
        
        sb.AppendLine("    }");
        sb.AppendLine();

        // Delete method
        sb.AppendLine($"    public async Task<int> DeleteAsync({pkType} id)");
        sb.AppendLine("    {");
        sb.AppendLine($"        return await _connection.ExecuteAsync(\"DELETE FROM {tableName} WHERE {pkColumnName} = @Id\", new {{ Id = id }});");
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

    private static string GetFullTableName(TableInfo table)
    {
        return string.IsNullOrEmpty(table.Schema) ? table.Name : $"{table.Schema}.{table.Name}";
    }

    private string GetConnectionCreation()
    {
        return _databaseType switch
        {
            DatabaseType.MySql => "new MySqlConnection(_connectionString)",
            DatabaseType.PostgreSql => "new NpgsqlConnection(_connectionString)",
            DatabaseType.SqlServer => "new SqlConnection(_connectionString)",
            DatabaseType.Oracle => "new OracleConnection(_connectionString)",
            DatabaseType.Sqlite => "new SqliteConnection(_connectionString)",
            _ => "new SqlConnection(_connectionString)"
        };
    }

    private string GetConnectionUsing()
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

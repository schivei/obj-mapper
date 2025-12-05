using System.Text;
using ObjMapper.Models;
using ObjMapper.Services;

namespace ObjMapper.Generators;

/// <summary>
/// Generates Dapper-compatible entities and repository context.
/// </summary>
public class DapperGenerator : ICodeGenerator
{
    private readonly TypeMapper _typeMapper;
    private readonly string _namespace;
    private readonly DatabaseType _databaseType;

    public EntityTypeMode EntityTypeMode { get; set; } = EntityTypeMode.Class;

    public DapperGenerator(DatabaseType databaseType, string namespaceName = "Generated")
    {
        _typeMapper = new TypeMapper(databaseType);
        _namespace = namespaceName;
        _databaseType = databaseType;
    }

    public Dictionary<string, string> GenerateEntities(DatabaseSchema schema)
    {
        var entities = new Dictionary<string, string>();

        foreach (var table in schema.Tables)
        {
            var entityName = NamingHelper.ToEntityName(table.Name);
            var code = GenerateEntityClass(table);
            entities[$"{entityName}.cs"] = code;
        }

        return entities;
    }

    public Dictionary<string, string> GenerateConfigurations(DatabaseSchema schema)
    {
        // For Dapper, we generate repository classes instead of EF configurations
        var repositories = new Dictionary<string, string>();

        foreach (var table in schema.Tables)
        {
            var entityName = NamingHelper.ToEntityName(table.Name);
            var code = GenerateRepositoryClass(table);
            repositories[$"{entityName}Repository.cs"] = code;
        }

        return repositories;
    }

    public string GenerateDbContext(DatabaseSchema schema, string contextName)
    {
        var sb = new StringBuilder();
        
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
        sb.AppendLine("            }");
        sb.AppendLine("            return _connection;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate repository properties
        foreach (var table in schema.Tables)
        {
            var entityName = NamingHelper.ToEntityName(table.Name);
            var repositoryField = $"_{NamingHelper.ToCamelCase(entityName)}Repository";
            var repositoryProperty = $"{entityName}Repository";
            
            sb.AppendLine($"    private {repositoryProperty}? {repositoryField};");
            sb.AppendLine($"    public {repositoryProperty} {NamingHelper.ToCollectionName(entityName)} => {repositoryField} ??= new {repositoryProperty}(Connection);");
            sb.AppendLine();
        }

        sb.AppendLine("    public void Dispose()");
        sb.AppendLine("    {");
        sb.AppendLine("        _connection?.Dispose();");
        sb.AppendLine("        _connection = null;");
        sb.AppendLine("        GC.SuppressFinalize(this);");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GenerateEntityClass(TableInfo table)
    {
        var entityName = NamingHelper.ToEntityName(table.Name);
        var sb = new StringBuilder();
        
        // Collect all property names first to detect conflicts
        var propertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { entityName };

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
            var propertyName = GetUniquePropertyName(NamingHelper.ToPascalCase(column.Column), entityName, propertyNames);
            propertyNames.Add(propertyName);
            
            var csharpType = _typeMapper.MapToCSharpType(column.Type, column.Nullable);

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

    private string GenerateRepositoryClass(TableInfo table)
    {
        var entityName = NamingHelper.ToEntityName(table.Name);
        var sb = new StringBuilder();
        var tableName = GetFullTableName(table);
        
        // Track property names for this entity to handle conflicts
        var propertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { entityName };
        
        // Detect primary key column
        var pkColumn = table.Columns.FirstOrDefault(c => 
            c.Column.Equals("id", StringComparison.OrdinalIgnoreCase)) ?? 
            table.Columns.FirstOrDefault();
        var pkPropertyName = pkColumn != null 
            ? GetUniquePropertyName(NamingHelper.ToPascalCase(pkColumn.Column), entityName, propertyNames) 
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
            var paramNames = string.Join(", ", insertColumns.Select(c => 
            {
                var propName = GetUniquePropertyName(NamingHelper.ToPascalCase(c.Column), entityName, propertyNames);
                return $"@{propName}";
            }));
            
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
            var setClauses = string.Join(", ", updateColumns.Select(c => 
            {
                var propName = GetUniquePropertyName(NamingHelper.ToPascalCase(c.Column), entityName, propertyNames);
                return $"{c.Column} = @{propName}";
            }));
            
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

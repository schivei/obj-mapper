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
        sb.AppendLine("using Microsoft.Data.SqlClient;");
        sb.AppendLine("using MySqlConnector;");
        sb.AppendLine("using Npgsql;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_namespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Database context for Dapper operations.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public class {contextName} : IDisposable");
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

        sb.AppendLine($"public class {entityName}");
        sb.AppendLine("{");

        // Generate properties for columns
        foreach (var column in table.Columns)
        {
            var propertyName = NamingHelper.ToPascalCase(column.Column);
            var csharpType = _typeMapper.MapToCSharpType(column.Type, column.Nullable);

            // Detect primary key
            var isPrimaryKey = column.Column.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                             (column.Column.EndsWith("_id", StringComparison.OrdinalIgnoreCase) && 
                              column.Column.StartsWith(table.Name, StringComparison.OrdinalIgnoreCase));

            // Add comment if present
            if (!string.IsNullOrEmpty(column.Comment))
            {
                sb.AppendLine("    /// <summary>");
                sb.AppendLine($"    /// {column.Comment}");
                sb.AppendLine("    /// </summary>");
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
        
        // Detect primary key column
        var pkColumn = table.Columns.FirstOrDefault(c => 
            c.Column.Equals("id", StringComparison.OrdinalIgnoreCase)) ?? 
            table.Columns.FirstOrDefault();
        var pkPropertyName = pkColumn != null ? NamingHelper.ToPascalCase(pkColumn.Column) : "Id";
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
        sb.AppendLine($"public class {entityName}Repository");
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
            var paramNames = string.Join(", ", insertColumns.Select(c => $"@{NamingHelper.ToPascalCase(c.Column)}"));
            
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
                $"{c.Column} = @{NamingHelper.ToPascalCase(c.Column)}"));
            
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

    private string GetFullTableName(TableInfo table)
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
            DatabaseType.Oracle => "new SqlConnection(_connectionString)", // Simplified
            DatabaseType.Sqlite => "new SqlConnection(_connectionString)", // Simplified
            _ => "new SqlConnection(_connectionString)"
        };
    }
}

using System.Data.Common;
using Microsoft.Data.SqlClient;
using ObjMapper.Models;

namespace ObjMapper.Services.Extractors;

/// <summary>
/// Extracts schema from SQL Server databases.
/// </summary>
public class SqlServerSchemaExtractor : BaseSchemaExtractor
{
    protected override DatabaseType DatabaseType => DatabaseType.SqlServer;
    protected override string DefaultSchemaName => "dbo";

    protected override DbConnection CreateConnection(string connectionString) => 
        new SqlConnection(connectionString);

    protected override async Task<List<(string name, string schema)>> GetTablesAsync(DbConnection connection, string schemaName)
    {
        var tables = new List<(string name, string schema)>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT TABLE_NAME, TABLE_SCHEMA 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = @schema
            ORDER BY TABLE_NAME";
        AddParameter(command, "@schema", schemaName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add((reader.GetString(0), reader.GetString(1)));
        }
        
        return tables;
    }

    protected override async Task<List<(string name, string schema)>> GetViewsAsync(DbConnection connection, string schemaName)
    {
        var views = new List<(string name, string schema)>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT TABLE_NAME, TABLE_SCHEMA 
            FROM INFORMATION_SCHEMA.VIEWS 
            WHERE TABLE_SCHEMA = @schema
            ORDER BY TABLE_NAME";
        AddParameter(command, "@schema", schemaName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            views.Add((reader.GetString(0), reader.GetString(1)));
        }
        
        return views;
    }

    protected override async Task<List<ColumnInfo>> GetColumnsAsync(DbConnection connection, string schemaName, string tableName)
    {
        var columns = new List<ColumnInfo>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                c.COLUMN_NAME, 
                c.DATA_TYPE, 
                c.IS_NULLABLE,
                ISNULL(CAST(ep.value AS NVARCHAR(MAX)), '') as column_comment
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN sys.columns sc ON sc.name = c.COLUMN_NAME 
                AND sc.object_id = OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME)
            LEFT JOIN sys.extended_properties ep ON ep.major_id = sc.object_id 
                AND ep.minor_id = sc.column_id AND ep.name = 'MS_Description'
            WHERE c.TABLE_SCHEMA = @schema AND c.TABLE_NAME = @table
            ORDER BY c.ORDINAL_POSITION";
        AddParameter(command, "@schema", schemaName);
        AddParameter(command, "@table", tableName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnInfo
            {
                Schema = schemaName,
                Table = tableName,
                Column = reader.GetString(0),
                Type = reader.GetString(1),
                Nullable = reader.GetString(2) == "YES",
                Comment = reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
            });
        }
        
        return columns;
    }

    protected override async Task<List<IndexInfo>> GetIndexesAsync(DbConnection connection, string schemaName, string tableName)
    {
        var indexes = new List<IndexInfo>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                i.name as index_name,
                STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal) as columns,
                i.is_unique
            FROM sys.indexes i
            INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            INNER JOIN sys.tables t ON i.object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE s.name = @schema 
              AND t.name = @table
              AND i.is_primary_key = 0
              AND i.name IS NOT NULL
            GROUP BY i.name, i.is_unique";
        AddParameter(command, "@schema", schemaName);
        AddParameter(command, "@table", tableName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            indexes.Add(new IndexInfo
            {
                Schema = schemaName,
                Table = tableName,
                Name = reader.GetString(0),
                Key = reader.GetString(1),
                Type = reader.GetBoolean(2) ? "unique" : "nonclustered"
            });
        }
        
        return indexes;
    }

    protected override async Task<List<RelationshipInfo>> GetRelationshipsAsync(DbConnection connection, string schemaName)
    {
        var fkGroups = new Dictionary<string, (RelationshipInfo rel, List<string> keys, List<string> foreigns)>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                fk.name AS constraint_name,
                SCHEMA_NAME(t.schema_id) AS table_schema,
                t.name AS table_name,
                c.name AS column_name,
                SCHEMA_NAME(rt.schema_id) AS referenced_schema,
                rt.name AS referenced_table,
                rc.name AS referenced_column
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            INNER JOIN sys.tables t ON fkc.parent_object_id = t.object_id
            INNER JOIN sys.columns c ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
            INNER JOIN sys.tables rt ON fkc.referenced_object_id = rt.object_id
            INNER JOIN sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
            WHERE SCHEMA_NAME(t.schema_id) = @schema
            ORDER BY fk.name, fkc.constraint_column_id";
        AddParameter(command, "@schema", schemaName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ProcessForeignKeyRow(reader, fkGroups);
        }
        
        return ProcessForeignKeyGroups(fkGroups);
    }

    protected override async Task<List<ScalarFunctionInfo>> GetScalarFunctionsAsync(DbConnection connection, string schemaName)
    {
        var functions = new List<ScalarFunctionInfo>();
        var functionList = new List<(string schema, string name, string returnType)>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                SCHEMA_NAME(o.schema_id) AS schema_name,
                o.name AS function_name,
                TYPE_NAME(r.user_type_id) AS return_type
            FROM sys.objects o
            JOIN sys.sql_modules m ON o.object_id = m.object_id
            LEFT JOIN sys.parameters r ON o.object_id = r.object_id AND r.parameter_id = 0
            WHERE o.type = 'FN'
              AND SCHEMA_NAME(o.schema_id) = @schema
            ORDER BY o.name";
        AddParameter(command, "@schema", schemaName);
        
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                functionList.Add((
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? "sql_variant" : reader.GetString(2)
                ));
            }
        }
        
        foreach (var (funcSchema, funcName, returnType) in functionList)
        {
            var functionInfo = new ScalarFunctionInfo
            {
                Schema = funcSchema,
                Name = funcName,
                ReturnType = returnType,
                Parameters = await GetFunctionParametersAsync(connection, funcSchema, funcName)
            };
            functions.Add(functionInfo);
        }
        
        return functions;
    }

    private async Task<List<ScalarFunctionParameter>> GetFunctionParametersAsync(DbConnection connection, string schemaName, string functionName)
    {
        var parameters = new List<ScalarFunctionParameter>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                p.name AS param_name,
                TYPE_NAME(p.user_type_id) AS data_type,
                p.parameter_id
            FROM sys.parameters p
            JOIN sys.objects o ON p.object_id = o.object_id
            WHERE SCHEMA_NAME(o.schema_id) = @schema 
              AND o.name = @function
              AND p.parameter_id > 0
            ORDER BY p.parameter_id";
        AddParameter(command, "@schema", schemaName);
        AddParameter(command, "@function", functionName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var paramName = reader.GetString(0);
            if (paramName.StartsWith('@'))
                paramName = paramName[1..];
                
            parameters.Add(new ScalarFunctionParameter
            {
                Name = paramName,
                DataType = reader.GetString(1),
                OrdinalPosition = reader.GetInt32(2)
            });
        }
        
        return parameters;
    }

    protected override async Task<List<StoredProcedureInfo>> GetStoredProceduresAsync(DbConnection connection, string schemaName)
    {
        var procedures = new List<StoredProcedureInfo>();
        var procList = new List<(string schema, string name)>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                SCHEMA_NAME(p.schema_id) AS schema_name,
                p.name AS procedure_name
            FROM sys.procedures p
            WHERE SCHEMA_NAME(p.schema_id) = @schema
            ORDER BY p.name";
        AddParameter(command, "@schema", schemaName);
        
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                procList.Add((reader.GetString(0), reader.GetString(1)));
            }
        }
        
        foreach (var (procSchema, procName) in procList)
        {
            var procInfo = new StoredProcedureInfo
            {
                Schema = procSchema,
                Name = procName,
                Parameters = await GetProcedureParametersAsync(connection, procSchema, procName)
            };
            
            // Determine output type by analyzing procedure definition
            procInfo.OutputType = await DetermineProcedureOutputTypeAsync(connection, procSchema, procName);
            
            if (procInfo.OutputType == StoredProcedureOutputType.Tabular)
            {
                procInfo.ResultColumns = await GetProcedureResultColumnsAsync(connection, procSchema, procName);
            }
            
            procedures.Add(procInfo);
        }
        
        return procedures;
    }

    private async Task<List<StoredProcedureParameter>> GetProcedureParametersAsync(DbConnection connection, string schemaName, string procedureName)
    {
        var parameters = new List<StoredProcedureParameter>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                p.name AS param_name,
                TYPE_NAME(p.user_type_id) AS data_type,
                p.parameter_id,
                p.is_output,
                p.has_default_value
            FROM sys.parameters p
            JOIN sys.procedures proc ON p.object_id = proc.object_id
            WHERE SCHEMA_NAME(proc.schema_id) = @schema 
              AND proc.name = @procedure
              AND p.parameter_id > 0
            ORDER BY p.parameter_id";
        AddParameter(command, "@schema", schemaName);
        AddParameter(command, "@procedure", procedureName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var paramName = reader.GetString(0);
            if (paramName.StartsWith('@'))
                paramName = paramName.TrimStart('@');
                
            parameters.Add(new StoredProcedureParameter
            {
                Name = paramName,
                DataType = reader.GetString(1),
                OrdinalPosition = reader.GetInt32(2),
                IsOutput = reader.GetBoolean(3),
                HasDefault = reader.GetBoolean(4)
            });
        }
        
        return parameters;
    }

    private async Task<StoredProcedureOutputType> DetermineProcedureOutputTypeAsync(DbConnection connection, string schemaName, string procedureName)
    {
        // Check for output parameters (scalar output)
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*)
            FROM sys.parameters p
            JOIN sys.procedures proc ON p.object_id = proc.object_id
            WHERE SCHEMA_NAME(proc.schema_id) = @schema 
              AND proc.name = @procedure
              AND p.is_output = 1";
        AddParameter(command, "@schema", schemaName);
        AddParameter(command, "@procedure", procedureName);
        
        var result = await command.ExecuteScalarAsync();
        var outputParamCount = result != null ? (int)result : 0;
        if (outputParamCount > 0)
        {
            return StoredProcedureOutputType.Scalar;
        }
        
        // Check procedure definition for SELECT statements (tabular output)
        // Use sys.dm_exec_describe_first_result_set_for_object for more accurate detection
        try
        {
            await using var resultCommand = connection.CreateCommand();
            resultCommand.CommandText = @"
                SELECT TOP 1 column_ordinal
                FROM sys.dm_exec_describe_first_result_set_for_object(
                    OBJECT_ID(@fullName), NULL)
                WHERE name IS NOT NULL";
            AddParameter(resultCommand, "@fullName", $"{schemaName}.{procedureName}");
            
            var hasResult = await resultCommand.ExecuteScalarAsync();
            if (hasResult != null)
            {
                return StoredProcedureOutputType.Tabular;
            }
        }
        catch (Microsoft.Data.SqlClient.SqlException)
        {
            // Some procedures can't be analyzed (dynamic SQL, temp tables, etc.)
            // Fall back to heuristic approach
            await using var defCommand = connection.CreateCommand();
            defCommand.CommandText = @"
                SELECT m.definition
                FROM sys.sql_modules m
                JOIN sys.procedures p ON m.object_id = p.object_id
                WHERE SCHEMA_NAME(p.schema_id) = @schema 
                  AND p.name = @procedure";
            AddParameter(defCommand, "@schema", schemaName);
            AddParameter(defCommand, "@procedure", procedureName);
            
            var definition = await defCommand.ExecuteScalarAsync() as string;
            if (!string.IsNullOrEmpty(definition))
            {
                // Remove comments and string literals before checking for SELECT
                var cleanedDef = RemoveCommentsAndStrings(definition);
                var upperDef = cleanedDef.ToUpperInvariant();
                
                // Check for SELECT at statement level (not inside INSERT, INTO, or subqueries for assignments)
                var hasTabularSelect = System.Text.RegularExpressions.Regex.IsMatch(
                    upperDef, 
                    @"^\s*SELECT\s+(?!.*\s+INTO\s+)", 
                    System.Text.RegularExpressions.RegexOptions.Multiline);
                    
                if (hasTabularSelect && !upperDef.Contains("INSERT INTO") && !upperDef.Contains("SELECT INTO"))
                {
                    return StoredProcedureOutputType.Tabular;
                }
            }
        }
        
        return StoredProcedureOutputType.None;
    }

    private static string RemoveCommentsAndStrings(string sql)
    {
        // Remove single-line comments
        sql = System.Text.RegularExpressions.Regex.Replace(sql, @"--[^\r\n]*", "");
        // Remove multi-line comments
        sql = System.Text.RegularExpressions.Regex.Replace(sql, @"/\*[\s\S]*?\*/", "");
        // Remove string literals (simplified - doesn't handle escaped quotes perfectly)
        sql = System.Text.RegularExpressions.Regex.Replace(sql, @"'[^']*'", "''");
        return sql;
    }

    private async Task<List<StoredProcedureColumn>> GetProcedureResultColumnsAsync(DbConnection connection, string schemaName, string procedureName)
    {
        var columns = new List<StoredProcedureColumn>();
        
        try
        {
            // Use sys.dm_exec_describe_first_result_set_for_object to get result columns
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    name,
                    system_type_name,
                    is_nullable,
                    column_ordinal
                FROM sys.dm_exec_describe_first_result_set_for_object(
                    OBJECT_ID(@fullName), NULL)
                WHERE name IS NOT NULL
                ORDER BY column_ordinal";
            AddParameter(command, "@fullName", $"{schemaName}.{procedureName}");
            
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(new StoredProcedureColumn
                {
                    Name = reader.GetString(0),
                    DataType = reader.GetString(1),
                    IsNullable = reader.GetBoolean(2),
                    OrdinalPosition = reader.GetInt32(3)
                });
            }
        }
        catch (Microsoft.Data.SqlClient.SqlException)
        {
            // Some procedures may not be analyzable (dynamic SQL, temp tables, etc.)
            // Return empty list in such cases - this is expected behavior
        }
        
        return columns;
    }

    private static void ProcessForeignKeyRow(DbDataReader reader,
        Dictionary<string, (RelationshipInfo rel, List<string> keys, List<string> foreigns)> fkGroups)
    {
        var constraintName = reader.GetString(0);
        var tableSchema = reader.GetString(1);
        var tableName = reader.GetString(2);
        var columnName = reader.GetString(3);
        var foreignSchema = reader.GetString(4);
        var foreignTable = reader.GetString(5);
        var foreignColumn = reader.GetString(6);
        
        if (!fkGroups.TryGetValue(constraintName, out var group))
        {
            var rel = new RelationshipInfo
            {
                Name = constraintName,
                SchemaFrom = tableSchema,
                SchemaTo = foreignSchema,
                TableFrom = tableName,
                TableTo = foreignTable
            };
            group = (rel, [], []);
            fkGroups[constraintName] = group;
        }
        
        group.keys.Add(foreignColumn);
        group.foreigns.Add(columnName);
    }
}

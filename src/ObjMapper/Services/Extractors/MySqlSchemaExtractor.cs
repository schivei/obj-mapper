using System.Data.Common;
using MySqlConnector;
using ObjMapper.Models;
using ObjMapper.Services.TypeInference;

namespace ObjMapper.Services.Extractors;

/// <summary>
/// Extracts schema from MySQL databases.
/// </summary>
public class MySqlSchemaExtractor : BaseSchemaExtractor
{
    protected override DatabaseType DatabaseType => DatabaseType.MySql;
    protected override string DefaultSchemaName => string.Empty; // Will use database name

    protected override DbConnection CreateConnection(string connectionString) => 
        new MySqlConnection(connectionString);

    public override async Task<DatabaseSchema> ExtractSchemaAsync(string connectionString, SchemaExtractionOptions options)
    {
        // For MySQL, get the database name from connection if no filter provided
        await using var connection = (MySqlConnection)CreateConnection(connectionString);
        await connection.OpenAsync();
        
        var databaseName = options.SchemaFilter ?? connection.Database;
        return await ExtractSchemaWithDatabaseAsync(connection, databaseName, options);
    }

    private async Task<DatabaseSchema> ExtractSchemaWithDatabaseAsync(MySqlConnection connection, string databaseName, SchemaExtractionOptions options)
    {
        var schema = new DatabaseSchema();
        
        var tables = await GetTablesInternalAsync(connection, databaseName);
        
        // Get views if enabled
        if (options.IncludeViews)
        {
            var views = await GetViewsInternalAsync(connection, databaseName);
            tables.AddRange(views);
        }
        
        foreach (var tableName in tables)
        {
            var tableInfo = new TableInfo
            {
                Schema = databaseName,
                Name = tableName
            };
            
            tableInfo.Columns = await GetColumnsInternalAsync(connection, databaseName, tableName);
            
            // Apply name-based inference first (fast, no DB queries)
            if (options.EnableTypeInference)
            {
                ApplyNameBasedInference(tableInfo);
            }
            
            // Only run expensive queries if data sampling is enabled AND type inference is enabled
            if (options.EnableTypeInference && options.EnableDataSampling)
            {
                await AnalyzeBooleanColumnsInternalAsync(connection, tableInfo, onlyUninferred: true);
                await AnalyzeGuidColumnsInternalAsync(connection, tableInfo, onlyUninferred: true);
            }
            
            tableInfo.Indexes = await GetIndexesInternalAsync(connection, databaseName, tableName);
            
            schema.Tables.Add(tableInfo);
        }
        
        schema.Relationships = await GetRelationshipsInternalAsync(connection, databaseName);
        PopulateTableRelationships(schema);
        
        if (options.IncludeUserDefinedFunctions)
        {
            schema.ScalarFunctions = await GetScalarFunctionsInternalAsync(connection, databaseName);
        }
        
        if (options.IncludeStoredProcedures)
        {
            schema.StoredProcedures = await GetStoredProceduresInternalAsync(connection, databaseName);
        }
        
        return schema;
    }

    private static async Task<List<string>> GetViewsInternalAsync(MySqlConnection connection, string databaseName)
    {
        var views = new List<string>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT TABLE_NAME 
            FROM INFORMATION_SCHEMA.VIEWS 
            WHERE TABLE_SCHEMA = @schema
            ORDER BY TABLE_NAME";
        command.Parameters.AddWithValue("@schema", databaseName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            views.Add(reader.GetString(0));
        }
        
        return views;
    }

    private static void ApplyNameBasedInference(TableInfo tableInfo)
    {
        foreach (var column in tableInfo.Columns)
        {
            var lowerName = column.Column.ToLowerInvariant();
            var lowerType = column.Type.ToLowerInvariant();
            
            // Boolean inference from column name patterns
            if (IsBooleanNamePattern(lowerName) && IsSmallIntegerType(lowerType))
            {
                column.InferredAsBoolean = true;
            }
            
            // GUID inference from column name patterns
            if (IsGuidNamePattern(lowerName) && IsGuidCompatibleType(lowerType))
            {
                column.InferredAsGuid = true;
            }
        }
    }
    
    private static bool IsBooleanNamePattern(string columnName)
    {
        return columnName.StartsWith("is_") ||
               columnName.StartsWith("has_") ||
               columnName.StartsWith("can_") ||
               columnName.StartsWith("should_") ||
               columnName.EndsWith("_flag") ||
               columnName.EndsWith("_enabled") ||
               columnName.EndsWith("_active") ||
               columnName.EndsWith("_deleted") ||
               columnName == "active" ||
               columnName == "enabled" ||
               columnName == "deleted" ||
               columnName == "published" ||
               columnName == "verified";
    }
    
    private static bool IsSmallIntegerType(string typeName)
    {
        return typeName.Contains("tinyint") ||
               typeName.Contains("smallint") ||
               typeName.Contains("bit") ||
               typeName.Contains("boolean") ||
               typeName.Contains("bool");
    }
    
    private static bool IsGuidNamePattern(string columnName)
    {
        return columnName == "uuid" ||
               columnName == "guid" ||
               columnName.EndsWith("_uuid") ||
               columnName.EndsWith("_guid") ||
               columnName == "correlation_id" ||
               columnName == "tracking_id" ||
               columnName == "external_id";
    }
    
    private static bool IsGuidCompatibleType(string typeName)
    {
        return typeName.Contains("char(36)") ||
               typeName.Contains("varchar(36)");
    }

    private async Task AnalyzeBooleanColumnsInternalAsync(MySqlConnection connection, TableInfo tableInfo, bool onlyUninferred = false)
    {
        var columnsToAnalyze = onlyUninferred
            ? tableInfo.Columns.Where(c => !c.InferredAsBoolean).ToList()
            : tableInfo.Columns;
            
        if (!columnsToAnalyze.Any())
            return;
            
        var booleanAnalysis = await TypeInference.BooleanColumnAnalyzer.AnalyzeColumnsAsync(
            connection, tableInfo.Schema, tableInfo.Name, columnsToAnalyze, DatabaseType);
        
        foreach (var column in columnsToAnalyze.Where(c => 
            booleanAnalysis.TryGetValue(c.Column, out var couldBeBoolean) && couldBeBoolean))
        {
            column.InferredAsBoolean = true;
        }
    }
    
    private async Task AnalyzeGuidColumnsInternalAsync(MySqlConnection connection, TableInfo tableInfo, bool onlyUninferred = false)
    {
        var columnsToAnalyze = onlyUninferred
            ? tableInfo.Columns.Where(c => !c.InferredAsGuid).ToList()
            : tableInfo.Columns;
            
        if (!columnsToAnalyze.Any())
            return;
            
        var guidAnalysis = await GuidColumnAnalyzer.AnalyzeColumnsAsync(
            connection, tableInfo.Schema, tableInfo.Name, columnsToAnalyze, DatabaseType);
        
        foreach (var column in columnsToAnalyze.Where(c => 
            guidAnalysis.TryGetValue(c.Column, out var couldBeGuid) && couldBeGuid))
        {
            column.InferredAsGuid = true;
        }
    }

    protected override async Task<List<(string name, string schema)>> GetTablesAsync(DbConnection connection, string schemaName)
    {
        var mysqlConn = (MySqlConnection)connection;
        var tables = await GetTablesInternalAsync(mysqlConn, schemaName);
        return tables.Select(t => (t, schemaName)).ToList();
    }

    protected override async Task<List<(string name, string schema)>> GetViewsAsync(DbConnection connection, string schemaName)
    {
        var views = new List<(string name, string schema)>();
        var mysqlConn = (MySqlConnection)connection;
        
        await using var command = mysqlConn.CreateCommand();
        command.CommandText = @"
            SELECT TABLE_NAME 
            FROM INFORMATION_SCHEMA.VIEWS 
            WHERE TABLE_SCHEMA = @schema
            ORDER BY TABLE_NAME";
        command.Parameters.AddWithValue("@schema", schemaName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            views.Add((reader.GetString(0), schemaName));
        }
        
        return views;
    }

    protected override Task<List<ColumnInfo>> GetColumnsAsync(DbConnection connection, string schemaName, string tableName) =>
        GetColumnsInternalAsync((MySqlConnection)connection, schemaName, tableName);

    protected override Task<List<IndexInfo>> GetIndexesAsync(DbConnection connection, string schemaName, string tableName) =>
        GetIndexesInternalAsync((MySqlConnection)connection, schemaName, tableName);

    protected override Task<List<RelationshipInfo>> GetRelationshipsAsync(DbConnection connection, string schemaName) =>
        GetRelationshipsInternalAsync((MySqlConnection)connection, schemaName);

    protected override Task<List<ScalarFunctionInfo>> GetScalarFunctionsAsync(DbConnection connection, string schemaName) =>
        GetScalarFunctionsInternalAsync((MySqlConnection)connection, schemaName);

    protected override Task<List<StoredProcedureInfo>> GetStoredProceduresAsync(DbConnection connection, string schemaName) =>
        GetStoredProceduresInternalAsync((MySqlConnection)connection, schemaName);

    private static async Task<List<string>> GetTablesInternalAsync(MySqlConnection connection, string databaseName)
    {
        var tables = new List<string>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT TABLE_NAME 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_SCHEMA = @schema AND TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_NAME";
        command.Parameters.AddWithValue("@schema", databaseName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }
        
        return tables;
    }

    private static async Task<List<ColumnInfo>> GetColumnsInternalAsync(MySqlConnection connection, string databaseName, string tableName)
    {
        var columns = new List<ColumnInfo>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                COLUMN_NAME, 
                DATA_TYPE, 
                IS_NULLABLE,
                COALESCE(COLUMN_COMMENT, '') as column_comment
            FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table
            ORDER BY ORDINAL_POSITION";
        command.Parameters.AddWithValue("@schema", databaseName);
        command.Parameters.AddWithValue("@table", tableName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnInfo
            {
                Schema = databaseName,
                Table = tableName,
                Column = reader.GetString(0),
                Type = reader.GetString(1),
                Nullable = reader.GetString(2) == "YES",
                Comment = reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
            });
        }
        
        return columns;
    }

    private static async Task<List<IndexInfo>> GetIndexesInternalAsync(MySqlConnection connection, string databaseName, string tableName)
    {
        var indexes = new List<IndexInfo>();
        var indexGroups = new Dictionary<string, (List<string> columns, bool isUnique)>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                INDEX_NAME,
                COLUMN_NAME,
                NON_UNIQUE
            FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = @schema 
              AND TABLE_NAME = @table
              AND INDEX_NAME != 'PRIMARY'
            ORDER BY INDEX_NAME, SEQ_IN_INDEX";
        command.Parameters.AddWithValue("@schema", databaseName);
        command.Parameters.AddWithValue("@table", tableName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var indexName = reader.GetString(0);
            var columnName = reader.GetString(1);
            var nonUnique = reader.GetInt32(2) == 1;
            
            if (!indexGroups.ContainsKey(indexName))
            {
                indexGroups[indexName] = ([], !nonUnique);
            }
            indexGroups[indexName].columns.Add(columnName);
        }
        
        foreach (var (indexName, (columns, isUnique)) in indexGroups)
        {
            indexes.Add(new IndexInfo
            {
                Schema = databaseName,
                Table = tableName,
                Name = indexName,
                Key = string.Join(",", columns),
                Type = isUnique ? "unique" : "btree"
            });
        }
        
        return indexes;
    }

    private static async Task<List<RelationshipInfo>> GetRelationshipsInternalAsync(MySqlConnection connection, string databaseName)
    {
        var fkGroups = new Dictionary<string, (RelationshipInfo rel, List<string> keys, List<string> foreigns)>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                CONSTRAINT_NAME,
                TABLE_SCHEMA,
                TABLE_NAME,
                COLUMN_NAME,
                REFERENCED_TABLE_SCHEMA,
                REFERENCED_TABLE_NAME,
                REFERENCED_COLUMN_NAME
            FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
            WHERE TABLE_SCHEMA = @schema
              AND REFERENCED_TABLE_NAME IS NOT NULL
            ORDER BY CONSTRAINT_NAME, ORDINAL_POSITION";
        command.Parameters.AddWithValue("@schema", databaseName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var constraintName = reader.GetString(0);
            var tableSchema = reader.GetString(1);
            var tableName = reader.GetString(2);
            var columnName = reader.GetString(3);
            var foreignSchema = reader.IsDBNull(4) ? tableSchema : reader.GetString(4);
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
        
        return ProcessForeignKeyGroups(fkGroups);
    }

    private static async Task<List<ScalarFunctionInfo>> GetScalarFunctionsInternalAsync(MySqlConnection connection, string databaseName)
    {
        var functions = new List<ScalarFunctionInfo>();
        var functionList = new List<(string schema, string name, string returnType)>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                ROUTINE_SCHEMA,
                ROUTINE_NAME,
                DATA_TYPE
            FROM INFORMATION_SCHEMA.ROUTINES
            WHERE ROUTINE_SCHEMA = @schema
              AND ROUTINE_TYPE = 'FUNCTION'
            ORDER BY ROUTINE_NAME";
        command.Parameters.AddWithValue("@schema", databaseName);
        
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                functionList.Add((
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? "varchar" : reader.GetString(2)
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

    private static async Task<List<ScalarFunctionParameter>> GetFunctionParametersAsync(MySqlConnection connection, string schemaName, string functionName)
    {
        var parameters = new List<ScalarFunctionParameter>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                PARAMETER_NAME,
                DATA_TYPE,
                ORDINAL_POSITION
            FROM INFORMATION_SCHEMA.PARAMETERS
            WHERE SPECIFIC_SCHEMA = @schema 
              AND SPECIFIC_NAME = @function
              AND PARAMETER_MODE = 'IN'
            ORDER BY ORDINAL_POSITION";
        command.Parameters.AddWithValue("@schema", schemaName);
        command.Parameters.AddWithValue("@function", functionName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            parameters.Add(new ScalarFunctionParameter
            {
                Name = reader.IsDBNull(0) ? $"p{reader.GetInt32(2)}" : reader.GetString(0),
                DataType = reader.GetString(1),
                OrdinalPosition = reader.GetInt32(2)
            });
        }
        
        return parameters;
    }

    private static async Task<List<StoredProcedureInfo>> GetStoredProceduresInternalAsync(MySqlConnection connection, string databaseName)
    {
        var procedures = new List<StoredProcedureInfo>();
        var procList = new List<(string schema, string name)>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                ROUTINE_SCHEMA,
                ROUTINE_NAME
            FROM INFORMATION_SCHEMA.ROUTINES
            WHERE ROUTINE_SCHEMA = @schema
              AND ROUTINE_TYPE = 'PROCEDURE'
            ORDER BY ROUTINE_NAME";
        command.Parameters.AddWithValue("@schema", databaseName);
        
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
            
            // Determine output type based on OUT parameters
            procInfo.OutputType = DetermineProcedureOutputType(procInfo.Parameters);
            
            procedures.Add(procInfo);
        }
        
        return procedures;
    }

    private static async Task<List<StoredProcedureParameter>> GetProcedureParametersAsync(MySqlConnection connection, string schemaName, string procedureName)
    {
        var parameters = new List<StoredProcedureParameter>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                PARAMETER_NAME,
                DATA_TYPE,
                ORDINAL_POSITION,
                PARAMETER_MODE
            FROM INFORMATION_SCHEMA.PARAMETERS
            WHERE SPECIFIC_SCHEMA = @schema 
              AND SPECIFIC_NAME = @procedure
            ORDER BY ORDINAL_POSITION";
        command.Parameters.AddWithValue("@schema", schemaName);
        command.Parameters.AddWithValue("@procedure", procedureName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var mode = reader.IsDBNull(3) ? "IN" : reader.GetString(3);
            parameters.Add(new StoredProcedureParameter
            {
                Name = reader.IsDBNull(0) ? $"p{reader.GetInt32(2)}" : reader.GetString(0),
                DataType = reader.GetString(1),
                OrdinalPosition = reader.GetInt32(2),
                IsOutput = mode == "OUT" || mode == "INOUT"
            });
        }
        
        return parameters;
    }

    private static StoredProcedureOutputType DetermineProcedureOutputType(List<StoredProcedureParameter> parameters)
    {
        var outputParams = parameters.Count(p => p.IsOutput);
        return outputParams switch
        {
            0 => StoredProcedureOutputType.None,
            1 => StoredProcedureOutputType.Scalar,
            _ => StoredProcedureOutputType.Tabular
        };
    }
}

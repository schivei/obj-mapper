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

    public override async Task<DatabaseSchema> ExtractSchemaAsync(string connectionString, string? schemaFilter, bool enableTypeInference)
    {
        // For MySQL, get the database name from connection if no filter provided
        await using var connection = (MySqlConnection)CreateConnection(connectionString);
        await connection.OpenAsync();
        
        var databaseName = schemaFilter ?? connection.Database;
        return await ExtractSchemaWithDatabaseAsync(connection, databaseName, enableTypeInference);
    }

    private async Task<DatabaseSchema> ExtractSchemaWithDatabaseAsync(MySqlConnection connection, string databaseName, bool enableTypeInference)
    {
        var schema = new DatabaseSchema();
        
        var tables = await GetTablesInternalAsync(connection, databaseName);
        
        foreach (var tableName in tables)
        {
            var tableInfo = new TableInfo
            {
                Schema = databaseName,
                Name = tableName
            };
            
            tableInfo.Columns = await GetColumnsInternalAsync(connection, databaseName, tableName);
            
            if (enableTypeInference)
            {
                await AnalyzeBooleanColumnsInternalAsync(connection, tableInfo);
                await AnalyzeGuidColumnsInternalAsync(connection, tableInfo);
            }
            
            tableInfo.Indexes = await GetIndexesInternalAsync(connection, databaseName, tableName);
            
            schema.Tables.Add(tableInfo);
        }
        
        schema.Relationships = await GetRelationshipsInternalAsync(connection, databaseName);
        PopulateTableRelationships(schema);
        schema.ScalarFunctions = await GetScalarFunctionsInternalAsync(connection, databaseName);
        
        return schema;
    }

    private async Task AnalyzeBooleanColumnsInternalAsync(MySqlConnection connection, TableInfo tableInfo)
    {
        var booleanAnalysis = await TypeInference.BooleanColumnAnalyzer.AnalyzeColumnsAsync(
            connection, tableInfo.Schema, tableInfo.Name, tableInfo.Columns, DatabaseType);
        
        foreach (var column in tableInfo.Columns.Where(c => 
            booleanAnalysis.TryGetValue(c.Column, out var couldBeBoolean) && couldBeBoolean))
        {
            column.InferredAsBoolean = true;
        }
    }
    
    private async Task AnalyzeGuidColumnsInternalAsync(MySqlConnection connection, TableInfo tableInfo)
    {
        var guidAnalysis = await GuidColumnAnalyzer.AnalyzeColumnsAsync(
            connection, tableInfo.Schema, tableInfo.Name, tableInfo.Columns, DatabaseType);
        
        foreach (var column in tableInfo.Columns.Where(c => 
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

    protected override Task<List<ColumnInfo>> GetColumnsAsync(DbConnection connection, string schemaName, string tableName) =>
        GetColumnsInternalAsync((MySqlConnection)connection, schemaName, tableName);

    protected override Task<List<IndexInfo>> GetIndexesAsync(DbConnection connection, string schemaName, string tableName) =>
        GetIndexesInternalAsync((MySqlConnection)connection, schemaName, tableName);

    protected override Task<List<RelationshipInfo>> GetRelationshipsAsync(DbConnection connection, string schemaName) =>
        GetRelationshipsInternalAsync((MySqlConnection)connection, schemaName);

    protected override Task<List<ScalarFunctionInfo>> GetScalarFunctionsAsync(DbConnection connection, string schemaName) =>
        GetScalarFunctionsInternalAsync((MySqlConnection)connection, schemaName);

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
}

using MySqlConnector;
using ObjMapper.Models;
using ObjMapper.Services.TypeInference;

namespace ObjMapper.Services;

/// <summary>
/// Extracts schema from MySQL databases.
/// </summary>
public class MySqlSchemaExtractor : IDatabaseSchemaExtractor
{
    public Task<DatabaseSchema> ExtractSchemaAsync(string connectionString, string? schemaFilter = null) =>
        ExtractSchemaAsync(connectionString, schemaFilter, enableTypeInference: false);

    public async Task<DatabaseSchema> ExtractSchemaAsync(string connectionString, string? schemaFilter, bool enableTypeInference)
    {
        var schema = new DatabaseSchema();
        
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        
        var databaseName = schemaFilter ?? connection.Database;
        
        // Get all tables
        var tables = await GetTablesAsync(connection, databaseName);
        
        foreach (var tableName in tables)
        {
            var tableInfo = new TableInfo
            {
                Schema = databaseName,
                Name = tableName
            };
            
            // Get columns
            tableInfo.Columns = await GetColumnsAsync(connection, databaseName, tableName);
            
            // Analyze columns for potential boolean types if type inference is enabled
            if (enableTypeInference)
            {
                var booleanAnalysis = await BooleanColumnAnalyzer.AnalyzeColumnsAsync(
                    connection, databaseName, tableName, tableInfo.Columns, DatabaseType.MySql);
                
                foreach (var column in tableInfo.Columns)
                {
                    if (booleanAnalysis.TryGetValue(column.Column, out var couldBeBoolean) && couldBeBoolean)
                    {
                        column.InferredAsBoolean = true;
                    }
                }
            }
            
            // Get indexes
            tableInfo.Indexes = await GetIndexesAsync(connection, databaseName, tableName);
            
            schema.Tables.Add(tableInfo);
        }
        
        // Get relationships
        schema.Relationships = await GetRelationshipsAsync(connection, databaseName);
        
        // Populate table-level relationships (outgoing and incoming)
        PopulateTableRelationships(schema);
        
        // Get scalar functions
        schema.ScalarFunctions = await GetScalarFunctionsAsync(connection, databaseName);
        
        return schema;
    }
    
    /// <summary>
    /// Populates the OutgoingRelationships and IncomingRelationships for each table.
    /// </summary>
    private static void PopulateTableRelationships(DatabaseSchema schema)
    {
        foreach (var table in schema.Tables)
        {
            var fullTableName = string.IsNullOrEmpty(table.Schema) 
                ? table.Name 
                : $"{table.Schema}.{table.Name}";

            // Outgoing relationships: where this table has foreign keys pointing to other tables
            table.OutgoingRelationships = [.. schema.Relationships
                .Where(r => r.FullTableFrom.Equals(fullTableName, StringComparison.OrdinalIgnoreCase) ||
                           r.TableFrom.Equals(table.Name, StringComparison.OrdinalIgnoreCase))];

            // Incoming relationships: where other tables have foreign keys pointing to this table
            table.IncomingRelationships = [.. schema.Relationships
                .Where(r => r.FullTableTo.Equals(fullTableName, StringComparison.OrdinalIgnoreCase) ||
                           r.TableTo.Equals(table.Name, StringComparison.OrdinalIgnoreCase))];
        }
    }
    
    public async Task<bool> TestConnectionAsync(string connectionString)
    {
        try
        {
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    private static async Task<List<string>> GetTablesAsync(MySqlConnection connection, string databaseName)
    {
        var tables = new List<string>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT table_name 
            FROM information_schema.tables 
            WHERE table_schema = @database AND table_type = 'BASE TABLE'
            ORDER BY table_name";
        command.Parameters.AddWithValue("@database", databaseName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }
        
        return tables;
    }
    
    private static async Task<List<ColumnInfo>> GetColumnsAsync(MySqlConnection connection, string databaseName, string tableName)
    {
        var columns = new List<ColumnInfo>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                column_name, 
                data_type, 
                is_nullable,
                column_comment
            FROM information_schema.columns 
            WHERE table_schema = @database AND table_name = @table
            ORDER BY ordinal_position";
        command.Parameters.AddWithValue("@database", databaseName);
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
    
    private static async Task<List<IndexInfo>> GetIndexesAsync(MySqlConnection connection, string databaseName, string tableName)
    {
        var indexes = new List<IndexInfo>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                index_name,
                GROUP_CONCAT(column_name ORDER BY seq_in_index) as columns,
                NOT non_unique as is_unique,
                index_type
            FROM information_schema.statistics 
            WHERE table_schema = @database 
              AND table_name = @table
              AND index_name != 'PRIMARY'
            GROUP BY index_name, non_unique, index_type";
        command.Parameters.AddWithValue("@database", databaseName);
        command.Parameters.AddWithValue("@table", tableName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var columnList = reader.GetString(1);
            indexes.Add(new IndexInfo
            {
                Schema = databaseName,
                Table = tableName,
                Name = reader.GetString(0),
                Key = columnList,
                Type = reader.GetBoolean(2) ? "unique" : reader.GetString(3).ToLower()
            });
        }
        
        return indexes;
    }
    
    private static async Task<List<RelationshipInfo>> GetRelationshipsAsync(MySqlConnection connection, string databaseName)
    {
        var relationships = new List<RelationshipInfo>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT
                constraint_name,
                table_schema,
                table_name,
                column_name,
                referenced_table_schema,
                referenced_table_name,
                referenced_column_name
            FROM information_schema.key_column_usage
            WHERE table_schema = @database
              AND referenced_table_name IS NOT NULL
            ORDER BY constraint_name, ordinal_position";
        command.Parameters.AddWithValue("@database", databaseName);
        
        var fkGroups = new Dictionary<string, (RelationshipInfo rel, List<string> keys, List<string> foreigns)>();
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
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
                group = (rel, new List<string>(), new List<string>());
                fkGroups[constraintName] = group;
            }
            
            group.keys.Add(foreignColumn);
            group.foreigns.Add(columnName);
        }
        
        foreach (var (_, (rel, keys, foreigns)) in fkGroups)
        {
            rel.Key = string.Join(",", keys);
            rel.Foreign = string.Join(",", foreigns);
            relationships.Add(rel);
        }
        
        return relationships;
    }
    
    private static async Task<List<ScalarFunctionInfo>> GetScalarFunctionsAsync(MySqlConnection connection, string databaseName)
    {
        var functions = new List<ScalarFunctionInfo>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                routine_schema,
                routine_name,
                COALESCE(data_type, 'varchar') AS return_type
            FROM information_schema.routines
            WHERE routine_schema = @database
              AND routine_type = 'FUNCTION'
            ORDER BY routine_name";
        command.Parameters.AddWithValue("@database", databaseName);
        
        var functionList = new List<(string schema, string name, string returnType)>();
        
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                functionList.Add((
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2)
                ));
            }
        }
        
        // Get parameters for each function
        foreach (var (funcSchema, funcName, returnType) in functionList)
        {
            var functionInfo = new ScalarFunctionInfo
            {
                Schema = funcSchema,
                Name = funcName,
                ReturnType = returnType
            };
            
            functionInfo.Parameters = await GetFunctionParametersAsync(connection, funcSchema, funcName);
            functions.Add(functionInfo);
        }
        
        return functions;
    }
    
    private static async Task<List<ScalarFunctionParameter>> GetFunctionParametersAsync(MySqlConnection connection, string databaseName, string functionName)
    {
        var parameters = new List<ScalarFunctionParameter>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                parameter_name,
                data_type,
                ordinal_position
            FROM information_schema.parameters
            WHERE specific_schema = @database 
              AND specific_name = @function
              AND parameter_mode = 'IN'
            ORDER BY ordinal_position";
        command.Parameters.AddWithValue("@database", databaseName);
        command.Parameters.AddWithValue("@function", functionName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var paramName = reader.IsDBNull(0) ? $"p{reader.GetInt32(2)}" : reader.GetString(0);
            parameters.Add(new ScalarFunctionParameter
            {
                Name = paramName,
                DataType = reader.GetString(1),
                OrdinalPosition = reader.GetInt32(2)
            });
        }
        
        return parameters;
    }
}

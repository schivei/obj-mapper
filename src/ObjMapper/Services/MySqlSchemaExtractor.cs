using MySqlConnector;
using ObjMapper.Models;

namespace ObjMapper.Services;

/// <summary>
/// Extracts schema from MySQL databases.
/// </summary>
public class MySqlSchemaExtractor : IDatabaseSchemaExtractor
{
    public async Task<DatabaseSchema> ExtractSchemaAsync(string connectionString, string? schemaFilter = null)
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
            
            // Get indexes
            tableInfo.Indexes = await GetIndexesAsync(connection, databaseName, tableName);
            
            schema.Tables.Add(tableInfo);
        }
        
        // Get relationships
        schema.Relationships = await GetRelationshipsAsync(connection, databaseName);
        
        return schema;
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
}

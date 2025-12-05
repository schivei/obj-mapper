using System.Data.Common;
using Microsoft.Data.Sqlite;
using ObjMapper.Models;

namespace ObjMapper.Services;

/// <summary>
/// Extracts schema from SQLite databases.
/// </summary>
public class SqliteSchemaExtractor : IDatabaseSchemaExtractor
{
    public async Task<DatabaseSchema> ExtractSchemaAsync(string connectionString, string? schemaFilter = null)
    {
        var schema = new DatabaseSchema();
        
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        
        // Get all tables
        var tables = await GetTablesAsync(connection);
        
        foreach (var tableName in tables)
        {
            var tableInfo = new TableInfo
            {
                Schema = "main",
                Name = tableName
            };
            
            // Get columns
            tableInfo.Columns = await GetColumnsAsync(connection, tableName);
            
            // Get indexes
            tableInfo.Indexes = await GetIndexesAsync(connection, tableName);
            
            schema.Tables.Add(tableInfo);
        }
        
        // Get relationships
        schema.Relationships = await GetRelationshipsAsync(connection, tables);
        
        return schema;
    }
    
    public async Task<bool> TestConnectionAsync(string connectionString)
    {
        try
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    private static async Task<List<string>> GetTablesAsync(SqliteConnection connection)
    {
        var tables = new List<string>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }
        
        return tables;
    }
    
    private static async Task<List<ColumnInfo>> GetColumnsAsync(SqliteConnection connection, string tableName)
    {
        var columns = new List<ColumnInfo>();
        
        // Validate table name
        if (!IsValidIdentifier(tableName))
            return columns;
        
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info([{tableName}])";
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnInfo
            {
                Schema = "main",
                Table = tableName,
                Column = reader.GetString(1),
                Type = reader.GetString(2),
                Nullable = reader.GetInt32(3) == 0, // notnull = 0 means nullable
                Comment = string.Empty
            });
        }
        
        return columns;
    }
    
    private static async Task<List<IndexInfo>> GetIndexesAsync(SqliteConnection connection, string tableName)
    {
        var indexes = new List<IndexInfo>();
        
        if (!IsValidIdentifier(tableName))
            return indexes;
        
        // Get index list
        await using var listCommand = connection.CreateCommand();
        listCommand.CommandText = $"PRAGMA index_list([{tableName}])";
        
        var indexList = new List<(string name, bool unique)>();
        await using (var reader = await listCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var indexName = reader.GetString(1);
                var unique = reader.GetInt32(2) == 1;
                
                // Skip auto-created indexes
                if (!indexName.StartsWith("sqlite_"))
                {
                    indexList.Add((indexName, unique));
                }
            }
        }
        
        // Get columns for each index
        foreach (var (indexName, unique) in indexList)
        {
            if (!IsValidIdentifier(indexName))
                continue;
                
            await using var infoCommand = connection.CreateCommand();
            infoCommand.CommandText = $"PRAGMA index_info([{indexName}])";
            
            var columnsList = new List<string>();
            await using (var reader = await infoCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    columnsList.Add(reader.GetString(2));
                }
            }
            
            indexes.Add(new IndexInfo
            {
                Schema = "main",
                Table = tableName,
                Name = indexName,
                Key = string.Join(",", columnsList),
                Type = unique ? "unique" : "btree"
            });
        }
        
        return indexes;
    }
    
    private static async Task<List<RelationshipInfo>> GetRelationshipsAsync(SqliteConnection connection, List<string> tables)
    {
        var relationships = new List<RelationshipInfo>();
        
        foreach (var tableName in tables)
        {
            if (!IsValidIdentifier(tableName))
                continue;
                
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA foreign_key_list([{tableName}])";
            
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var refTable = reader.GetString(2);
                var fromColumn = reader.GetString(3);
                var toColumn = reader.GetString(4);
                
                relationships.Add(new RelationshipInfo
                {
                    Name = $"fk_{tableName}_{refTable}",
                    SchemaFrom = "main",
                    SchemaTo = "main",
                    TableFrom = tableName,
                    TableTo = refTable,
                    Key = toColumn,
                    Foreign = fromColumn
                });
            }
        }
        
        return relationships;
    }
    
    private static bool IsValidIdentifier(string name)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_]*$");
    }
}

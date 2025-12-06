using System.Data.Common;
using Microsoft.Data.Sqlite;
using ObjMapper.Models;

namespace ObjMapper.Services.Extractors;

/// <summary>
/// Extracts schema from SQLite databases.
/// </summary>
public class SqliteSchemaExtractor : BaseSchemaExtractor
{
    protected override DatabaseType DatabaseType => DatabaseType.Sqlite;
    protected override string DefaultSchemaName => "main";

    protected override DbConnection CreateConnection(string connectionString) => 
        new SqliteConnection(connectionString);

    protected override async Task<List<(string name, string schema)>> GetTablesAsync(DbConnection connection, string schemaName)
    {
        var tables = new List<(string name, string schema)>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT name 
            FROM sqlite_master 
            WHERE type='table' AND name NOT LIKE 'sqlite_%'
            ORDER BY name";
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add((reader.GetString(0), "main"));
        }
        
        return tables;
    }

    protected override async Task<List<ColumnInfo>> GetColumnsAsync(DbConnection connection, string schemaName, string tableName)
    {
        var columns = new List<ColumnInfo>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\")";
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnInfo
            {
                Schema = "main",
                Table = tableName,
                Column = reader.GetString(1),  // name
                Type = reader.GetString(2),     // type
                Nullable = reader.GetInt32(3) == 0,  // notnull (0 = nullable)
                Comment = string.Empty
            });
        }
        
        return columns;
    }

    protected override async Task<List<IndexInfo>> GetIndexesAsync(DbConnection connection, string schemaName, string tableName)
    {
        var indexes = new List<IndexInfo>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA index_list(\"{tableName}\")";
        
        var indexList = new List<(string name, bool isUnique)>();
        
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var indexName = reader.GetString(1);
                var isUnique = reader.GetInt32(2) == 1;
                
                // Skip auto-created indexes
                if (!indexName.StartsWith("sqlite_autoindex_"))
                {
                    indexList.Add((indexName, isUnique));
                }
            }
        }
        
        foreach (var (indexName, isUnique) in indexList)
        {
            var columns = await GetIndexColumnsAsync(connection, indexName);
            indexes.Add(new IndexInfo
            {
                Schema = "main",
                Table = tableName,
                Name = indexName,
                Key = string.Join(",", columns),
                Type = isUnique ? "unique" : "btree"
            });
        }
        
        return indexes;
    }

    private static async Task<List<string>> GetIndexColumnsAsync(DbConnection connection, string indexName)
    {
        var columns = new List<string>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA index_info(\"{indexName}\")";
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(2)); // name column
        }
        
        return columns;
    }

    protected override async Task<List<RelationshipInfo>> GetRelationshipsAsync(DbConnection connection, string schemaName)
    {
        var relationships = new List<RelationshipInfo>();
        
        // Get all tables first
        var tables = await GetTablesAsync(connection, schemaName);
        
        foreach (var (tableName, _) in tables)
        {
            var tableRelationships = await GetTableForeignKeysAsync(connection, tableName);
            relationships.AddRange(tableRelationships);
        }
        
        return relationships;
    }

    private static async Task<List<RelationshipInfo>> GetTableForeignKeysAsync(DbConnection connection, string tableName)
    {
        var relationships = new List<RelationshipInfo>();
        var fkGroups = new Dictionary<int, (RelationshipInfo rel, List<string> keys, List<string> foreigns)>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA foreign_key_list(\"{tableName}\")";
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var fkId = reader.GetInt32(0);
            var referencedTable = reader.GetString(2);
            var fromColumn = reader.GetString(3);
            var toColumn = reader.GetString(4);
            
            if (!fkGroups.TryGetValue(fkId, out var group))
            {
                var rel = new RelationshipInfo
                {
                    Name = $"fk_{tableName}_{referencedTable}_{fkId}",
                    SchemaFrom = "main",
                    SchemaTo = "main",
                    TableFrom = tableName,
                    TableTo = referencedTable
                };
                group = (rel, [], []);
                fkGroups[fkId] = group;
            }
            
            group.keys.Add(toColumn);
            group.foreigns.Add(fromColumn);
        }
        
        foreach (var (_, (rel, keys, foreigns)) in fkGroups)
        {
            rel.Key = string.Join(",", keys);
            rel.Foreign = string.Join(",", foreigns);
            relationships.Add(rel);
        }
        
        return relationships;
    }

    protected override Task<List<ScalarFunctionInfo>> GetScalarFunctionsAsync(DbConnection connection, string schemaName)
    {
        // SQLite doesn't support user-defined functions stored in the database
        // They are registered at runtime via application code
        return Task.FromResult(new List<ScalarFunctionInfo>());
    }
}

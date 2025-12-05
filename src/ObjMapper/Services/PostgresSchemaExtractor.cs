using Npgsql;
using ObjMapper.Models;

namespace ObjMapper.Services;

/// <summary>
/// Extracts schema from PostgreSQL databases.
/// </summary>
public class PostgresSchemaExtractor : IDatabaseSchemaExtractor
{
    public async Task<DatabaseSchema> ExtractSchemaAsync(string connectionString, string? schemaFilter = null)
    {
        var schema = new DatabaseSchema();
        var schemaName = schemaFilter ?? "public";
        
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        
        // Get all tables
        var tables = await GetTablesAsync(connection, schemaName);
        
        foreach (var (tableName, tableSchema) in tables)
        {
            var tableInfo = new TableInfo
            {
                Schema = tableSchema,
                Name = tableName
            };
            
            // Get columns
            tableInfo.Columns = await GetColumnsAsync(connection, tableSchema, tableName);
            
            // Get indexes
            tableInfo.Indexes = await GetIndexesAsync(connection, tableSchema, tableName);
            
            schema.Tables.Add(tableInfo);
        }
        
        // Get relationships
        schema.Relationships = await GetRelationshipsAsync(connection, schemaName);
        
        return schema;
    }
    
    public async Task<bool> TestConnectionAsync(string connectionString)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    private static async Task<List<(string name, string schema)>> GetTablesAsync(NpgsqlConnection connection, string schemaName)
    {
        var tables = new List<(string name, string schema)>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT table_name, table_schema 
            FROM information_schema.tables 
            WHERE table_schema = @schema AND table_type = 'BASE TABLE'
            ORDER BY table_name";
        command.Parameters.AddWithValue("schema", schemaName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add((reader.GetString(0), reader.GetString(1)));
        }
        
        return tables;
    }
    
    private static async Task<List<ColumnInfo>> GetColumnsAsync(NpgsqlConnection connection, string schemaName, string tableName)
    {
        var columns = new List<ColumnInfo>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                column_name, 
                data_type, 
                is_nullable,
                COALESCE(col_description((table_schema || '.' || table_name)::regclass, ordinal_position), '') as comment
            FROM information_schema.columns 
            WHERE table_schema = @schema AND table_name = @table
            ORDER BY ordinal_position";
        command.Parameters.AddWithValue("schema", schemaName);
        command.Parameters.AddWithValue("table", tableName);
        
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
    
    private static async Task<List<IndexInfo>> GetIndexesAsync(NpgsqlConnection connection, string schemaName, string tableName)
    {
        var indexes = new List<IndexInfo>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                i.relname as index_name,
                array_agg(a.attname ORDER BY k.n) as column_names,
                ix.indisunique as is_unique
            FROM pg_index ix
            JOIN pg_class i ON ix.indexrelid = i.oid
            JOIN pg_class t ON ix.indrelid = t.oid
            JOIN pg_namespace n ON t.relnamespace = n.oid
            JOIN LATERAL unnest(ix.indkey) WITH ORDINALITY AS k(attnum, n) ON true
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum
            WHERE n.nspname = @schema 
              AND t.relname = @table
              AND NOT ix.indisprimary
            GROUP BY i.relname, ix.indisunique";
        command.Parameters.AddWithValue("schema", schemaName);
        command.Parameters.AddWithValue("table", tableName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var columnNames = (string[])reader.GetValue(1);
            indexes.Add(new IndexInfo
            {
                Schema = schemaName,
                Table = tableName,
                Name = reader.GetString(0),
                Key = string.Join(",", columnNames),
                Type = reader.GetBoolean(2) ? "unique" : "btree"
            });
        }
        
        return indexes;
    }
    
    private static async Task<List<RelationshipInfo>> GetRelationshipsAsync(NpgsqlConnection connection, string schemaName)
    {
        var relationships = new List<RelationshipInfo>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT
                tc.constraint_name,
                tc.table_schema,
                tc.table_name,
                kcu.column_name,
                ccu.table_schema AS foreign_table_schema,
                ccu.table_name AS foreign_table_name,
                ccu.column_name AS foreign_column_name
            FROM information_schema.table_constraints AS tc
            JOIN information_schema.key_column_usage AS kcu
              ON tc.constraint_name = kcu.constraint_name
              AND tc.table_schema = kcu.table_schema
            JOIN information_schema.constraint_column_usage AS ccu
              ON ccu.constraint_name = tc.constraint_name
              AND ccu.table_schema = tc.table_schema
            WHERE tc.constraint_type = 'FOREIGN KEY'
              AND tc.table_schema = @schema
            ORDER BY tc.constraint_name, kcu.ordinal_position";
        command.Parameters.AddWithValue("schema", schemaName);
        
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

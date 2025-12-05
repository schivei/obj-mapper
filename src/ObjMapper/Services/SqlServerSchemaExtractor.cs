using Microsoft.Data.SqlClient;
using ObjMapper.Models;

namespace ObjMapper.Services;

/// <summary>
/// Extracts schema from SQL Server databases.
/// </summary>
public class SqlServerSchemaExtractor : IDatabaseSchemaExtractor
{
    public async Task<DatabaseSchema> ExtractSchemaAsync(string connectionString, string? schemaFilter = null)
    {
        var schema = new DatabaseSchema();
        var schemaName = schemaFilter ?? "dbo";
        
        await using var connection = new SqlConnection(connectionString);
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
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    private static async Task<List<(string name, string schema)>> GetTablesAsync(SqlConnection connection, string schemaName)
    {
        var tables = new List<(string name, string schema)>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT TABLE_NAME, TABLE_SCHEMA 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = @schema
            ORDER BY TABLE_NAME";
        command.Parameters.AddWithValue("@schema", schemaName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add((reader.GetString(0), reader.GetString(1)));
        }
        
        return tables;
    }
    
    private static async Task<List<ColumnInfo>> GetColumnsAsync(SqlConnection connection, string schemaName, string tableName)
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
        command.Parameters.AddWithValue("@schema", schemaName);
        command.Parameters.AddWithValue("@table", tableName);
        
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
    
    private static async Task<List<IndexInfo>> GetIndexesAsync(SqlConnection connection, string schemaName, string tableName)
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
        command.Parameters.AddWithValue("@schema", schemaName);
        command.Parameters.AddWithValue("@table", tableName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var columnList = reader.GetString(1);
            indexes.Add(new IndexInfo
            {
                Schema = schemaName,
                Table = tableName,
                Name = reader.GetString(0),
                Key = columnList,
                Type = reader.GetBoolean(2) ? "unique" : "nonclustered"
            });
        }
        
        return indexes;
    }
    
    private static async Task<List<RelationshipInfo>> GetRelationshipsAsync(SqlConnection connection, string schemaName)
    {
        var relationships = new List<RelationshipInfo>();
        
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
        command.Parameters.AddWithValue("@schema", schemaName);
        
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

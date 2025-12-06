using System.Data.Common;
using ObjMapper.Models;
using ObjMapper.Services.TypeInference;

namespace ObjMapper.Services;

/// <summary>
/// Abstract base class for database schema extractors implementing the Template Method pattern.
/// Provides common functionality for extracting schema information from various databases.
/// </summary>
public abstract class BaseSchemaExtractor : IDatabaseSchemaExtractor
{
    /// <summary>
    /// The database type this extractor handles.
    /// </summary>
    protected abstract DatabaseType DatabaseType { get; }
    
    /// <summary>
    /// The default schema name for this database type.
    /// </summary>
    protected abstract string DefaultSchemaName { get; }

    public Task<DatabaseSchema> ExtractSchemaAsync(string connectionString, string? schemaFilter = null) =>
        ExtractSchemaAsync(connectionString, schemaFilter, enableTypeInference: false);

    public virtual async Task<DatabaseSchema> ExtractSchemaAsync(string connectionString, string? schemaFilter, bool enableTypeInference)
    {
        var schema = new DatabaseSchema();
        var schemaName = schemaFilter ?? DefaultSchemaName;
        
        await using var connection = CreateConnection(connectionString);
        await connection.OpenAsync();
        
        // Template method: Get all tables
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
            
            // Analyze columns for potential boolean types if type inference is enabled
            if (enableTypeInference)
            {
                await AnalyzeBooleanColumnsAsync(connection, tableInfo);
            }
            
            // Get indexes
            tableInfo.Indexes = await GetIndexesAsync(connection, tableSchema, tableName);
            
            schema.Tables.Add(tableInfo);
        }
        
        // Get relationships
        schema.Relationships = await GetRelationshipsAsync(connection, schemaName);
        
        // Populate table-level relationships (common logic)
        PopulateTableRelationships(schema);
        
        // Get scalar functions
        schema.ScalarFunctions = await GetScalarFunctionsAsync(connection, schemaName);
        
        return schema;
    }

    public async Task<bool> TestConnectionAsync(string connectionString)
    {
        try
        {
            await using var connection = CreateConnection(connectionString);
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a database connection for this database type.
    /// </summary>
    protected abstract DbConnection CreateConnection(string connectionString);
    
    /// <summary>
    /// Gets all tables from the database.
    /// </summary>
    protected abstract Task<List<(string name, string schema)>> GetTablesAsync(DbConnection connection, string schemaName);
    
    /// <summary>
    /// Gets all columns for a specific table.
    /// </summary>
    protected abstract Task<List<ColumnInfo>> GetColumnsAsync(DbConnection connection, string schemaName, string tableName);
    
    /// <summary>
    /// Gets all indexes for a specific table.
    /// </summary>
    protected abstract Task<List<IndexInfo>> GetIndexesAsync(DbConnection connection, string schemaName, string tableName);
    
    /// <summary>
    /// Gets all foreign key relationships from the database.
    /// </summary>
    protected abstract Task<List<RelationshipInfo>> GetRelationshipsAsync(DbConnection connection, string schemaName);
    
    /// <summary>
    /// Gets all scalar functions from the database.
    /// </summary>
    protected abstract Task<List<ScalarFunctionInfo>> GetScalarFunctionsAsync(DbConnection connection, string schemaName);

    /// <summary>
    /// Analyzes columns for potential boolean types based on data values.
    /// </summary>
    private async Task AnalyzeBooleanColumnsAsync(DbConnection connection, TableInfo tableInfo)
    {
        var booleanAnalysis = await BooleanColumnAnalyzer.AnalyzeColumnsAsync(
            connection, tableInfo.Schema, tableInfo.Name, tableInfo.Columns, DatabaseType);
        
        foreach (var column in tableInfo.Columns)
        {
            if (booleanAnalysis.TryGetValue(column.Column, out var couldBeBoolean) && couldBeBoolean)
            {
                column.InferredAsBoolean = true;
            }
        }
    }

    /// <summary>
    /// Populates outgoing and incoming relationships for each table.
    /// </summary>
    protected static void PopulateTableRelationships(DatabaseSchema schema)
    {
        foreach (var table in schema.Tables)
        {
            var fullTableName = string.IsNullOrEmpty(table.Schema) 
                ? table.Name 
                : $"{table.Schema}.{table.Name}";

            table.OutgoingRelationships = [.. schema.Relationships
                .Where(r => r.FullTableFrom.Equals(fullTableName, StringComparison.OrdinalIgnoreCase) ||
                           r.TableFrom.Equals(table.Name, StringComparison.OrdinalIgnoreCase))];

            table.IncomingRelationships = [.. schema.Relationships
                .Where(r => r.FullTableTo.Equals(fullTableName, StringComparison.OrdinalIgnoreCase) ||
                           r.TableTo.Equals(table.Name, StringComparison.OrdinalIgnoreCase))];
        }
    }

    /// <summary>
    /// Helper method to process foreign key groups into relationship info objects.
    /// </summary>
    protected static List<RelationshipInfo> ProcessForeignKeyGroups(
        Dictionary<string, (RelationshipInfo rel, List<string> keys, List<string> foreigns)> fkGroups)
    {
        var relationships = new List<RelationshipInfo>();
        
        foreach (var (_, (rel, keys, foreigns)) in fkGroups)
        {
            rel.Key = string.Join(",", keys);
            rel.Foreign = string.Join(",", foreigns);
            relationships.Add(rel);
        }
        
        return relationships;
    }

    /// <summary>
    /// Helper method to add a parameter to a command in a database-agnostic way.
    /// </summary>
    protected static void AddParameter(DbCommand command, string name, object value)
    {
        var param = command.CreateParameter();
        param.ParameterName = name;
        param.Value = value;
        command.Parameters.Add(param);
    }
}

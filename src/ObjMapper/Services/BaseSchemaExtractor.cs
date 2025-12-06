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
        ExtractSchemaAsync(connectionString, new SchemaExtractionOptions 
        { 
            SchemaFilter = schemaFilter, 
            EnableTypeInference = false, 
            EnableDataSampling = false 
        });

    public Task<DatabaseSchema> ExtractSchemaAsync(string connectionString, string? schemaFilter, bool enableTypeInference) =>
        ExtractSchemaAsync(connectionString, new SchemaExtractionOptions 
        { 
            SchemaFilter = schemaFilter, 
            EnableTypeInference = enableTypeInference, 
            EnableDataSampling = true 
        });

    public Task<DatabaseSchema> ExtractSchemaAsync(string connectionString, string? schemaFilter, bool enableTypeInference, bool enableDataSampling) =>
        ExtractSchemaAsync(connectionString, new SchemaExtractionOptions 
        { 
            SchemaFilter = schemaFilter, 
            EnableTypeInference = enableTypeInference, 
            EnableDataSampling = enableDataSampling 
        });

    public virtual async Task<DatabaseSchema> ExtractSchemaAsync(string connectionString, SchemaExtractionOptions options)
    {
        var schema = new DatabaseSchema();
        var schemaName = options.SchemaFilter ?? DefaultSchemaName;
        
        await using var connection = CreateConnection(connectionString);
        await connection.OpenAsync();
        
        // Template method: Get all tables
        var tables = await GetTablesAsync(connection, schemaName);
        
        // Get views if enabled
        if (options.IncludeViews)
        {
            var views = await GetViewsAsync(connection, schemaName);
            tables.AddRange(views);
        }
        
        foreach (var (tableName, tableSchema) in tables)
        {
            var tableInfo = new TableInfo
            {
                Schema = tableSchema,
                Name = tableName
            };
            
            // Get columns
            tableInfo.Columns = await GetColumnsAsync(connection, tableSchema, tableName);
            
            // Apply name-based inference first (fast, no DB queries)
            if (options.EnableTypeInference)
            {
                ApplyNameBasedInference(tableInfo);
            }
            
            // Analyze columns for potential boolean and GUID types with data sampling
            // Only run expensive queries if data sampling is enabled AND type inference is enabled
            // AND the column wasn't already inferred from its name
            if (options.EnableTypeInference && options.EnableDataSampling)
            {
                await AnalyzeBooleanColumnsAsync(connection, tableInfo, onlyUninferred: true);
                await AnalyzeGuidColumnsAsync(connection, tableInfo, onlyUninferred: true);
            }
            
            // Get indexes
            tableInfo.Indexes = await GetIndexesAsync(connection, tableSchema, tableName);
            
            schema.Tables.Add(tableInfo);
        }
        
        // Get relationships if enabled
        if (options.IncludeRelationships)
        {
            schema.Relationships = await GetRelationshipsAsync(connection, schemaName);
            
            // If no FK relationships found and legacy inference is enabled, infer from naming patterns
            if (schema.Relationships.Count == 0 && options.EnableLegacyRelationshipInference)
            {
                schema.Relationships = InferRelationshipsFromNaming(schema.Tables);
            }
            
            // Populate table-level relationships (common logic)
            PopulateTableRelationships(schema);
        }
        
        // Get scalar functions if enabled
        if (options.IncludeUserDefinedFunctions)
        {
            schema.ScalarFunctions = await GetScalarFunctionsAsync(connection, schemaName);
        }
        
        // Get stored procedures if enabled
        if (options.IncludeStoredProcedures)
        {
            schema.StoredProcedures = await GetStoredProceduresAsync(connection, schemaName);
        }
        
        return schema;
    }

    /// <summary>
    /// Infers relationships from column and table naming patterns (legacy mode).
    /// Detects patterns like: table_id, tableId, table_fk, id_table
    /// </summary>
    private static List<RelationshipInfo> InferRelationshipsFromNaming(List<TableInfo> tables)
    {
        var relationships = new List<RelationshipInfo>();
        var tableNames = tables.Select(t => t.Name.ToLowerInvariant()).ToHashSet();
        var tablesByName = tables.ToDictionary(t => t.Name.ToLowerInvariant(), t => t);
        
        foreach (var table in tables)
        {
            foreach (var column in table.Columns)
            {
                var colName = column.Column.ToLowerInvariant();
                
                // Skip primary key columns
                if (colName == "id" || colName == $"{table.Name.ToLowerInvariant()}_id")
                    continue;
                
                // Pattern 1: column ends with _id (e.g., user_id, customer_id)
                if (colName.EndsWith("_id"))
                {
                    var potentialTable = colName[..^3]; // Remove _id
                    if (TryFindReferencedTable(potentialTable, tablesByName, out var referencedTable))
                    {
                        relationships.Add(CreateInferredRelationship(table, column, referencedTable!));
                    }
                }
                // Pattern 2: column ends with Id (camelCase, e.g., userId, customerId)
                else if (colName.EndsWith("id") && colName.Length > 2)
                {
                    var potentialTable = colName[..^2]; // Remove Id
                    if (TryFindReferencedTable(potentialTable, tablesByName, out var referencedTable))
                    {
                        relationships.Add(CreateInferredRelationship(table, column, referencedTable!));
                    }
                }
                // Pattern 3: column ends with _fk (e.g., user_fk)
                else if (colName.EndsWith("_fk"))
                {
                    var potentialTable = colName[..^3]; // Remove _fk
                    if (TryFindReferencedTable(potentialTable, tablesByName, out var referencedTable))
                    {
                        relationships.Add(CreateInferredRelationship(table, column, referencedTable!));
                    }
                }
                // Pattern 4: column starts with fk_ (e.g., fk_user)
                else if (colName.StartsWith("fk_"))
                {
                    var potentialTable = colName[3..]; // Remove fk_
                    if (TryFindReferencedTable(potentialTable, tablesByName, out var referencedTable))
                    {
                        relationships.Add(CreateInferredRelationship(table, column, referencedTable!));
                    }
                }
            }
        }
        
        return relationships;
    }
    
    private static bool TryFindReferencedTable(string potentialName, Dictionary<string, TableInfo> tablesByName, out TableInfo? table)
    {
        table = null;
        var normalizedName = potentialName.ToLowerInvariant();
        
        // Direct match
        if (tablesByName.TryGetValue(normalizedName, out table))
            return true;
        
        // Try plural form (simple: add 's')
        if (tablesByName.TryGetValue(normalizedName + "s", out table))
            return true;
        
        // Try singular form (simple: remove 's')
        if (normalizedName.EndsWith("s") && tablesByName.TryGetValue(normalizedName[..^1], out table))
            return true;
        
        // Try with underscores replaced (user_account -> useraccount)
        var withoutUnderscores = normalizedName.Replace("_", "");
        if (tablesByName.TryGetValue(withoutUnderscores, out table))
            return true;
        
        return false;
    }
    
    private static RelationshipInfo CreateInferredRelationship(TableInfo fromTable, ColumnInfo column, TableInfo toTable)
    {
        // Find the primary key column in the referenced table
        var pkColumn = toTable.Columns.FirstOrDefault(c => 
            c.Column.Equals("id", StringComparison.OrdinalIgnoreCase) ||
            c.Column.Equals($"{toTable.Name}_id", StringComparison.OrdinalIgnoreCase));
        
        var pkName = pkColumn?.Column ?? "id";
        
        return new RelationshipInfo
        {
            Name = $"inferred_fk_{fromTable.Name}_{column.Column}",
            SchemaFrom = fromTable.Schema,
            SchemaTo = toTable.Schema,
            TableFrom = fromTable.Name,
            TableTo = toTable.Name,
            Key = pkName,
            Foreign = column.Column
        };
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
    /// Gets all views from the database.
    /// </summary>
    protected virtual Task<List<(string name, string schema)>> GetViewsAsync(DbConnection connection, string schemaName)
    {
        // Default implementation returns empty list - subclasses can override
        return Task.FromResult(new List<(string name, string schema)>());
    }
    
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
    /// Gets all stored procedures from the database.
    /// </summary>
    protected abstract Task<List<StoredProcedureInfo>> GetStoredProceduresAsync(DbConnection connection, string schemaName);

    /// <summary>
    /// Applies fast name-based inference without querying the database.
    /// Uses column name patterns like is_*, has_*, *_flag for boolean,
    /// and uuid, *_guid, *_id patterns for GUID inference.
    /// </summary>
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
        // Common boolean naming patterns
        return columnName.StartsWith("is_") ||
               columnName.StartsWith("has_") ||
               columnName.StartsWith("can_") ||
               columnName.StartsWith("should_") ||
               columnName.StartsWith("will_") ||
               columnName.StartsWith("allow_") ||
               columnName.StartsWith("enable_") ||
               columnName.StartsWith("disable_") ||
               columnName.EndsWith("_flag") ||
               columnName.EndsWith("_enabled") ||
               columnName.EndsWith("_disabled") ||
               columnName.EndsWith("_active") ||
               columnName.EndsWith("_deleted") ||
               columnName.EndsWith("_visible") ||
               columnName.EndsWith("_hidden") ||
               columnName.EndsWith("_required") ||
               columnName == "active" ||
               columnName == "enabled" ||
               columnName == "disabled" ||
               columnName == "deleted" ||
               columnName == "visible" ||
               columnName == "hidden" ||
               columnName == "published" ||
               columnName == "approved" ||
               columnName == "verified" ||
               columnName == "confirmed";
    }
    
    private static bool IsSmallIntegerType(string typeName)
    {
        return typeName.Contains("tinyint") ||
               typeName.Contains("smallint") ||
               typeName.Contains("bit") ||
               typeName.Contains("boolean") ||
               typeName.Contains("bool") ||
               typeName == "int2" ||
               typeName == "int1";
    }
    
    private static bool IsGuidNamePattern(string columnName)
    {
        return columnName == "uuid" ||
               columnName == "guid" ||
               columnName.EndsWith("_uuid") ||
               columnName.EndsWith("_guid") ||
               columnName == "correlation_id" ||
               columnName == "tracking_id" ||
               columnName == "external_id" ||
               columnName == "request_id" ||
               columnName == "session_id" ||
               columnName == "transaction_id";
    }
    
    private static bool IsGuidCompatibleType(string typeName)
    {
        return typeName.Contains("char(36)") ||
               typeName.Contains("varchar(36)") ||
               typeName.Contains("character(36)") ||
               typeName == "uuid" ||
               typeName == "uniqueidentifier";
    }

    /// <summary>
    /// Analyzes columns for potential boolean types based on data values.
    /// </summary>
    /// <param name="connection">Database connection.</param>
    /// <param name="tableInfo">Table information.</param>
    /// <param name="onlyUninferred">If true, only analyze columns not already inferred from name patterns.</param>
    private async Task AnalyzeBooleanColumnsAsync(DbConnection connection, TableInfo tableInfo, bool onlyUninferred = false)
    {
        // Filter columns - only those not already inferred if onlyUninferred is true
        var columnsToAnalyze = onlyUninferred
            ? tableInfo.Columns.Where(c => !c.InferredAsBoolean).ToList()
            : tableInfo.Columns;
            
        if (!columnsToAnalyze.Any())
            return;
            
        var booleanAnalysis = await BooleanColumnAnalyzer.AnalyzeColumnsAsync(
            connection, tableInfo.Schema, tableInfo.Name, columnsToAnalyze, DatabaseType);
        
        foreach (var column in columnsToAnalyze.Where(c => 
            booleanAnalysis.TryGetValue(c.Column, out var couldBeBoolean) && couldBeBoolean))
        {
            column.InferredAsBoolean = true;
        }
    }
    
    /// <summary>
    /// Analyzes columns for potential GUID types based on data values.
    /// </summary>
    /// <param name="connection">Database connection.</param>
    /// <param name="tableInfo">Table information.</param>
    /// <param name="onlyUninferred">If true, only analyze columns not already inferred from name patterns.</param>
    private async Task AnalyzeGuidColumnsAsync(DbConnection connection, TableInfo tableInfo, bool onlyUninferred = false)
    {
        // Filter columns - only those not already inferred if onlyUninferred is true
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

using System.Data.Common;
using ObjMapper.Models;

namespace ObjMapper.Services.TypeInference;

/// <summary>
/// Analyzes column data to determine if string columns could be GUIDs.
/// </summary>
public static class GuidColumnAnalyzer
{
    private const int MinValidGuidRecords = 10;
    
    /// <summary>
    /// String types that could potentially be GUIDs (36 characters).
    /// </summary>
    private static readonly HashSet<string> PotentialGuidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "char(36)", "varchar(36)", "nchar(36)", "nvarchar(36)", "character(36)"
    };
    
    /// <summary>
    /// Checks if a column type could potentially be a GUID based on type definition.
    /// </summary>
    public static bool IsPotentialGuidType(string dbType)
    {
        var normalizedType = dbType.Trim().ToLowerInvariant();
        return PotentialGuidTypes.Contains(normalizedType) || 
               normalizedType == "char(36)" ||
               normalizedType.StartsWith("varchar(36)") ||
               normalizedType.StartsWith("nvarchar(36)") ||
               normalizedType.StartsWith("character varying(36)");
    }
    
    /// <summary>
    /// Analyzes column data to determine if it contains valid GUIDs.
    /// Requires at least 10 valid GUIDs and no blank/whitespace records.
    /// </summary>
    public static async Task<bool> CouldBeGuidAsync(
        DbConnection connection, 
        string schemaName, 
        string tableName, 
        string columnName,
        DatabaseType databaseType)
    {
        try
        {
            var sql = BuildGuidAnalysisQuery(schemaName, tableName, columnName, databaseType);
            
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            
            var validGuidCount = 0;
            var totalCount = 0;
            var hasBlankValue = false;
            
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                totalCount++;
                
                if (reader.IsDBNull(0))
                    continue; // NULL values are OK, just skip them
                
                var value = reader.GetString(0);
                
                // Check for blank/whitespace-only values
                if (string.IsNullOrWhiteSpace(value))
                {
                    hasBlankValue = true;
                    break; // Disqualify if any blank value found
                }
                
                // Check if it's a valid GUID
                if (Guid.TryParse(value, out _))
                {
                    validGuidCount++;
                }
            }
            
            // Disqualify if any blank values found
            if (hasBlankValue)
                return false;
            
            // Need at least 10 valid GUIDs (or all records if less than 10)
            return validGuidCount >= Math.Min(MinValidGuidRecords, totalCount) && 
                   validGuidCount > 0 &&
                   validGuidCount == totalCount; // All non-null values must be valid GUIDs
        }
        catch
        {
            // If analysis fails, assume it's not a GUID
            return false;
        }
    }
    
    /// <summary>
    /// Analyzes multiple columns in a table for potential GUID values.
    /// </summary>
    public static async Task<Dictionary<string, bool>> AnalyzeColumnsAsync(
        DbConnection connection,
        string schemaName,
        string tableName,
        IEnumerable<ColumnInfo> columns,
        DatabaseType databaseType)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var column in columns)
        {
            if (IsPotentialGuidType(column.Type))
            {
                var couldBeGuid = await CouldBeGuidAsync(
                    connection, schemaName, tableName, column.Column, databaseType);
                result[column.Column] = couldBeGuid;
            }
        }
        
        return result;
    }
    
    private static string BuildGuidAnalysisQuery(
        string schemaName, 
        string tableName, 
        string columnName,
        DatabaseType databaseType)
    {
        // Quote identifiers based on database type
        var (openQuote, closeQuote) = databaseType switch
        {
            DatabaseType.MySql => ("`", "`"),
            DatabaseType.PostgreSql => ("\"", "\""),
            DatabaseType.SqlServer => ("[", "]"),
            DatabaseType.Sqlite => ("\"", "\""),
            DatabaseType.Oracle => ("\"", "\""),
            _ => ("\"", "\"")
        };
        
        var quotedColumn = $"{openQuote}{columnName}{closeQuote}";
        var quotedTable = string.IsNullOrEmpty(schemaName) 
            ? $"{openQuote}{tableName}{closeQuote}"
            : $"{openQuote}{schemaName}{closeQuote}.{openQuote}{tableName}{closeQuote}";
        
        // Get a sample of records to analyze (limit to 100 for performance)
        return databaseType switch
        {
            DatabaseType.SqlServer => $"SELECT TOP 100 {quotedColumn} FROM {quotedTable} WHERE {quotedColumn} IS NOT NULL",
            _ => $"SELECT {quotedColumn} FROM {quotedTable} WHERE {quotedColumn} IS NOT NULL LIMIT 100"
        };
    }
}

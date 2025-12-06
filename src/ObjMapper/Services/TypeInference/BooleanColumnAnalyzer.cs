using System.Data.Common;
using ObjMapper.Models;

namespace ObjMapper.Services.TypeInference;

/// <summary>
/// Analyzes column data to determine if small integer columns could be boolean.
/// </summary>
public static class BooleanColumnAnalyzer
{
    /// <summary>
    /// Small integer types that could potentially be boolean.
    /// </summary>
    private static readonly HashSet<string> SmallIntegerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "tinyint", "smallint", "bit", "int2", "mediumint", "byte"
    };
    
    /// <summary>
    /// Checks if a column type is a small integer that could potentially be boolean.
    /// </summary>
    public static bool IsSmallIntegerType(string dbType)
    {
        var baseType = ExtractBaseType(dbType);
        return SmallIntegerTypes.Contains(baseType);
    }
    
    /// <summary>
    /// Analyzes column data to determine if it only contains boolean-like values (NULL, 0, 1).
    /// </summary>
    public static async Task<bool> CouldBeBooleanAsync(
        DbConnection connection, 
        string schemaName, 
        string tableName, 
        string columnName,
        DatabaseType databaseType)
    {
        try
        {
            var sql = BuildAnalysisQuery(schemaName, tableName, columnName, databaseType);
            
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            
            var distinctValues = new HashSet<object?>();
            
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var value = reader.IsDBNull(0) ? null : reader.GetValue(0);
                distinctValues.Add(value);
                
                // If we find more than 3 distinct values (null, 0, 1), it's not boolean
                if (distinctValues.Count > 3)
                    return false;
            }
            
            // Check if all values are in the boolean set
            return distinctValues.All(v => 
                v is null || 
                (v is int intVal && (intVal == 0 || intVal == 1)) ||
                (v is long longVal && (longVal == 0 || longVal == 1)) ||
                (v is short shortVal && (shortVal == 0 || shortVal == 1)) ||
                (v is byte byteVal && (byteVal == 0 || byteVal == 1)) ||
                (v is sbyte sbyteVal && (sbyteVal == 0 || sbyteVal == 1)) ||
                (v is bool) ||
                (v is decimal decVal && (decVal == 0 || decVal == 1)));
        }
        catch
        {
            // If analysis fails, assume it's not boolean
            return false;
        }
    }
    
    /// <summary>
    /// Analyzes multiple columns in a table for potential boolean values.
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
            if (IsSmallIntegerType(column.Type))
            {
                var couldBeBoolean = await CouldBeBooleanAsync(
                    connection, schemaName, tableName, column.Column, databaseType);
                result[column.Column] = couldBeBoolean;
            }
        }
        
        return result;
    }
    
    private static string BuildAnalysisQuery(
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
            _ => ("\"", "\"")
        };
        
        var quotedColumn = $"{openQuote}{columnName}{closeQuote}";
        var quotedTable = string.IsNullOrEmpty(schemaName) 
            ? $"{openQuote}{tableName}{closeQuote}"
            : $"{openQuote}{schemaName}{closeQuote}.{openQuote}{tableName}{closeQuote}";
        
        // Query to get distinct values - limit to avoid performance issues
        return databaseType switch
        {
            DatabaseType.SqlServer => $"SELECT DISTINCT TOP 10 {quotedColumn} FROM {quotedTable}",
            DatabaseType.MySql => $"SELECT DISTINCT {quotedColumn} FROM {quotedTable} LIMIT 10",
            DatabaseType.PostgreSql => $"SELECT DISTINCT {quotedColumn} FROM {quotedTable} LIMIT 10",
            DatabaseType.Sqlite => $"SELECT DISTINCT {quotedColumn} FROM {quotedTable} LIMIT 10",
            _ => $"SELECT DISTINCT {quotedColumn} FROM {quotedTable} LIMIT 10"
        };
    }
    
    private static string ExtractBaseType(string dbType)
    {
        var parenIndex = dbType.IndexOf('(');
        return parenIndex > 0 ? dbType[..parenIndex].Trim() : dbType.Trim();
    }
}

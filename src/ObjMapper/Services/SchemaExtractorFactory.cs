using ObjMapper.Models;

namespace ObjMapper.Services;

/// <summary>
/// Factory for creating database schema extractors based on database type.
/// </summary>
public static class SchemaExtractorFactory
{
    /// <summary>
    /// Creates a schema extractor for the specified database type.
    /// </summary>
    public static IDatabaseSchemaExtractor Create(DatabaseType databaseType)
    {
        return databaseType switch
        {
            DatabaseType.Sqlite => new SqliteSchemaExtractor(),
            DatabaseType.PostgreSql => new PostgresSchemaExtractor(),
            DatabaseType.MySql => new MySqlSchemaExtractor(),
            DatabaseType.SqlServer => new SqlServerSchemaExtractor(),
            DatabaseType.Oracle => throw new NotSupportedException("Oracle schema extraction is not yet supported. Please use CSV files."),
            _ => throw new ArgumentException($"Unknown database type: {databaseType}")
        };
    }
    
    /// <summary>
    /// Tries to detect the database type from a connection string.
    /// </summary>
    public static DatabaseType? DetectDatabaseType(string connectionString)
    {
        var lowerConn = connectionString.ToLowerInvariant();
        
        // SQLite detection
        if (lowerConn.Contains("data source=") && 
            (lowerConn.Contains(".db") || lowerConn.Contains(".sqlite") || lowerConn.Contains(":memory:")))
        {
            return DatabaseType.Sqlite;
        }
        
        // PostgreSQL detection
        if (lowerConn.Contains("host=") && lowerConn.Contains("database=") && !lowerConn.Contains("server="))
        {
            return DatabaseType.PostgreSql;
        }
        
        // MySQL detection
        if (lowerConn.Contains("server=") && lowerConn.Contains("database=") && !lowerConn.Contains("initial catalog"))
        {
            return DatabaseType.MySql;
        }
        
        // SQL Server detection
        if (lowerConn.Contains("server=") && (lowerConn.Contains("initial catalog") || lowerConn.Contains("database=")))
        {
            // Check for SQL Server specific patterns
            if (lowerConn.Contains("trusted_connection") || 
                lowerConn.Contains("integrated security") ||
                lowerConn.Contains("trustservercertificate") ||
                lowerConn.Contains("user id="))
            {
                return DatabaseType.SqlServer;
            }
        }
        
        return null;
    }
}

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
        
        // SQLite detection - most specific first
        if (lowerConn.Contains("data source=") && 
            (lowerConn.Contains(".db") || lowerConn.Contains(".sqlite") || lowerConn.Contains(":memory:")))
        {
            return DatabaseType.Sqlite;
        }
        
        // PostgreSQL detection - uses "host=" instead of "server="
        if (lowerConn.Contains("host=") && !lowerConn.Contains("server="))
        {
            return DatabaseType.PostgreSql;
        }
        
        // SQL Server detection - check specific SQL Server patterns first
        // SQL Server typically uses: "Server=", "Initial Catalog=", "User Id=", "Trusted_Connection=", 
        // "Integrated Security=", "TrustServerCertificate=", "MultipleActiveResultSets="
        if (lowerConn.Contains("server=") || lowerConn.Contains("data source="))
        {
            // SQL Server specific patterns
            if (lowerConn.Contains("initial catalog") ||
                lowerConn.Contains("trusted_connection") || 
                lowerConn.Contains("integrated security") ||
                lowerConn.Contains("trustservercertificate") ||
                lowerConn.Contains("multipleactiveresultsets") ||
                lowerConn.Contains("user id=") ||
                lowerConn.Contains("encrypt="))
            {
                return DatabaseType.SqlServer;
            }
        }
        
        // MySQL detection - uses "server=" with "user=" (not "user id=")
        if (lowerConn.Contains("server=") && lowerConn.Contains("database="))
        {
            // MySQL typically uses "user=" or "uid=" instead of "user id="
            if (lowerConn.Contains("user=") && !lowerConn.Contains("user id="))
            {
                return DatabaseType.MySql;
            }
            // MySQL with uid parameter
            if (lowerConn.Contains("uid="))
            {
                return DatabaseType.MySql;
            }
            // If no clear indicator, check for MySQL specific options
            if (lowerConn.Contains("sslmode=") || 
                lowerConn.Contains("charset=") ||
                lowerConn.Contains("allowuservariables="))
            {
                return DatabaseType.MySql;
            }
        }
        
        return null;
    }
}

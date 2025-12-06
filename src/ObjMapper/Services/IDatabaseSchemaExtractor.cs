using ObjMapper.Models;

namespace ObjMapper.Services;

/// <summary>
/// Interface for extracting database schema from a connection.
/// </summary>
public interface IDatabaseSchemaExtractor
{
    /// <summary>
    /// Extracts the complete database schema.
    /// </summary>
    Task<DatabaseSchema> ExtractSchemaAsync(string connectionString, string? schemaFilter = null);
    
    /// <summary>
    /// Extracts the complete database schema with optional type inference.
    /// </summary>
    Task<DatabaseSchema> ExtractSchemaAsync(string connectionString, string? schemaFilter, bool enableTypeInference);
    
    /// <summary>
    /// Tests the database connection.
    /// </summary>
    Task<bool> TestConnectionAsync(string connectionString);
}

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
    /// Extracts the complete database schema with optional type inference and data sampling.
    /// </summary>
    /// <param name="connectionString">The database connection string.</param>
    /// <param name="schemaFilter">Optional schema filter.</param>
    /// <param name="enableTypeInference">Enable ML-based type inference from column metadata.</param>
    /// <param name="enableDataSampling">Enable data sampling queries for type verification (slower but more accurate).</param>
    Task<DatabaseSchema> ExtractSchemaAsync(string connectionString, string? schemaFilter, bool enableTypeInference, bool enableDataSampling);
    
    /// <summary>
    /// Extracts the database schema with full control over extraction options.
    /// </summary>
    /// <param name="connectionString">The database connection string.</param>
    /// <param name="options">Schema extraction options.</param>
    Task<DatabaseSchema> ExtractSchemaAsync(string connectionString, SchemaExtractionOptions options);
    
    /// <summary>
    /// Tests the database connection.
    /// </summary>
    Task<bool> TestConnectionAsync(string connectionString);
}

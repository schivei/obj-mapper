using ObjMapper.Models;

namespace ObjMapper.Generators;

/// <summary>
/// Base interface for code generators.
/// </summary>
public interface ICodeGenerator
{
    /// <summary>
    /// Gets or sets the entity type mode for generating entities.
    /// </summary>
    EntityTypeMode EntityTypeMode { get; set; }
    
    /// <summary>
    /// Generates entity classes.
    /// </summary>
    Dictionary<string, string> GenerateEntities(DatabaseSchema schema);

    /// <summary>
    /// Generates configuration/mapping classes.
    /// </summary>
    Dictionary<string, string> GenerateConfigurations(DatabaseSchema schema);

    /// <summary>
    /// Generates the database context class.
    /// </summary>
    string GenerateDbContext(DatabaseSchema schema, string contextName);
    
    /// <summary>
    /// Generates scalar function mapping classes.
    /// </summary>
    Dictionary<string, string> GenerateScalarFunctions(DatabaseSchema schema);
    
    /// <summary>
    /// Generates stored procedure wrapper classes.
    /// </summary>
    Dictionary<string, string> GenerateStoredProcedures(DatabaseSchema schema);
}

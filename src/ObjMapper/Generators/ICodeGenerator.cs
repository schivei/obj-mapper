using ObjMapper.Models;

namespace ObjMapper.Generators;

/// <summary>
/// Base interface for code generators.
/// </summary>
public interface ICodeGenerator
{
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
}

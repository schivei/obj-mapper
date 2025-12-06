namespace ObjMapper.Models;

/// <summary>
/// Options for schema extraction from database connections.
/// </summary>
public class SchemaExtractionOptions
{
    /// <summary>
    /// Optional schema/database name filter.
    /// </summary>
    public string? SchemaFilter { get; set; }
    
    /// <summary>
    /// Enable ML-based type inference from column metadata.
    /// </summary>
    public bool EnableTypeInference { get; set; } = true;
    
    /// <summary>
    /// Enable data sampling queries for type verification (slower but more accurate).
    /// </summary>
    public bool EnableDataSampling { get; set; } = true;
    
    /// <summary>
    /// Include views in schema extraction.
    /// </summary>
    public bool IncludeViews { get; set; } = true;
    
    /// <summary>
    /// Include stored procedures in schema extraction.
    /// </summary>
    public bool IncludeStoredProcedures { get; set; } = true;
    
    /// <summary>
    /// Include user-defined functions in schema extraction.
    /// </summary>
    public bool IncludeUserDefinedFunctions { get; set; } = true;
    
    /// <summary>
    /// Include relationships in schema extraction.
    /// </summary>
    public bool IncludeRelationships { get; set; } = true;
    
    /// <summary>
    /// Enable legacy relationship inference based on naming patterns.
    /// When enabled, infers relationships from column/table names when no foreign keys exist.
    /// </summary>
    public bool EnableLegacyRelationshipInference { get; set; }
    
    /// <summary>
    /// Creates extraction options from command options.
    /// </summary>
    public static SchemaExtractionOptions FromCommandOptions(CommandOptions options) => new()
    {
        SchemaFilter = options.SchemaFilter,
        EnableTypeInference = options.UseTypeInference,
        EnableDataSampling = options.UseDataSampling,
        IncludeViews = options.IncludeViews,
        IncludeStoredProcedures = options.IncludeProcs,
        IncludeUserDefinedFunctions = options.IncludeUdfs,
        IncludeRelationships = options.IncludeRelationships,
        EnableLegacyRelationshipInference = options.UseLegacyInference
    };
}

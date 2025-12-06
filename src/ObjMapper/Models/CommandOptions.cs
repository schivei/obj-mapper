namespace ObjMapper.Models;

/// <summary>
/// Command-line options for the tool.
/// </summary>
public class CommandOptions
{
    public FileInfo? SchemaFile { get; set; }
    public string? ConnectionString { get; set; }
    public string? SchemaFilter { get; set; }
    public string MappingType { get; set; } = string.Empty;
    public string DatabaseType { get; set; } = string.Empty;
    public FileInfo? RelationshipsFile { get; set; }
    public FileInfo? IndexesFile { get; set; }
    public DirectoryInfo OutputDir { get; set; } = null!;
    public string Namespace { get; set; } = "Generated";
    public string ContextName { get; set; } = "AppDbContext";
    public string EntityMode { get; set; } = "class";
    public string Locale { get; set; } = "en-us";
    public bool NoPluralizer { get; set; }
    
    /// <summary>
    /// Whether to disable type inference for column type mapping.
    /// Type inference is enabled by default.
    /// </summary>
    public bool NoInference { get; set; }
    
    /// <summary>
    /// Whether to disable data sampling queries for type verification.
    /// When enabled, type inference uses only column metadata (name, type, comment).
    /// This significantly speeds up schema extraction but may reduce accuracy.
    /// </summary>
    public bool NoChecks { get; set; }
    
    /// <summary>
    /// Whether to disable view mapping.
    /// </summary>
    public bool NoViews { get; set; }
    
    /// <summary>
    /// Whether to disable stored procedure mapping.
    /// </summary>
    public bool NoProcs { get; set; }
    
    /// <summary>
    /// Whether to disable user-defined function mapping.
    /// </summary>
    public bool NoUdfs { get; set; }
    
    /// <summary>
    /// Whether to disable relationship mapping entirely.
    /// Cannot be used together with --legacy.
    /// </summary>
    public bool NoRel { get; set; }
    
    /// <summary>
    /// Whether to enable legacy relationship inference.
    /// Infers relationships based on column/table naming patterns when no foreign keys exist.
    /// Cannot be used together with --no-rel.
    /// </summary>
    public bool Legacy { get; set; }
    
    /// <summary>
    /// Whether type inference is enabled (inverse of NoInference).
    /// </summary>
    public bool UseTypeInference => !NoInference;
    
    /// <summary>
    /// Whether data sampling queries are enabled (inverse of NoChecks).
    /// </summary>
    public bool UseDataSampling => !NoChecks;
    
    /// <summary>
    /// Whether view mapping is enabled (inverse of NoViews).
    /// </summary>
    public bool IncludeViews => !NoViews;
    
    /// <summary>
    /// Whether stored procedure mapping is enabled (inverse of NoProcs).
    /// </summary>
    public bool IncludeProcs => !NoProcs;
    
    /// <summary>
    /// Whether user-defined function mapping is enabled (inverse of NoUdfs).
    /// </summary>
    public bool IncludeUdfs => !NoUdfs;
    
    /// <summary>
    /// Whether relationship mapping is enabled (inverse of NoRel).
    /// </summary>
    public bool IncludeRelationships => !NoRel;
    
    /// <summary>
    /// Whether legacy relationship inference is enabled.
    /// </summary>
    public bool UseLegacyInference => Legacy && !NoRel;
    
    /// <summary>
    /// Whether to use connection string mode instead of CSV files.
    /// </summary>
    public bool UseConnectionString => !string.IsNullOrEmpty(ConnectionString);
}

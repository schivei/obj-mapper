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
    public bool NoPluralizer { get; set; } = false;
    
    /// <summary>
    /// Whether to use connection string mode instead of CSV files.
    /// </summary>
    public bool UseConnectionString => !string.IsNullOrEmpty(ConnectionString);
}

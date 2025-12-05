namespace ObjMapper.Models;

/// <summary>
/// Command-line options for the tool.
/// </summary>
public class CommandOptions
{
    public FileInfo SchemaFile { get; set; } = null!;
    public string MappingType { get; set; } = string.Empty;
    public string DatabaseType { get; set; } = string.Empty;
    public FileInfo? RelationshipsFile { get; set; }
    public FileInfo? IndexesFile { get; set; }
    public DirectoryInfo OutputDir { get; set; } = null!;
    public string Namespace { get; set; } = "Generated";
    public string ContextName { get; set; } = "AppDbContext";
    public string EntityMode { get; set; } = "class";
}

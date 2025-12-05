namespace ObjMapper.Models;

/// <summary>
/// Represents an index from the indexes CSV file.
/// </summary>
public class IndexInfo
{
    /// <summary>
    /// Schema of the table.
    /// </summary>
    public string Schema { get; set; } = string.Empty;
    
    /// <summary>
    /// Name of the table.
    /// </summary>
    public string Table { get; set; } = string.Empty;
    
    /// <summary>
    /// Name of the index.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Column(s) included in the index. Comma-separated for composite indexes.
    /// </summary>
    public string Key { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of index (e.g., unique, btree, hash, fulltext, etc.).
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets the full name of the table (schema.table or just table if no schema).
    /// </summary>
    public string FullTableName => string.IsNullOrEmpty(Schema) ? Table : $"{Schema}.{Table}";
    
    /// <summary>
    /// Gets the list of columns in the index (for composite indexes).
    /// </summary>
    public string[] Keys => Key.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    
    /// <summary>
    /// Gets whether this is a unique index.
    /// </summary>
    public bool IsUnique => Type.Contains("unique", StringComparison.OrdinalIgnoreCase);
}

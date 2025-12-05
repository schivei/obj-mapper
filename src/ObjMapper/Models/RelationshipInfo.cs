namespace ObjMapper.Models;

/// <summary>
/// Represents a relationship from the relationships CSV file.
/// Supports composite keys and cross-schema relationships.
/// </summary>
public class RelationshipInfo
{
    /// <summary>
    /// Name of the relationship/constraint.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Schema of the source table.
    /// </summary>
    public string SchemaFrom { get; set; } = string.Empty;
    
    /// <summary>
    /// Schema of the target table.
    /// </summary>
    public string SchemaTo { get; set; } = string.Empty;
    
    /// <summary>
    /// Name of the source table.
    /// </summary>
    public string TableFrom { get; set; } = string.Empty;
    
    /// <summary>
    /// Name of the target table.
    /// </summary>
    public string TableTo { get; set; } = string.Empty;
    
    /// <summary>
    /// Primary key column(s) in the target table. Comma-separated for composite keys.
    /// </summary>
    public string Key { get; set; } = string.Empty;
    
    /// <summary>
    /// Foreign key column(s) in the source table. Comma-separated for composite keys.
    /// </summary>
    public string Foreign { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets the full name of the source table (schema.table or just table if no schema).
    /// </summary>
    public string FullTableFrom => string.IsNullOrEmpty(SchemaFrom) ? TableFrom : $"{SchemaFrom}.{TableFrom}";
    
    /// <summary>
    /// Gets the full name of the target table (schema.table or just table if no schema).
    /// </summary>
    public string FullTableTo => string.IsNullOrEmpty(SchemaTo) ? TableTo : $"{SchemaTo}.{TableTo}";
    
    /// <summary>
    /// Gets the list of primary key columns (for composite keys).
    /// </summary>
    public string[] Keys => Key.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    
    /// <summary>
    /// Gets the list of foreign key columns (for composite keys).
    /// </summary>
    public string[] ForeignKeys => Foreign.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

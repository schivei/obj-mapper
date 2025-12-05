namespace ObjMapper.Models;

/// <summary>
/// Represents a table with all its columns and relationships.
/// </summary>
public class TableInfo
{
    public string Schema { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<ColumnInfo> Columns { get; set; } = [];
    public List<RelationshipInfo> OutgoingRelationships { get; set; } = [];
    public List<RelationshipInfo> IncomingRelationships { get; set; } = [];
}

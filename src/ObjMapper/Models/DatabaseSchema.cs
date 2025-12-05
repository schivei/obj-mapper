namespace ObjMapper.Models;

/// <summary>
/// Represents the entire database schema.
/// </summary>
public class DatabaseSchema
{
    public List<TableInfo> Tables { get; set; } = [];
    public List<RelationshipInfo> Relationships { get; set; } = [];
}

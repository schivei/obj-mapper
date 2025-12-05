namespace ObjMapper.Models;

/// <summary>
/// Represents a relationship from the relationships CSV file.
/// </summary>
public class RelationshipInfo
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Keys { get; set; } = string.Empty;
    public string ForeignKeys { get; set; } = string.Empty;
}

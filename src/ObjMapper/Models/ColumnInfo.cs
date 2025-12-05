namespace ObjMapper.Models;

/// <summary>
/// Represents a column from the schema CSV file.
/// </summary>
public class ColumnInfo
{
    public string Schema { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public string Column { get; set; } = string.Empty;
    public bool Nullable { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}

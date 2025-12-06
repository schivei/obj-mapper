namespace ObjMapper.Models;

/// <summary>
/// Represents the entire database schema.
/// </summary>
public class DatabaseSchema
{
    public List<TableInfo> Tables { get; set; } = [];
    public List<RelationshipInfo> Relationships { get; set; } = [];
    public List<IndexInfo> Indexes { get; set; } = [];
    public List<ScalarFunctionInfo> ScalarFunctions { get; set; } = [];
    public List<StoredProcedureInfo> StoredProcedures { get; set; } = [];
}

using CsvHelper.Configuration;
using ObjMapper.Models;

namespace ObjMapper.Parsers;

/// <summary>
/// CSV mapping for ColumnInfo.
/// </summary>
public sealed class ColumnInfoMap : ClassMap<ColumnInfo>
{
    public ColumnInfoMap()
    {
        Map(m => m.Schema).Name("schema");
        Map(m => m.Table).Name("table");
        Map(m => m.Column).Name("column");
        Map(m => m.Nullable).Name("nullable");
        Map(m => m.Type).Name("type");
        Map(m => m.Comment).Name("comment");
    }
}

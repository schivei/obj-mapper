using CsvHelper.Configuration;
using ObjMapper.Models;

namespace ObjMapper.Parsers;

/// <summary>
/// CSV mapping for IndexInfo.
/// </summary>
public sealed class IndexInfoMap : ClassMap<IndexInfo>
{
    public IndexInfoMap()
    {
        Map(m => m.Schema).Name("schema");
        Map(m => m.Table).Name("table");
        Map(m => m.Name).Name("name");
        Map(m => m.Key).Name("key");
        Map(m => m.Type).Name("type");
    }
}

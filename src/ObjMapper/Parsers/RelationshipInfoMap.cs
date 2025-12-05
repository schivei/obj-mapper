using CsvHelper.Configuration;
using ObjMapper.Models;

namespace ObjMapper.Parsers;

/// <summary>
/// CSV mapping for RelationshipInfo.
/// </summary>
public sealed class RelationshipInfoMap : ClassMap<RelationshipInfo>
{
    public RelationshipInfoMap()
    {
        Map(m => m.From).Name("from");
        Map(m => m.To).Name("to");
        Map(m => m.Keys).Name("keys");
        Map(m => m.ForeignKeys).Name("foreignkeys");
    }
}

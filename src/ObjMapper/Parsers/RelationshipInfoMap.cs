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
        Map(m => m.Name).Name("name");
        Map(m => m.SchemaFrom).Name("schema_from");
        Map(m => m.SchemaTo).Name("schema_to");
        Map(m => m.TableFrom).Name("table_from");
        Map(m => m.TableTo).Name("table_to");
        Map(m => m.Key).Name("key");
        Map(m => m.Foreign).Name("foreign");
    }
}

using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using ObjMapper.Models;

namespace ObjMapper.Parsers;

/// <summary>
/// Parses CSV files into schema models.
/// </summary>
public class CsvSchemaParser
{
    private static CsvConfiguration CreateCsvConfiguration() => new(CultureInfo.InvariantCulture)
    {
        HeaderValidated = null,
        MissingFieldFound = null,
        PrepareHeaderForMatch = args => args.Header.ToLowerInvariant()
    };

    /// <summary>
    /// Parses the schema CSV file and returns a list of columns.
    /// </summary>
    public List<ColumnInfo> ParseSchemaFile(string filePath)
    {
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CreateCsvConfiguration());
        
        csv.Context.RegisterClassMap<ColumnInfoMap>();
        return [.. csv.GetRecords<ColumnInfo>()];
    }

    /// <summary>
    /// Parses the relationships CSV file and returns a list of relationships.
    /// </summary>
    public List<RelationshipInfo> ParseRelationshipsFile(string filePath)
    {
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CreateCsvConfiguration());
        
        csv.Context.RegisterClassMap<RelationshipInfoMap>();
        return [.. csv.GetRecords<RelationshipInfo>()];
    }

    /// <summary>
    /// Parses the indexes CSV file and returns a list of indexes.
    /// </summary>
    public List<IndexInfo> ParseIndexesFile(string filePath)
    {
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CreateCsvConfiguration());
        
        csv.Context.RegisterClassMap<IndexInfoMap>();
        return [.. csv.GetRecords<IndexInfo>()];
    }

    /// <summary>
    /// Builds a complete database schema from columns, relationships, and indexes.
    /// </summary>
    public DatabaseSchema BuildSchema(List<ColumnInfo> columns, List<RelationshipInfo>? relationships, List<IndexInfo>? indexes)
    {
        var schema = new DatabaseSchema
        {
            Relationships = relationships ?? [],
            Indexes = indexes ?? []
        };

        // Group columns by schema and table
        var tableGroups = columns.GroupBy(c => new { c.Schema, c.Table });

        foreach (var group in tableGroups)
        {
            var table = new TableInfo
            {
                Schema = group.Key.Schema,
                Name = group.Key.Table,
                Columns = [.. group]
            };

            var fullTableName = string.IsNullOrEmpty(table.Schema) 
                ? table.Name 
                : $"{table.Schema}.{table.Name}";

            if (relationships is not null)
            {
                table.OutgoingRelationships = [.. relationships
                    .Where(r => r.FullTableFrom.Equals(fullTableName, StringComparison.OrdinalIgnoreCase) ||
                               r.TableFrom.Equals(table.Name, StringComparison.OrdinalIgnoreCase))];

                table.IncomingRelationships = [.. relationships
                    .Where(r => r.FullTableTo.Equals(fullTableName, StringComparison.OrdinalIgnoreCase) ||
                               r.TableTo.Equals(table.Name, StringComparison.OrdinalIgnoreCase))];
            }

            if (indexes is not null)
            {
                table.Indexes = [.. indexes
                    .Where(i => i.FullTableName.Equals(fullTableName, StringComparison.OrdinalIgnoreCase) ||
                               i.Table.Equals(table.Name, StringComparison.OrdinalIgnoreCase))];
            }

            schema.Tables.Add(table);
        }

        return schema;
    }
}

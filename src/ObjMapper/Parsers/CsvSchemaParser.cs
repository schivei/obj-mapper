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
    /// <summary>
    /// Parses the schema CSV file and returns a list of columns.
    /// </summary>
    public List<ColumnInfo> ParseSchemaFile(string filePath)
    {
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
            PrepareHeaderForMatch = args => args.Header.ToLowerInvariant()
        });
        
        csv.Context.RegisterClassMap<ColumnInfoMap>();
        return csv.GetRecords<ColumnInfo>().ToList();
    }

    /// <summary>
    /// Parses the relationships CSV file and returns a list of relationships.
    /// </summary>
    public List<RelationshipInfo> ParseRelationshipsFile(string filePath)
    {
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
            PrepareHeaderForMatch = args => args.Header.ToLowerInvariant()
        });
        
        csv.Context.RegisterClassMap<RelationshipInfoMap>();
        return csv.GetRecords<RelationshipInfo>().ToList();
    }

    /// <summary>
    /// Builds a complete database schema from columns and relationships.
    /// </summary>
    public DatabaseSchema BuildSchema(List<ColumnInfo> columns, List<RelationshipInfo>? relationships)
    {
        var schema = new DatabaseSchema
        {
            Relationships = relationships ?? []
        };

        // Group columns by schema and table
        var tableGroups = columns.GroupBy(c => new { c.Schema, c.Table });

        foreach (var group in tableGroups)
        {
            var table = new TableInfo
            {
                Schema = group.Key.Schema,
                Name = group.Key.Table,
                Columns = group.ToList()
            };

            if (relationships != null)
            {
                var fullTableName = string.IsNullOrEmpty(table.Schema) 
                    ? table.Name 
                    : $"{table.Schema}.{table.Name}";

                table.OutgoingRelationships = relationships
                    .Where(r => r.From.Equals(fullTableName, StringComparison.OrdinalIgnoreCase) ||
                               r.From.Equals(table.Name, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                table.IncomingRelationships = relationships
                    .Where(r => r.To.Equals(fullTableName, StringComparison.OrdinalIgnoreCase) ||
                               r.To.Equals(table.Name, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            schema.Tables.Add(table);
        }

        return schema;
    }
}

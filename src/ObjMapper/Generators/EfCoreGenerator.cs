using System.Text;
using ObjMapper.Models;
using ObjMapper.Services;

namespace ObjMapper.Generators;

/// <summary>
/// Generates EF Core entities, configurations, and DbContext.
/// </summary>
public class EfCoreGenerator : ICodeGenerator
{
    private readonly TypeMapper _typeMapper;
    private readonly string _namespace;

    public EntityTypeMode EntityTypeMode { get; set; } = EntityTypeMode.Class;

    public EfCoreGenerator(DatabaseType databaseType, string namespaceName = "Generated")
    {
        _typeMapper = new TypeMapper(databaseType);
        _namespace = namespaceName;
    }

    public Dictionary<string, string> GenerateEntities(DatabaseSchema schema)
    {
        var entities = new Dictionary<string, string>();
        var filteredTables = FilterDuplicateTables(schema.Tables);

        // Track used entity names to handle conflicts
        var usedEntityNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in filteredTables)
        {
            var entityName = GetUniqueEntityName(NamingHelper.ToEntityName(table.Name), usedEntityNames);
            usedEntityNames.Add(entityName);
            
            var code = GenerateEntityClass(table, schema, entityName);
            entities[$"{entityName}.cs"] = code;
        }

        return entities;
    }

    public Dictionary<string, string> GenerateConfigurations(DatabaseSchema schema)
    {
        var configurations = new Dictionary<string, string>();
        var filteredTables = FilterDuplicateTables(schema.Tables);

        // Track used entity names to handle conflicts
        var usedEntityNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in filteredTables)
        {
            var entityName = GetUniqueEntityName(NamingHelper.ToEntityName(table.Name), usedEntityNames);
            usedEntityNames.Add(entityName);
            
            var code = GenerateConfigurationClass(table, schema, entityName);
            configurations[$"{entityName}Configuration.cs"] = code;
        }

        return configurations;
    }

    public string GenerateDbContext(DatabaseSchema schema, string contextName)
    {
        var sb = new StringBuilder();
        var filteredTables = FilterDuplicateTables(schema.Tables);
        
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_namespace};");
        sb.AppendLine();
        sb.AppendLine($"public partial class {contextName} : DbContext");
        sb.AppendLine("{");
        sb.AppendLine($"    public {contextName}(DbContextOptions<{contextName}> options) : base(options)");
        sb.AppendLine("    {");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Track used names to avoid duplicates
        var usedEntityNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedCollectionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tableEntityMap = new List<(TableInfo table, string entityName, string collectionName)>();

        // First pass: determine all entity and collection names
        foreach (var table in filteredTables)
        {
            var entityName = GetUniqueEntityName(NamingHelper.ToEntityName(table.Name), usedEntityNames);
            usedEntityNames.Add(entityName);
            
            var collectionName = GetUniqueCollectionName(NamingHelper.ToCollectionName(entityName), usedCollectionNames);
            usedCollectionNames.Add(collectionName);
            
            tableEntityMap.Add((table, entityName, collectionName));
        }

        // Generate DbSet properties
        foreach (var (table, entityName, collectionName) in tableEntityMap)
        {
            sb.AppendLine($"    public DbSet<{entityName}> {collectionName} {{ get; set; }} = null!;");
        }

        sb.AppendLine();
        sb.AppendLine("    protected override void OnModelCreating(ModelBuilder modelBuilder)");
        sb.AppendLine("    {");
        sb.AppendLine("        base.OnModelCreating(modelBuilder);");
        sb.AppendLine();

        // Apply configurations
        foreach (var (table, entityName, collectionName) in tableEntityMap)
        {
            sb.AppendLine($"        modelBuilder.ApplyConfiguration(new {entityName}Configuration());");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Filters tables to remove duplicates where both singular and plural versions exist.
    /// When duplicates are found, the pluralized version is kept.
    /// </summary>
    /// <param name="tables">List of tables to filter.</param>
    /// <returns>Filtered list of tables.</returns>
    private static List<TableInfo> FilterDuplicateTables(List<TableInfo> tables)
    {
        // Group tables by their entity name (which singularizes the table name)
        var entityGroups = tables
            .GroupBy(t => NamingHelper.ToEntityName(t.Name), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<TableInfo>();

        foreach (var group in entityGroups)
        {
            if (group.Count() == 1)
            {
                // No duplicate, just add the table
                result.Add(group.First());
            }
            else
            {
                // Multiple tables map to the same entity name
                // Prefer the pluralized version (the one where table name != entity name)
                var pluralizedTable = group.FirstOrDefault(t => 
                    !t.Name.Equals(NamingHelper.ToEntityName(t.Name), StringComparison.OrdinalIgnoreCase));
                
                if (pluralizedTable != null)
                {
                    result.Add(pluralizedTable);
                }
                else
                {
                    // If no pluralized version found, just take the first one
                    result.Add(group.First());
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Gets a unique entity name that doesn't conflict with existing entity names.
    /// </summary>
    private static string GetUniqueEntityName(string proposedName, HashSet<string> existingNames)
    {
        if (!existingNames.Contains(proposedName))
        {
            return proposedName;
        }

        var baseName = proposedName;
        var counter = 1;
        while (existingNames.Contains(proposedName))
        {
            proposedName = $"{baseName}{counter}";
            counter++;
        }

        return proposedName;
    }

    /// <summary>
    /// Gets a unique collection name that doesn't conflict with existing collection names.
    /// </summary>
    private static string GetUniqueCollectionName(string proposedName, HashSet<string> existingNames)
    {
        if (!existingNames.Contains(proposedName))
        {
            return proposedName;
        }

        var baseName = proposedName;
        var counter = 1;
        while (existingNames.Contains(proposedName))
        {
            proposedName = $"{baseName}{counter}";
            counter++;
        }

        return proposedName;
    }

    private string GenerateEntityClass(TableInfo table, DatabaseSchema schema, string entityName)
    {
        var sb = new StringBuilder();

        // Collect all property names first to detect conflicts
        var propertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { entityName };

        sb.AppendLine($"namespace {_namespace};");
        sb.AppendLine();
        
        // Add XML comment if any column has a comment
        if (table.Columns.Exists(c => !string.IsNullOrEmpty(c.Comment)))
        {
            sb.AppendLine("/// <summary>");
            sb.AppendLine($"/// Entity for table {table.Name}.");
            sb.AppendLine("/// </summary>");
        }

        // Generate the type declaration based on EntityTypeMode
        var typeKeyword = GetTypeKeyword();
        sb.AppendLine($"public partial {typeKeyword} {entityName}");
        sb.AppendLine("{");

        // Generate properties for columns
        foreach (var column in table.Columns)
        {
            var propertyName = GetUniquePropertyName(NamingHelper.ToPascalCase(column.Column), entityName, propertyNames);
            propertyNames.Add(propertyName);
            
            var csharpType = _typeMapper.MapToCSharpType(column.Type, column.Nullable);

            // Add comment if present
            if (!string.IsNullOrEmpty(column.Comment))
            {
                AppendXmlComment(sb, column.Comment, "    ");
            }

            if (csharpType == "string" && !column.Nullable)
            {
                sb.AppendLine($"    public {csharpType} {propertyName} {{ get; set; }} = string.Empty;");
            }
            else if (csharpType == "byte[]" && !column.Nullable)
            {
                sb.AppendLine($"    public {csharpType} {propertyName} {{ get; set; }} = [];");
            }
            else
            {
                sb.AppendLine($"    public {csharpType} {propertyName} {{ get; set; }}");
            }
        }

        // Generate navigation properties for outgoing relationships (foreign keys)
        foreach (var rel in table.OutgoingRelationships)
        {
            var relatedEntityName = NamingHelper.ToEntityName(rel.TableTo);
            var propertyName = GetUniquePropertyName(relatedEntityName, entityName, propertyNames);
            propertyNames.Add(propertyName);
            
            sb.AppendLine();
            sb.AppendLine($"    public virtual {relatedEntityName}? {propertyName} {{ get; set; }}");
        }

        // Generate navigation properties for incoming relationships (inverse navigation)
        foreach (var rel in table.IncomingRelationships)
        {
            var relatedEntityName = NamingHelper.ToEntityName(rel.TableFrom);
            var collectionName = GetUniquePropertyName(NamingHelper.ToCollectionName(relatedEntityName), entityName, propertyNames);
            propertyNames.Add(collectionName);
            
            sb.AppendLine();
            sb.AppendLine($"    public virtual ICollection<{relatedEntityName}> {collectionName} {{ get; set; }} = [];");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GenerateConfigurationClass(TableInfo table, DatabaseSchema schema, string entityName)
    {
        var sb = new StringBuilder();
        
        // Track property names for this entity to handle conflicts
        var propertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { entityName };

        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine("using Microsoft.EntityFrameworkCore.Metadata.Builders;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_namespace};");
        sb.AppendLine();
        sb.AppendLine($"public partial class {entityName}Configuration : IEntityTypeConfiguration<{entityName}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public void Configure(EntityTypeBuilder<{entityName}> builder)");
        sb.AppendLine("    {");

        // Table mapping
        if (!string.IsNullOrEmpty(table.Schema))
        {
            sb.AppendLine($"        builder.ToTable(\"{table.Name}\", \"{table.Schema}\");");
        }
        else
        {
            sb.AppendLine($"        builder.ToTable(\"{table.Name}\");");
        }

        sb.AppendLine();

        // Detect and configure primary key
        var potentialPrimaryKeys = table.Columns
            .Where(c => c.Column.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                       c.Column.EndsWith("_id", StringComparison.OrdinalIgnoreCase) && 
                       c.Column.StartsWith(table.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (potentialPrimaryKeys.Count > 0)
        {
            var pkColumn = potentialPrimaryKeys[0];
            var pkPropertyName = GetUniquePropertyName(NamingHelper.ToPascalCase(pkColumn.Column), entityName, propertyNames);
            sb.AppendLine($"        builder.HasKey(e => e.{pkPropertyName});");
            sb.AppendLine();
        }

        // Configure each column
        foreach (var column in table.Columns)
        {
            var propertyName = GetUniquePropertyName(NamingHelper.ToPascalCase(column.Column), entityName, propertyNames);
            propertyNames.Add(propertyName);
            
            sb.Append($"        builder.Property(e => e.{propertyName})");
            sb.AppendLine();
            sb.AppendLine($"            .HasColumnName(\"{column.Column}\")");

            if (!column.Nullable)
            {
                sb.AppendLine("            .IsRequired()");
            }

            // Add column type for specific database types
            if (NeedsColumnType(column.Type))
            {
                sb.AppendLine($"            .HasColumnType(\"{column.Type}\")");
            }

            if (!string.IsNullOrEmpty(column.Comment))
            {
                sb.AppendLine($"            .HasComment(\"{EscapeString(column.Comment)}\")");
            }

            // Remove the last newline and add semicolon
            sb.Length -= Environment.NewLine.Length;
            sb.AppendLine(";");
            sb.AppendLine();
        }

        // Configure indexes
        foreach (var index in table.Indexes)
        {
            var keyProperties = index.Keys.Select(k => 
            {
                var propName = GetUniquePropertyName(NamingHelper.ToPascalCase(k), entityName, propertyNames);
                return $"e.{propName}";
            });
            var keysExpression = index.Keys.Length == 1 
                ? keyProperties.First() 
                : $"new {{ {string.Join(", ", keyProperties)} }}";
            
            sb.Append($"        builder.HasIndex(e => {keysExpression})");
            
            if (!string.IsNullOrEmpty(index.Name))
            {
                sb.AppendLine();
                sb.Append($"            .HasDatabaseName(\"{index.Name}\")");
            }
            
            if (index.IsUnique)
            {
                sb.AppendLine();
                sb.Append("            .IsUnique()");
            }
            
            sb.AppendLine(";");
            sb.AppendLine();
        }

        // Configure relationships
        foreach (var rel in table.OutgoingRelationships)
        {
            var relatedEntityName = NamingHelper.ToEntityName(rel.TableTo);
            var navPropertyName = GetUniquePropertyName(relatedEntityName, entityName, propertyNames);
            var inverseCollectionName = NamingHelper.ToCollectionName(entityName);

            // Handle composite keys
            if (rel.ForeignKeys.Length == 1)
            {
                var foreignKeyProperty = GetUniquePropertyName(NamingHelper.ToPascalCase(rel.ForeignKeys[0]), entityName, propertyNames);
                var principalKeyProperty = NamingHelper.ToPascalCase(rel.Keys[0]);

                sb.AppendLine($"        builder.HasOne(e => e.{navPropertyName})");
                sb.AppendLine($"            .WithMany(e => e.{inverseCollectionName})");
                sb.AppendLine($"            .HasForeignKey(e => e.{foreignKeyProperty})");
                sb.AppendLine($"            .HasPrincipalKey(e => e.{principalKeyProperty});");
            }
            else
            {
                var foreignKeyProperties = rel.ForeignKeys.Select(k => 
                {
                    var propName = GetUniquePropertyName(NamingHelper.ToPascalCase(k), entityName, propertyNames);
                    return $"e.{propName}";
                });
                var principalKeyProperties = rel.Keys.Select(k => $"e.{NamingHelper.ToPascalCase(k)}");

                sb.AppendLine($"        builder.HasOne(e => e.{navPropertyName})");
                sb.AppendLine($"            .WithMany(e => e.{inverseCollectionName})");
                sb.AppendLine($"            .HasForeignKey(e => new {{ {string.Join(", ", foreignKeyProperties)} }})");
                sb.AppendLine($"            .HasPrincipalKey(e => new {{ {string.Join(", ", principalKeyProperties)} }});");
            }
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GetTypeKeyword()
    {
        return EntityTypeMode switch
        {
            EntityTypeMode.Class => "class",
            EntityTypeMode.Record => "record",
            EntityTypeMode.Struct => "struct",
            EntityTypeMode.RecordStruct => "record struct",
            _ => "class"
        };
    }

    /// <summary>
    /// Gets a unique property name that doesn't conflict with the entity name or other properties.
    /// </summary>
    /// <param name="proposedName">The proposed property name.</param>
    /// <param name="entityName">The name of the containing entity.</param>
    /// <param name="existingNames">Set of existing property names (including entity name).</param>
    /// <returns>A unique property name.</returns>
    private static string GetUniquePropertyName(string proposedName, string entityName, HashSet<string> existingNames)
    {
        // If the property name equals the entity name, add a suffix
        if (proposedName.Equals(entityName, StringComparison.OrdinalIgnoreCase))
        {
            proposedName = $"{proposedName}Value";
        }
        
        // If there's still a conflict with existing names, add numeric suffix
        var baseName = proposedName;
        var counter = 1;
        while (existingNames.Contains(proposedName))
        {
            proposedName = $"{baseName}{counter}";
            counter++;
        }
        
        return proposedName;
    }

    private static bool NeedsColumnType(string dbType)
    {
        var type = dbType.ToUpperInvariant();
        return type.Contains('(') || 
               type.Contains("DECIMAL") || 
               type.Contains("NUMERIC") ||
               type.Contains("VARCHAR") ||
               type.Contains("NVARCHAR");
    }

    private static string EscapeString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r\n", "\\n")
            .Replace("\r", "\\n")
            .Replace("\n", "\\n");
    }

    /// <summary>
    /// Appends a properly formatted XML documentation comment to the StringBuilder.
    /// Handles multi-line comments by prefixing each line with the XML comment syntax.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="comment">The comment text (may contain newlines).</param>
    /// <param name="indent">The indentation to use (e.g., "    " for 4 spaces).</param>
    private static void AppendXmlComment(StringBuilder sb, string comment, string indent)
    {
        // Normalize line endings and split into lines
        var lines = comment
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Split('\n')
            .Select(line => EscapeXmlComment(line.Trim()))
            .Where(line => !string.IsNullOrEmpty(line))
            .ToArray();

        sb.AppendLine($"{indent}/// <summary>");
        
        foreach (var line in lines)
        {
            sb.AppendLine($"{indent}/// {line}");
        }
        
        sb.AppendLine($"{indent}/// </summary>");
    }

    /// <summary>
    /// Escapes special characters in XML comments.
    /// </summary>
    private static string EscapeXmlComment(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}

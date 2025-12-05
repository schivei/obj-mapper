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

        foreach (var table in schema.Tables)
        {
            var entityName = NamingHelper.ToEntityName(table.Name);
            var code = GenerateEntityClass(table, schema);
            entities[$"{entityName}.cs"] = code;
        }

        return entities;
    }

    public Dictionary<string, string> GenerateConfigurations(DatabaseSchema schema)
    {
        var configurations = new Dictionary<string, string>();

        foreach (var table in schema.Tables)
        {
            var entityName = NamingHelper.ToEntityName(table.Name);
            var code = GenerateConfigurationClass(table, schema);
            configurations[$"{entityName}Configuration.cs"] = code;
        }

        return configurations;
    }

    public string GenerateDbContext(DatabaseSchema schema, string contextName)
    {
        var sb = new StringBuilder();
        
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

        // Generate DbSet properties
        foreach (var table in schema.Tables)
        {
            var entityName = NamingHelper.ToEntityName(table.Name);
            var collectionName = NamingHelper.ToCollectionName(entityName);
            sb.AppendLine($"    public DbSet<{entityName}> {collectionName} {{ get; set; }} = null!;");
        }

        sb.AppendLine();
        sb.AppendLine("    protected override void OnModelCreating(ModelBuilder modelBuilder)");
        sb.AppendLine("    {");
        sb.AppendLine("        base.OnModelCreating(modelBuilder);");
        sb.AppendLine();

        // Apply configurations
        foreach (var table in schema.Tables)
        {
            var entityName = NamingHelper.ToEntityName(table.Name);
            sb.AppendLine($"        modelBuilder.ApplyConfiguration(new {entityName}Configuration());");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GenerateEntityClass(TableInfo table, DatabaseSchema schema)
    {
        var entityName = NamingHelper.ToEntityName(table.Name);
        var sb = new StringBuilder();

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
            var propertyName = NamingHelper.ToPascalCase(column.Column);
            var csharpType = _typeMapper.MapToCSharpType(column.Type, column.Nullable);

            // Add comment if present
            if (!string.IsNullOrEmpty(column.Comment))
            {
                sb.AppendLine("    /// <summary>");
                sb.AppendLine($"    /// {column.Comment}");
                sb.AppendLine("    /// </summary>");
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
            var propertyName = relatedEntityName;
            
            sb.AppendLine();
            sb.AppendLine($"    public virtual {relatedEntityName}? {propertyName} {{ get; set; }}");
        }

        // Generate navigation properties for incoming relationships (inverse navigation)
        foreach (var rel in table.IncomingRelationships)
        {
            var relatedEntityName = NamingHelper.ToEntityName(rel.TableFrom);
            var collectionName = NamingHelper.ToCollectionName(relatedEntityName);
            
            sb.AppendLine();
            sb.AppendLine($"    public virtual ICollection<{relatedEntityName}> {collectionName} {{ get; set; }} = [];");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GenerateConfigurationClass(TableInfo table, DatabaseSchema schema)
    {
        var entityName = NamingHelper.ToEntityName(table.Name);
        var sb = new StringBuilder();

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
            var pkPropertyName = NamingHelper.ToPascalCase(pkColumn.Column);
            sb.AppendLine($"        builder.HasKey(e => e.{pkPropertyName});");
            sb.AppendLine();
        }

        // Configure each column
        foreach (var column in table.Columns)
        {
            var propertyName = NamingHelper.ToPascalCase(column.Column);
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
            var keyProperties = index.Keys.Select(k => $"e.{NamingHelper.ToPascalCase(k)}");
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
            var inverseCollectionName = NamingHelper.ToCollectionName(entityName);

            // Handle composite keys
            if (rel.ForeignKeys.Length == 1)
            {
                var foreignKeyProperty = NamingHelper.ToPascalCase(rel.ForeignKeys[0]);
                var principalKeyProperty = NamingHelper.ToPascalCase(rel.Keys[0]);

                sb.AppendLine($"        builder.HasOne(e => e.{relatedEntityName})");
                sb.AppendLine($"            .WithMany(e => e.{inverseCollectionName})");
                sb.AppendLine($"            .HasForeignKey(e => e.{foreignKeyProperty})");
                sb.AppendLine($"            .HasPrincipalKey(e => e.{principalKeyProperty});");
            }
            else
            {
                var foreignKeyProperties = rel.ForeignKeys.Select(k => $"e.{NamingHelper.ToPascalCase(k)}");
                var principalKeyProperties = rel.Keys.Select(k => $"e.{NamingHelper.ToPascalCase(k)}");

                sb.AppendLine($"        builder.HasOne(e => e.{relatedEntityName})");
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
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}

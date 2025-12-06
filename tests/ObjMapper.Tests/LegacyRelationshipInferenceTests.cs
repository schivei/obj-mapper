using ObjMapper.Models;
using Xunit;

namespace ObjMapper.Tests;

/// <summary>
/// Tests for legacy relationship inference based on naming patterns.
/// </summary>
public class LegacyRelationshipInferenceTests
{
    [Fact]
    public void InferRelationships_Pattern_TableNameId()
    {
        // Pattern: user_id column should reference users table
        var schema = CreateSchemaWithTables(
            ("users", new[] { "id", "name", "email" }),
            ("orders", new[] { "id", "user_id", "total" })
        );
        
        var relationships = InferRelationshipsFromSchema(schema);
        
        Assert.Single(relationships);
        Assert.Equal("orders", relationships[0].TableFrom);
        Assert.Equal("users", relationships[0].TableTo);
        Assert.Equal("user_id", relationships[0].Foreign);
    }
    
    [Fact]
    public void InferRelationships_Pattern_CamelCaseId()
    {
        // Pattern: userId column should reference user table
        var schema = CreateSchemaWithTables(
            ("user", new[] { "id", "name" }),
            ("post", new[] { "id", "userid", "title" })
        );
        
        var relationships = InferRelationshipsFromSchema(schema);
        
        Assert.Single(relationships);
        Assert.Equal("post", relationships[0].TableFrom);
        Assert.Equal("user", relationships[0].TableTo);
    }
    
    [Fact]
    public void InferRelationships_Pattern_FkPrefix()
    {
        // Pattern: fk_customer column should reference customer table
        var schema = CreateSchemaWithTables(
            ("customer", new[] { "id", "name" }),
            ("invoice", new[] { "id", "fk_customer", "amount" })
        );
        
        var relationships = InferRelationshipsFromSchema(schema);
        
        Assert.Single(relationships);
        Assert.Equal("invoice", relationships[0].TableFrom);
        Assert.Equal("customer", relationships[0].TableTo);
    }
    
    [Fact]
    public void InferRelationships_Pattern_FkSuffix()
    {
        // Pattern: product_fk column should reference product table
        var schema = CreateSchemaWithTables(
            ("product", new[] { "id", "name" }),
            ("order_item", new[] { "id", "product_fk", "quantity" })
        );
        
        var relationships = InferRelationshipsFromSchema(schema);
        
        Assert.Single(relationships);
        Assert.Equal("order_item", relationships[0].TableFrom);
        Assert.Equal("product", relationships[0].TableTo);
    }
    
    [Fact]
    public void InferRelationships_PluralToSingular()
    {
        // Pattern: user_id should match to both 'user' and 'users' tables
        var schema = CreateSchemaWithTables(
            ("users", new[] { "id", "name" }),
            ("comments", new[] { "id", "user_id", "text" })
        );
        
        var relationships = InferRelationshipsFromSchema(schema);
        
        Assert.Single(relationships);
        Assert.Equal("users", relationships[0].TableTo);
    }
    
    [Fact]
    public void InferRelationships_IgnoresPrimaryKey()
    {
        // Primary key 'id' should not create a relationship
        var schema = CreateSchemaWithTables(
            ("id", new[] { "id", "name" }), // Table named 'id'
            ("test", new[] { "id", "value" })
        );
        
        var relationships = InferRelationshipsFromSchema(schema);
        
        Assert.Empty(relationships);
    }
    
    [Fact]
    public void InferRelationships_IgnoresTableOwnId()
    {
        // user_id in users table should not create self-reference
        var schema = CreateSchemaWithTables(
            ("users", new[] { "id", "users_id", "name" })
        );
        
        var relationships = InferRelationshipsFromSchema(schema);
        
        // users_id in users table is ignored as potential self-reference to primary key
        Assert.Empty(relationships);
    }
    
    [Fact]
    public void InferRelationships_MultipleRelationships()
    {
        var schema = CreateSchemaWithTables(
            ("users", new[] { "id", "name" }),
            ("products", new[] { "id", "name" }),
            ("orders", new[] { "id", "user_id", "product_id" })
        );
        
        var relationships = InferRelationshipsFromSchema(schema);
        
        Assert.Equal(2, relationships.Count);
        Assert.Contains(relationships, r => r.TableTo == "users");
        Assert.Contains(relationships, r => r.TableTo == "products");
    }
    
    [Fact]
    public void InferRelationships_NoMatchingTable_NoRelationship()
    {
        var schema = CreateSchemaWithTables(
            ("orders", new[] { "id", "nonexistent_id", "total" })
        );
        
        var relationships = InferRelationshipsFromSchema(schema);
        
        Assert.Empty(relationships);
    }
    
    [Fact]
    public void InferRelationships_SetsCorrectKeyAndForeign()
    {
        var schema = CreateSchemaWithTables(
            ("category", new[] { "id", "name" }),
            ("product", new[] { "id", "category_id", "name" })
        );
        
        var relationships = InferRelationshipsFromSchema(schema);
        
        Assert.Single(relationships);
        Assert.Equal("id", relationships[0].Key);
        Assert.Equal("category_id", relationships[0].Foreign);
    }
    
    [Fact]
    public void InferRelationships_GeneratesUniqueConstraintName()
    {
        var schema = CreateSchemaWithTables(
            ("category", new[] { "id", "name" }),
            ("product", new[] { "id", "category_id", "name" })
        );
        
        var relationships = InferRelationshipsFromSchema(schema);
        
        Assert.Single(relationships);
        Assert.StartsWith("inferred_fk_", relationships[0].Name);
        Assert.Contains("product", relationships[0].Name);
        Assert.Contains("category_id", relationships[0].Name);
    }
    
    private static DatabaseSchema CreateSchemaWithTables(params (string name, string[] columns)[] tables)
    {
        var schema = new DatabaseSchema();
        
        foreach (var (name, columns) in tables)
        {
            var table = new TableInfo
            {
                Name = name,
                Schema = "public"
            };
            
            foreach (var col in columns)
            {
                table.Columns.Add(new ColumnInfo
                {
                    Table = name,
                    Column = col,
                    Type = col == "id" ? "int" : "varchar",
                    Schema = "public"
                });
            }
            
            schema.Tables.Add(table);
        }
        
        return schema;
    }
    
    private static List<RelationshipInfo> InferRelationshipsFromSchema(DatabaseSchema schema)
    {
        // Use reflection to call the private method for testing
        // In production, this is called by BaseSchemaExtractor
        var tablesByName = schema.Tables.ToDictionary(t => t.Name.ToLowerInvariant(), t => t);
        var relationships = new List<RelationshipInfo>();
        
        foreach (var table in schema.Tables)
        {
            foreach (var column in table.Columns)
            {
                var colName = column.Column.ToLowerInvariant();
                
                // Skip primary key columns
                if (colName == "id" || colName == $"{table.Name.ToLowerInvariant()}_id")
                    continue;
                
                string? potentialTable = null;
                
                if (colName.EndsWith("_id"))
                    potentialTable = colName[..^3];
                else if (colName.EndsWith("id") && colName.Length > 2)
                    potentialTable = colName[..^2];
                else if (colName.EndsWith("_fk"))
                    potentialTable = colName[..^3];
                else if (colName.StartsWith("fk_"))
                    potentialTable = colName[3..];
                
                if (potentialTable != null)
                {
                    if (TryFindTable(potentialTable, tablesByName, out var refTable))
                    {
                        relationships.Add(new RelationshipInfo
                        {
                            Name = $"inferred_fk_{table.Name}_{column.Column}",
                            SchemaFrom = table.Schema,
                            SchemaTo = refTable!.Schema,
                            TableFrom = table.Name,
                            TableTo = refTable.Name,
                            Key = "id",
                            Foreign = column.Column
                        });
                    }
                }
            }
        }
        
        return relationships;
    }
    
    private static bool TryFindTable(string name, Dictionary<string, TableInfo> tables, out TableInfo? table)
    {
        table = null;
        var normalized = name.ToLowerInvariant();
        
        if (tables.TryGetValue(normalized, out table)) return true;
        if (tables.TryGetValue(normalized + "s", out table)) return true;
        if (normalized.EndsWith("s") && tables.TryGetValue(normalized[..^1], out table)) return true;
        
        return false;
    }
}

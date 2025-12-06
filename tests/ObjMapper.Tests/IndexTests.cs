using ObjMapper.Generators;
using ObjMapper.Models;

namespace ObjMapper.Tests;

/// <summary>
/// Tests for index handling in code generators.
/// </summary>
public class IndexTests
{
    private static DatabaseSchema CreateSchemaWithIndexes()
    {
        var usersTable = new TableInfo
        {
            Schema = "public",
            Name = "users",
            Columns =
            [
                new ColumnInfo { Schema = "public", Table = "users", Column = "id", Type = "integer", Nullable = false },
                new ColumnInfo { Schema = "public", Table = "users", Column = "email", Type = "varchar(255)", Nullable = false },
                new ColumnInfo { Schema = "public", Table = "users", Column = "created_at", Type = "timestamp", Nullable = false }
            ],
            Indexes =
            [
                new IndexInfo { Schema = "public", Table = "users", Name = "idx_users_email", Key = "email", Type = "unique" },
                new IndexInfo { Schema = "public", Table = "users", Name = "idx_users_created_at", Key = "created_at", Type = "btree" }
            ]
        };

        var ordersTable = new TableInfo
        {
            Schema = "public",
            Name = "orders",
            Columns =
            [
                new ColumnInfo { Schema = "public", Table = "orders", Column = "id", Type = "integer", Nullable = false },
                new ColumnInfo { Schema = "public", Table = "orders", Column = "user_id", Type = "integer", Nullable = false },
                new ColumnInfo { Schema = "public", Table = "orders", Column = "status", Type = "varchar(50)", Nullable = false }
            ],
            Indexes =
            [
                new IndexInfo { Schema = "public", Table = "orders", Name = "idx_orders_composite", Key = "user_id,status", Type = "btree" }
            ]
        };

        return new DatabaseSchema { Tables = [usersTable, ordersTable] };
    }

    [Fact]
    public void EfCoreGenerator_GeneratesUniqueIndexConfiguration()
    {
        // Arrange
        var schema = CreateSchemaWithIndexes();
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        
        // Act
        var configurations = generator.GenerateConfigurations(schema);
        var userConfig = configurations["UserConfiguration.cs"];
        
        // Assert
        Assert.Contains("HasIndex", userConfig);
        Assert.Contains("IsUnique()", userConfig);
        Assert.Contains("idx_users_email", userConfig);
    }
    
    [Fact]
    public void EfCoreGenerator_GeneratesNonUniqueIndexConfiguration()
    {
        // Arrange
        var schema = CreateSchemaWithIndexes();
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        
        // Act
        var configurations = generator.GenerateConfigurations(schema);
        var userConfig = configurations["UserConfiguration.cs"];
        
        // Assert
        Assert.Contains("idx_users_created_at", userConfig);
    }
    
    [Fact]
    public void EfCoreGenerator_GeneratesCompositeIndexConfiguration()
    {
        // Arrange
        var schema = CreateSchemaWithIndexes();
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        
        // Act
        var configurations = generator.GenerateConfigurations(schema);
        var orderConfig = configurations["OrderConfiguration.cs"];
        
        // Assert
        Assert.Contains("idx_orders_composite", orderConfig);
        Assert.Contains("new {", orderConfig); // Composite key uses anonymous type
    }
    
    [Fact]
    public void IndexInfo_Keys_ParsesCompositeKeys()
    {
        // Arrange
        var index = new IndexInfo { Key = "column1, column2, column3" };
        
        // Act
        var keys = index.Keys;
        
        // Assert
        Assert.Equal(3, keys.Length);
        Assert.Equal("column1", keys[0]);
        Assert.Equal("column2", keys[1]);
        Assert.Equal("column3", keys[2]);
    }
    
    [Fact]
    public void IndexInfo_IsUnique_ReturnsTrue_ForUniqueIndex()
    {
        // Arrange
        var index = new IndexInfo { Type = "unique" };
        
        // Act & Assert
        Assert.True(index.IsUnique);
    }
    
    [Fact]
    public void IndexInfo_IsUnique_ReturnsFalse_ForBtreeIndex()
    {
        // Arrange
        var index = new IndexInfo { Type = "btree" };
        
        // Act & Assert
        Assert.False(index.IsUnique);
    }
    
    [Fact]
    public void IndexInfo_FullTableName_ReturnsSchemaAndTable()
    {
        // Arrange
        var index = new IndexInfo { Schema = "public", Table = "users" };
        
        // Act & Assert
        Assert.Equal("public.users", index.FullTableName);
    }
    
    [Fact]
    public void IndexInfo_FullTableName_ReturnsOnlyTable_WhenNoSchema()
    {
        // Arrange
        var index = new IndexInfo { Schema = "", Table = "users" };
        
        // Act & Assert
        Assert.Equal("users", index.FullTableName);
    }
}

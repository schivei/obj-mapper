using ObjMapper.Generators;
using ObjMapper.Models;

namespace ObjMapper.Tests;

/// <summary>
/// Tests for relationship population in schema extractors and code generators.
/// </summary>
public class RelationshipTests
{
    /// <summary>
    /// Creates a sample database schema with relationships for testing.
    /// </summary>
    private static DatabaseSchema CreateSampleSchemaWithRelationships()
    {
        var schema = new DatabaseSchema();
        
        // Create tables
        var usersTable = new TableInfo
        {
            Schema = "public",
            Name = "users",
            Columns = new List<ColumnInfo>
            {
                new() { Schema = "public", Table = "users", Column = "id", Type = "integer", Nullable = false },
                new() { Schema = "public", Table = "users", Column = "name", Type = "varchar(100)", Nullable = false },
                new() { Schema = "public", Table = "users", Column = "email", Type = "varchar(255)", Nullable = true }
            }
        };
        
        var ordersTable = new TableInfo
        {
            Schema = "public",
            Name = "orders",
            Columns = new List<ColumnInfo>
            {
                new() { Schema = "public", Table = "orders", Column = "id", Type = "integer", Nullable = false },
                new() { Schema = "public", Table = "orders", Column = "user_id", Type = "integer", Nullable = false },
                new() { Schema = "public", Table = "orders", Column = "total", Type = "decimal(10,2)", Nullable = false }
            }
        };
        
        var orderItemsTable = new TableInfo
        {
            Schema = "public",
            Name = "order_items",
            Columns = new List<ColumnInfo>
            {
                new() { Schema = "public", Table = "order_items", Column = "id", Type = "integer", Nullable = false },
                new() { Schema = "public", Table = "order_items", Column = "order_id", Type = "integer", Nullable = false },
                new() { Schema = "public", Table = "order_items", Column = "product_name", Type = "varchar(255)", Nullable = false },
                new() { Schema = "public", Table = "order_items", Column = "quantity", Type = "integer", Nullable = false }
            }
        };
        
        // Create relationships
        var ordersToUsersRelationship = new RelationshipInfo
        {
            Name = "fk_orders_users",
            SchemaFrom = "public",
            SchemaTo = "public",
            TableFrom = "orders",
            TableTo = "users",
            Key = "id",
            Foreign = "user_id"
        };
        
        var orderItemsToOrdersRelationship = new RelationshipInfo
        {
            Name = "fk_order_items_orders",
            SchemaFrom = "public",
            SchemaTo = "public",
            TableFrom = "order_items",
            TableTo = "orders",
            Key = "id",
            Foreign = "order_id"
        };
        
        schema.Relationships = new List<RelationshipInfo> 
        { 
            ordersToUsersRelationship, 
            orderItemsToOrdersRelationship 
        };
        
        // Populate table-level relationships (simulating what extractors now do)
        usersTable.OutgoingRelationships = new List<RelationshipInfo>();
        usersTable.IncomingRelationships = new List<RelationshipInfo> { ordersToUsersRelationship };
        
        ordersTable.OutgoingRelationships = new List<RelationshipInfo> { ordersToUsersRelationship };
        ordersTable.IncomingRelationships = new List<RelationshipInfo> { orderItemsToOrdersRelationship };
        
        orderItemsTable.OutgoingRelationships = new List<RelationshipInfo> { orderItemsToOrdersRelationship };
        orderItemsTable.IncomingRelationships = new List<RelationshipInfo>();
        
        schema.Tables = new List<TableInfo> { usersTable, ordersTable, orderItemsTable };
        
        return schema;
    }
    
    /// <summary>
    /// Creates a sample schema with composite key relationship.
    /// </summary>
    private static DatabaseSchema CreateSchemaWithCompositeKey()
    {
        var schema = new DatabaseSchema();
        
        var ordersTable = new TableInfo
        {
            Schema = "public",
            Name = "orders",
            Columns = new List<ColumnInfo>
            {
                new() { Schema = "public", Table = "orders", Column = "order_id", Type = "integer", Nullable = false },
                new() { Schema = "public", Table = "orders", Column = "product_id", Type = "integer", Nullable = false }
            }
        };
        
        var orderDetailsTable = new TableInfo
        {
            Schema = "public",
            Name = "order_details",
            Columns = new List<ColumnInfo>
            {
                new() { Schema = "public", Table = "order_details", Column = "id", Type = "integer", Nullable = false },
                new() { Schema = "public", Table = "order_details", Column = "order_id", Type = "integer", Nullable = false },
                new() { Schema = "public", Table = "order_details", Column = "product_id", Type = "integer", Nullable = false }
            }
        };
        
        var compositeRelationship = new RelationshipInfo
        {
            Name = "fk_order_details_orders",
            SchemaFrom = "public",
            SchemaTo = "public",
            TableFrom = "order_details",
            TableTo = "orders",
            Key = "order_id,product_id",
            Foreign = "order_id,product_id"
        };
        
        schema.Relationships = new List<RelationshipInfo> { compositeRelationship };
        
        ordersTable.IncomingRelationships = new List<RelationshipInfo> { compositeRelationship };
        orderDetailsTable.OutgoingRelationships = new List<RelationshipInfo> { compositeRelationship };
        
        schema.Tables = new List<TableInfo> { ordersTable, orderDetailsTable };
        
        return schema;
    }
    
    [Fact]
    public void EfCoreGenerator_GeneratesNavigationProperties_ForOneToManyRelationship()
    {
        // Arrange
        var schema = CreateSampleSchemaWithRelationships();
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        
        // Act
        var entities = generator.GenerateEntities(schema);
        
        // Assert
        Assert.Contains("User.cs", entities.Keys);
        Assert.Contains("Order.cs", entities.Keys);
        
        var userEntity = entities["User.cs"];
        var orderEntity = entities["Order.cs"];
        
        // User should have a collection navigation property for Orders
        Assert.Contains("ICollection<Order>", userEntity);
        
        // Order should have a navigation property for User
        Assert.Contains("User?", orderEntity);
    }
    
    [Fact]
    public void EfCoreGenerator_GeneratesRelationshipConfiguration_ForOneToManyRelationship()
    {
        // Arrange
        var schema = CreateSampleSchemaWithRelationships();
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        
        // Act
        var configurations = generator.GenerateConfigurations(schema);
        
        // Assert
        Assert.Contains("OrderConfiguration.cs", configurations.Keys);
        
        var orderConfig = configurations["OrderConfiguration.cs"];
        
        // Should contain HasOne and WithMany
        Assert.Contains("HasOne", orderConfig);
        Assert.Contains("WithMany", orderConfig);
        Assert.Contains("HasForeignKey", orderConfig);
        Assert.Contains("HasPrincipalKey", orderConfig);
    }
    
    [Fact]
    public void EfCoreGenerator_GeneratesCompositeKeyConfiguration()
    {
        // Arrange
        var schema = CreateSchemaWithCompositeKey();
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        
        // Act
        var configurations = generator.GenerateConfigurations(schema);
        
        // Assert
        Assert.Contains("OrderDetailConfiguration.cs", configurations.Keys);
        
        var orderDetailConfig = configurations["OrderDetailConfiguration.cs"];
        
        // Should contain composite foreign key configuration
        Assert.Contains("new {", orderDetailConfig);
    }
    
    [Fact]
    public void DapperGenerator_GeneratesNavigationlessEntities()
    {
        // Arrange - Dapper doesn't generate navigation properties
        var schema = CreateSampleSchemaWithRelationships();
        var generator = new DapperGenerator(DatabaseType.PostgreSql, "TestNamespace");
        
        // Act
        var entities = generator.GenerateEntities(schema);
        
        // Assert
        Assert.Contains("User.cs", entities.Keys);
        Assert.Contains("Order.cs", entities.Keys);
        
        var userEntity = entities["User.cs"];
        
        // Dapper entities shouldn't have navigation properties
        Assert.DoesNotContain("ICollection<Order>", userEntity);
        Assert.DoesNotContain("virtual", userEntity);
    }
    
    [Fact]
    public void RelationshipInfo_ParsesCompositeKeys_Correctly()
    {
        // Arrange
        var relationship = new RelationshipInfo
        {
            Key = "order_id, product_id",
            Foreign = "order_id, product_id"
        };
        
        // Act
        var keys = relationship.Keys;
        var foreignKeys = relationship.ForeignKeys;
        
        // Assert
        Assert.Equal(2, keys.Length);
        Assert.Equal(2, foreignKeys.Length);
        Assert.Equal("order_id", keys[0]);
        Assert.Equal("product_id", keys[1]);
    }
    
    [Fact]
    public void RelationshipInfo_FullTableNames_AreCorrect()
    {
        // Arrange
        var relationship = new RelationshipInfo
        {
            SchemaFrom = "sales",
            TableFrom = "orders",
            SchemaTo = "public",
            TableTo = "users"
        };
        
        // Act & Assert
        Assert.Equal("sales.orders", relationship.FullTableFrom);
        Assert.Equal("public.users", relationship.FullTableTo);
    }
    
    [Fact]
    public void RelationshipInfo_FullTableNames_WhenNoSchema()
    {
        // Arrange
        var relationship = new RelationshipInfo
        {
            SchemaFrom = "",
            TableFrom = "orders",
            SchemaTo = "",
            TableTo = "users"
        };
        
        // Act & Assert
        Assert.Equal("orders", relationship.FullTableFrom);
        Assert.Equal("users", relationship.FullTableTo);
    }
}

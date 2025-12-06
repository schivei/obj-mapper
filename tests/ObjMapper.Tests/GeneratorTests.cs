using ObjMapper.Generators;
using ObjMapper.Models;

namespace ObjMapper.Tests;

/// <summary>
/// Additional tests for EfCore and Dapper generators.
/// </summary>
public class GeneratorTests
{
    private static DatabaseSchema CreateCompleteSchema()
    {
        var usersTable = new TableInfo
        {
            Schema = "public",
            Name = "users",
            Columns =
            [
                new ColumnInfo { Schema = "public", Table = "users", Column = "id", Type = "integer", Nullable = false, Comment = "Primary key" },
                new ColumnInfo { Schema = "public", Table = "users", Column = "name", Type = "varchar(100)", Nullable = false },
                new ColumnInfo { Schema = "public", Table = "users", Column = "email", Type = "varchar(255)", Nullable = true, Comment = "User email" },
                new ColumnInfo { Schema = "public", Table = "users", Column = "created_at", Type = "timestamp", Nullable = false },
                new ColumnInfo { Schema = "public", Table = "users", Column = "data", Type = "bytea", Nullable = false }
            ],
            Indexes =
            [
                new IndexInfo { Schema = "public", Table = "users", Name = "idx_email", Key = "email", Type = "unique" }
            ],
            IncomingRelationships = [],
            OutgoingRelationships = []
        };
        
        var ordersTable = new TableInfo
        {
            Schema = "public",
            Name = "orders",
            Columns =
            [
                new ColumnInfo { Schema = "public", Table = "orders", Column = "id", Type = "integer", Nullable = false },
                new ColumnInfo { Schema = "public", Table = "orders", Column = "user_id", Type = "integer", Nullable = false },
                new ColumnInfo { Schema = "public", Table = "orders", Column = "total", Type = "decimal(10,2)", Nullable = false }
            ],
            IncomingRelationships = [],
            OutgoingRelationships =
            [
                new RelationshipInfo
                {
                    Name = "fk_orders_users",
                    SchemaFrom = "public",
                    SchemaTo = "public",
                    TableFrom = "orders",
                    TableTo = "users",
                    Key = "id",
                    Foreign = "user_id"
                }
            ]
        };
        
        usersTable.IncomingRelationships =
        [
            new RelationshipInfo
            {
                Name = "fk_orders_users",
                SchemaFrom = "public",
                SchemaTo = "public",
                TableFrom = "orders",
                TableTo = "users",
                Key = "id",
                Foreign = "user_id"
            }
        ];
        
        return new DatabaseSchema
        {
            Tables = [usersTable, ordersTable],
            Relationships =
            [
                new RelationshipInfo
                {
                    Name = "fk_orders_users",
                    SchemaFrom = "public",
                    SchemaTo = "public",
                    TableFrom = "orders",
                    TableTo = "users",
                    Key = "id",
                    Foreign = "user_id"
                }
            ]
        };
    }

    [Theory]
    [InlineData(EntityTypeMode.Class, "class")]
    [InlineData(EntityTypeMode.Record, "record")]
    [InlineData(EntityTypeMode.Struct, "struct")]
    [InlineData(EntityTypeMode.RecordStruct, "record struct")]
    public void EfCoreGenerator_GeneratesCorrectEntityTypeMode(EntityTypeMode mode, string expected)
    {
        // Arrange
        var schema = CreateCompleteSchema();
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace") { EntityTypeMode = mode };
        
        // Act
        var entities = generator.GenerateEntities(schema);
        var userEntity = entities["User.cs"];
        
        // Assert
        Assert.Contains($"public partial {expected} User", userEntity);
    }
    
    [Theory]
    [InlineData(EntityTypeMode.Class, "class")]
    [InlineData(EntityTypeMode.Record, "record")]
    [InlineData(EntityTypeMode.Struct, "struct")]
    [InlineData(EntityTypeMode.RecordStruct, "record struct")]
    public void DapperGenerator_GeneratesCorrectEntityTypeMode(EntityTypeMode mode, string expected)
    {
        // Arrange
        var schema = CreateCompleteSchema();
        var generator = new DapperGenerator(DatabaseType.PostgreSql, "TestNamespace") { EntityTypeMode = mode };
        
        // Act
        var entities = generator.GenerateEntities(schema);
        var userEntity = entities["User.cs"];
        
        // Assert
        Assert.Contains($"public partial {expected} User", userEntity);
    }
    
    [Fact]
    public void EfCoreGenerator_GeneratesXmlComments_WhenColumnHasComment()
    {
        // Arrange
        var schema = CreateCompleteSchema();
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        
        // Act
        var entities = generator.GenerateEntities(schema);
        var userEntity = entities["User.cs"];
        
        // Assert
        Assert.Contains("/// <summary>", userEntity);
        Assert.Contains("Primary key", userEntity);
    }
    
    [Fact]
    public void EfCoreGenerator_GeneratesByteArrayInitializer()
    {
        // Arrange
        var schema = CreateCompleteSchema();
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        
        // Act
        var entities = generator.GenerateEntities(schema);
        var userEntity = entities["User.cs"];
        
        // Assert
        Assert.Contains("byte[] Data { get; set; } = [];", userEntity);
    }
    
    [Fact]
    public void EfCoreGenerator_GeneratesHasComment_WhenColumnHasComment()
    {
        // Arrange
        var schema = CreateCompleteSchema();
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        
        // Act
        var configurations = generator.GenerateConfigurations(schema);
        var userConfig = configurations["UserConfiguration.cs"];
        
        // Assert
        Assert.Contains(".HasComment(", userConfig);
    }
    
    [Fact]
    public void EfCoreGenerator_GeneratesHasColumnType_ForSpecialTypes()
    {
        // Arrange
        var schema = CreateCompleteSchema();
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        
        // Act
        var configurations = generator.GenerateConfigurations(schema);
        var userConfig = configurations["UserConfiguration.cs"];
        
        // Assert
        Assert.Contains(".HasColumnType(", userConfig);
    }
    
    [Fact]
    public void EfCoreGenerator_GeneratesDbContext_WithDbSets()
    {
        // Arrange
        var schema = CreateCompleteSchema();
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        
        // Act
        var context = generator.GenerateDbContext(schema, "TestContext");
        
        // Assert
        Assert.Contains("public DbSet<User> Users { get; set; } = null!;", context);
        Assert.Contains("public DbSet<Order> Orders { get; set; } = null!;", context);
    }
    
    [Fact]
    public void EfCoreGenerator_GeneratesDbContext_WithApplyConfiguration()
    {
        // Arrange
        var schema = CreateCompleteSchema();
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        
        // Act
        var context = generator.GenerateDbContext(schema, "TestContext");
        
        // Assert
        Assert.Contains("modelBuilder.ApplyConfiguration(new UserConfiguration());", context);
        Assert.Contains("modelBuilder.ApplyConfiguration(new OrderConfiguration());", context);
    }
    
    [Fact]
    public void EfCoreGenerator_GeneratesPartialMethod()
    {
        // Arrange
        var schema = CreateCompleteSchema();
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        
        // Act
        var context = generator.GenerateDbContext(schema, "TestContext");
        
        // Assert
        Assert.Contains("partial void OnModelCreatingPartial(ModelBuilder modelBuilder);", context);
    }
    
    [Theory]
    [InlineData(DatabaseType.MySql, "MySqlConnection")]
    [InlineData(DatabaseType.PostgreSql, "NpgsqlConnection")]
    [InlineData(DatabaseType.SqlServer, "SqlConnection")]
    [InlineData(DatabaseType.Sqlite, "SqliteConnection")]
    public void DapperGenerator_GeneratesCorrectConnectionType(DatabaseType dbType, string expectedConnection)
    {
        // Arrange
        var schema = CreateCompleteSchema();
        var generator = new DapperGenerator(dbType, "TestNamespace");
        
        // Act
        var context = generator.GenerateDbContext(schema, "TestContext");
        
        // Assert
        Assert.Contains(expectedConnection, context);
    }
    
    [Fact]
    public void DapperGenerator_GeneratesRepository_WithCrudMethods()
    {
        // Arrange
        var schema = CreateCompleteSchema();
        var generator = new DapperGenerator(DatabaseType.PostgreSql, "TestNamespace");
        
        // Act
        var configurations = generator.GenerateConfigurations(schema);
        var userRepo = configurations["UserRepository.cs"];
        
        // Assert
        Assert.Contains("GetAllAsync()", userRepo);
        Assert.Contains("GetByIdAsync(", userRepo);
        Assert.Contains("InsertAsync(", userRepo);
        Assert.Contains("UpdateAsync(", userRepo);
        Assert.Contains("DeleteAsync(", userRepo);
    }
    
    [Fact]
    public void DapperGenerator_GeneratesTableAttribute()
    {
        // Arrange
        var schema = CreateCompleteSchema();
        var generator = new DapperGenerator(DatabaseType.PostgreSql, "TestNamespace");
        
        // Act
        var entities = generator.GenerateEntities(schema);
        var userEntity = entities["User.cs"];
        
        // Assert
        Assert.Contains("[Table(\"users\", Schema = \"public\")]", userEntity);
    }
    
    [Fact]
    public void DapperGenerator_GeneratesKeyAttribute()
    {
        // Arrange
        var schema = CreateCompleteSchema();
        var generator = new DapperGenerator(DatabaseType.PostgreSql, "TestNamespace");
        
        // Act
        var entities = generator.GenerateEntities(schema);
        var userEntity = entities["User.cs"];
        
        // Assert
        Assert.Contains("[Key]", userEntity);
    }
    
    [Fact]
    public void DapperGenerator_GeneratesRequiredAttribute()
    {
        // Arrange
        var schema = CreateCompleteSchema();
        var generator = new DapperGenerator(DatabaseType.PostgreSql, "TestNamespace");
        
        // Act
        var entities = generator.GenerateEntities(schema);
        var userEntity = entities["User.cs"];
        
        // Assert
        Assert.Contains("[Required]", userEntity);
    }
    
    [Fact]
    public void EfCoreGenerator_HandlesTableWithNoSchema()
    {
        // Arrange
        var schema = new DatabaseSchema
        {
            Tables =
            [
                new TableInfo
                {
                    Schema = "",
                    Name = "users",
                    Columns =
                    [
                        new ColumnInfo { Schema = "", Table = "users", Column = "id", Type = "integer", Nullable = false }
                    ]
                }
            ]
        };
        var generator = new EfCoreGenerator(DatabaseType.Sqlite, "TestNamespace");
        
        // Act
        var configurations = generator.GenerateConfigurations(schema);
        var userConfig = configurations["UserConfiguration.cs"];
        
        // Assert
        Assert.Contains("builder.ToTable(\"users\");", userConfig);
        Assert.DoesNotContain("builder.ToTable(\"users\", \"\")", userConfig);
    }
}

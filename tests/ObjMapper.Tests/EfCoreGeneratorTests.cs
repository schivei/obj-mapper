using ObjMapper.Generators;
using ObjMapper.Models;

namespace ObjMapper.Tests;

public class EfCoreGeneratorTests
{
    private static DatabaseSchema CreateSimpleSchema()
    {
        return new DatabaseSchema
        {
            Tables =
            [
                new TableInfo
                {
                    Schema = "public",
                    Name = "users",
                    Columns =
                    [
                        new ColumnInfo { Schema = "public", Table = "users", Column = "id", Type = "int", Nullable = false },
                        new ColumnInfo { Schema = "public", Table = "users", Column = "name", Type = "varchar(100)", Nullable = false },
                        new ColumnInfo { Schema = "public", Table = "users", Column = "email", Type = "varchar(255)", Nullable = true, Comment = "User email address" }
                    ]
                }
            ]
        };
    }

    private static DatabaseSchema CreateSchemaWithRelationships()
    {
        var schema = new DatabaseSchema
        {
            Tables =
            [
                new TableInfo
                {
                    Schema = "public",
                    Name = "users",
                    Columns =
                    [
                        new ColumnInfo { Schema = "public", Table = "users", Column = "id", Type = "int", Nullable = false },
                        new ColumnInfo { Schema = "public", Table = "users", Column = "name", Type = "varchar(100)", Nullable = false }
                    ]
                },
                new TableInfo
                {
                    Schema = "public",
                    Name = "orders",
                    Columns =
                    [
                        new ColumnInfo { Schema = "public", Table = "orders", Column = "id", Type = "int", Nullable = false },
                        new ColumnInfo { Schema = "public", Table = "orders", Column = "user_id", Type = "int", Nullable = false },
                        new ColumnInfo { Schema = "public", Table = "orders", Column = "total", Type = "decimal(10,2)", Nullable = false }
                    ]
                }
            ]
        };

        var relationship = new RelationshipInfo
        {
            Name = "fk_orders_users",
            SchemaFrom = "public",
            SchemaTo = "public",
            TableFrom = "orders",
            TableTo = "users",
            Key = "id",
            Foreign = "user_id"
        };

        schema.Tables[0].IncomingRelationships.Add(relationship);
        schema.Tables[1].OutgoingRelationships.Add(relationship);

        return schema;
    }

    [Fact]
    public void GenerateEntities_ReturnsCorrectNumberOfEntities()
    {
        // Arrange
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var entities = generator.GenerateEntities(schema);

        // Assert
        Assert.Single(entities);
        Assert.Contains("User.cs", entities.Keys);
    }

    [Fact]
    public void GenerateEntities_IncludesCorrectNamespace()
    {
        // Arrange
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "MyApp.Data");
        var schema = CreateSimpleSchema();

        // Act
        var entities = generator.GenerateEntities(schema);

        // Assert
        Assert.Contains("namespace MyApp.Data;", entities["User.cs"]);
    }

    [Fact]
    public void GenerateEntities_IncludesProperties()
    {
        // Arrange
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var entities = generator.GenerateEntities(schema);
        var userEntity = entities["User.cs"];

        // Assert
        Assert.Contains("public int Id { get; set; }", userEntity);
        Assert.Contains("public string Name { get; set; }", userEntity);
        // Nullable string doesn't add '?' since string is already a reference type
        Assert.Contains("public string Email { get; set; }", userEntity);
    }

    [Fact]
    public void GenerateEntities_IncludesXmlComments()
    {
        // Arrange
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var entities = generator.GenerateEntities(schema);
        var userEntity = entities["User.cs"];

        // Assert
        Assert.Contains("/// <summary>", userEntity);
        Assert.Contains("/// User email address", userEntity);
    }

    [Fact]
    public void GenerateEntities_WithRelationships_IncludesNavigationProperties()
    {
        // Arrange
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSchemaWithRelationships();

        // Act
        var entities = generator.GenerateEntities(schema);

        // Assert
        var userEntity = entities["User.cs"];
        var orderEntity = entities["Order.cs"];

        Assert.Contains("public virtual ICollection<Order> Orders { get; set; }", userEntity);
        Assert.Contains("public virtual User? User { get; set; }", orderEntity);
    }

    [Fact]
    public void GenerateConfigurations_ReturnsCorrectNumberOfConfigurations()
    {
        // Arrange
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var configs = generator.GenerateConfigurations(schema);

        // Assert
        Assert.Single(configs);
        Assert.Contains("UserConfiguration.cs", configs.Keys);
    }

    [Fact]
    public void GenerateConfigurations_IncludesTableMapping()
    {
        // Arrange
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var configs = generator.GenerateConfigurations(schema);
        var userConfig = configs["UserConfiguration.cs"];

        // Assert
        Assert.Contains("builder.ToTable(\"users\", \"public\");", userConfig);
    }

    [Fact]
    public void GenerateConfigurations_IncludesPrimaryKey()
    {
        // Arrange
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var configs = generator.GenerateConfigurations(schema);
        var userConfig = configs["UserConfiguration.cs"];

        // Assert
        Assert.Contains("builder.HasKey(e => e.Id);", userConfig);
    }

    [Fact]
    public void GenerateConfigurations_IncludesColumnConfiguration()
    {
        // Arrange
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var configs = generator.GenerateConfigurations(schema);
        var userConfig = configs["UserConfiguration.cs"];

        // Assert
        Assert.Contains(".HasColumnName(\"name\")", userConfig);
        Assert.Contains(".IsRequired()", userConfig);
    }

    [Fact]
    public void GenerateDbContext_IncludesDbSets()
    {
        // Arrange
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSchemaWithRelationships();

        // Act
        var context = generator.GenerateDbContext(schema, "AppDbContext");

        // Assert
        Assert.Contains("public DbSet<User> Users { get; set; }", context);
        Assert.Contains("public DbSet<Order> Orders { get; set; }", context);
    }

    [Fact]
    public void GenerateDbContext_IncludesConfigurationApply()
    {
        // Arrange
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var context = generator.GenerateDbContext(schema, "MyContext");

        // Assert
        Assert.Contains("modelBuilder.ApplyConfiguration(new UserConfiguration());", context);
    }

    [Fact]
    public void GenerateDbContext_IncludesConstructor()
    {
        // Arrange
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var context = generator.GenerateDbContext(schema, "TestContext");

        // Assert
        Assert.Contains("public TestContext(DbContextOptions<TestContext> options) : base(options)", context);
    }

    [Theory]
    [InlineData(EntityTypeMode.Class, "public partial class")]
    [InlineData(EntityTypeMode.Record, "public partial record")]
    [InlineData(EntityTypeMode.Struct, "public partial struct")]
    [InlineData(EntityTypeMode.RecordStruct, "public partial record struct")]
    public void GenerateEntities_RespectsEntityTypeMode(EntityTypeMode mode, string expectedKeyword)
    {
        // Arrange
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace")
        {
            EntityTypeMode = mode
        };
        var schema = CreateSimpleSchema();

        // Act
        var entities = generator.GenerateEntities(schema);
        var userEntity = entities["User.cs"];

        // Assert
        Assert.Contains(expectedKeyword, userEntity);
    }

    [Fact]
    public void GenerateEntities_StringProperties_HaveDefaultValue()
    {
        // Arrange
        var generator = new EfCoreGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var entities = generator.GenerateEntities(schema);
        var userEntity = entities["User.cs"];

        // Assert
        Assert.Contains("public string Name { get; set; } = string.Empty;", userEntity);
    }
}

using ObjMapper.Generators;
using ObjMapper.Models;

namespace ObjMapper.Tests;

public class DapperGeneratorTests
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

    [Fact]
    public void GenerateEntities_ReturnsCorrectNumberOfEntities()
    {
        // Arrange
        var generator = new DapperGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var entities = generator.GenerateEntities(schema);

        // Assert
        Assert.Single(entities);
        Assert.Contains("User.cs", entities.Keys);
    }

    [Fact]
    public void GenerateEntities_IncludesTableAttribute()
    {
        // Arrange
        var generator = new DapperGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var entities = generator.GenerateEntities(schema);
        var userEntity = entities["User.cs"];

        // Assert
        Assert.Contains("[Table(\"users\", Schema = \"public\")]", userEntity);
    }

    [Fact]
    public void GenerateEntities_IncludesKeyAttribute()
    {
        // Arrange
        var generator = new DapperGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var entities = generator.GenerateEntities(schema);
        var userEntity = entities["User.cs"];

        // Assert
        Assert.Contains("[Key]", userEntity);
    }

    [Fact]
    public void GenerateEntities_IncludesRequiredAttribute()
    {
        // Arrange
        var generator = new DapperGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var entities = generator.GenerateEntities(schema);
        var userEntity = entities["User.cs"];

        // Assert
        Assert.Contains("[Required]", userEntity);
    }

    [Fact]
    public void GenerateEntities_IncludesProperties()
    {
        // Arrange
        var generator = new DapperGenerator(DatabaseType.PostgreSql, "TestNamespace");
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
    public void GenerateConfigurations_GeneratesRepositories()
    {
        // Arrange
        var generator = new DapperGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var repositories = generator.GenerateConfigurations(schema);

        // Assert
        Assert.Single(repositories);
        Assert.Contains("UserRepository.cs", repositories.Keys);
    }

    [Fact]
    public void GenerateConfigurations_RepositoryIncludesGetAllMethod()
    {
        // Arrange
        var generator = new DapperGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var repositories = generator.GenerateConfigurations(schema);
        var userRepository = repositories["UserRepository.cs"];

        // Assert
        Assert.Contains("public async Task<IEnumerable<User>> GetAllAsync()", userRepository);
        Assert.Contains("SELECT * FROM public.users", userRepository);
    }

    [Fact]
    public void GenerateConfigurations_RepositoryIncludesGetByIdMethod()
    {
        // Arrange
        var generator = new DapperGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var repositories = generator.GenerateConfigurations(schema);
        var userRepository = repositories["UserRepository.cs"];

        // Assert
        Assert.Contains("public async Task<User?> GetByIdAsync(int id)", userRepository);
        Assert.Contains("WHERE id = @Id", userRepository);
    }

    [Fact]
    public void GenerateConfigurations_RepositoryIncludesInsertMethod()
    {
        // Arrange
        var generator = new DapperGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var repositories = generator.GenerateConfigurations(schema);
        var userRepository = repositories["UserRepository.cs"];

        // Assert
        Assert.Contains("public async Task<int> InsertAsync(User entity)", userRepository);
        Assert.Contains("INSERT INTO", userRepository);
    }

    [Fact]
    public void GenerateConfigurations_RepositoryIncludesUpdateMethod()
    {
        // Arrange
        var generator = new DapperGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var repositories = generator.GenerateConfigurations(schema);
        var userRepository = repositories["UserRepository.cs"];

        // Assert
        Assert.Contains("public async Task<int> UpdateAsync(User entity)", userRepository);
        Assert.Contains("UPDATE", userRepository);
    }

    [Fact]
    public void GenerateConfigurations_RepositoryIncludesDeleteMethod()
    {
        // Arrange
        var generator = new DapperGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var repositories = generator.GenerateConfigurations(schema);
        var userRepository = repositories["UserRepository.cs"];

        // Assert
        Assert.Contains("public async Task<int> DeleteAsync(int id)", userRepository);
        Assert.Contains("DELETE FROM", userRepository);
    }

    [Fact]
    public void GenerateDbContext_IncludesConnectionProperty()
    {
        // Arrange
        var generator = new DapperGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var context = generator.GenerateDbContext(schema, "DapperContext");

        // Assert
        Assert.Contains("public IDbConnection Connection", context);
    }

    [Fact]
    public void GenerateDbContext_IncludesRepositoryProperties()
    {
        // Arrange
        var generator = new DapperGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var context = generator.GenerateDbContext(schema, "DapperContext");

        // Assert
        Assert.Contains("UserRepository", context);
        Assert.Contains("Users =>", context);
    }

    [Fact]
    public void GenerateDbContext_IncludesDispose()
    {
        // Arrange
        var generator = new DapperGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var context = generator.GenerateDbContext(schema, "DapperContext");

        // Assert
        Assert.Contains("public void Dispose()", context);
        Assert.Contains("_connection?.Dispose()", context);
    }

    [Theory]
    [InlineData(DatabaseType.PostgreSql, "NpgsqlConnection")]
    [InlineData(DatabaseType.MySql, "MySqlConnection")]
    [InlineData(DatabaseType.SqlServer, "SqlConnection")]
    [InlineData(DatabaseType.Sqlite, "SqliteConnection")]
    public void GenerateDbContext_UsesCorrectConnectionType(DatabaseType dbType, string expectedConnection)
    {
        // Arrange
        var generator = new DapperGenerator(dbType, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var context = generator.GenerateDbContext(schema, "DapperContext");

        // Assert
        Assert.Contains(expectedConnection, context);
    }

    [Theory]
    [InlineData(EntityTypeMode.Class, "public partial class")]
    [InlineData(EntityTypeMode.Record, "public partial record")]
    [InlineData(EntityTypeMode.Struct, "public partial struct")]
    [InlineData(EntityTypeMode.RecordStruct, "public partial record struct")]
    public void GenerateEntities_RespectsEntityTypeMode(EntityTypeMode mode, string expectedKeyword)
    {
        // Arrange
        var generator = new DapperGenerator(DatabaseType.PostgreSql, "TestNamespace")
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
    public void GenerateEntities_WithNoSchema_UsesTableNameOnly()
    {
        // Arrange
        var generator = new DapperGenerator(DatabaseType.Sqlite, "TestNamespace");
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
                        new ColumnInfo { Schema = "", Table = "users", Column = "id", Type = "int", Nullable = false }
                    ]
                }
            ]
        };

        // Act
        var entities = generator.GenerateEntities(schema);
        var userEntity = entities["User.cs"];

        // Assert
        Assert.Contains("[Table(\"users\")]", userEntity);
        Assert.DoesNotContain("Schema =", userEntity);
    }

    [Fact]
    public void GenerateEntities_IncludesXmlComments()
    {
        // Arrange
        var generator = new DapperGenerator(DatabaseType.PostgreSql, "TestNamespace");
        var schema = CreateSimpleSchema();

        // Act
        var entities = generator.GenerateEntities(schema);
        var userEntity = entities["User.cs"];

        // Assert
        Assert.Contains("/// <summary>", userEntity);
        Assert.Contains("/// User email address", userEntity);
    }
}

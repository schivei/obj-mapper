using ObjMapper.Models;
using ObjMapper.Services;
using ObjMapper.Services.Extractors;

namespace ObjMapper.Tests;

public class SchemaExtractorFactoryTests
{
    [Theory]
    [InlineData(DatabaseType.Sqlite)]
    [InlineData(DatabaseType.PostgreSql)]
    [InlineData(DatabaseType.MySql)]
    [InlineData(DatabaseType.SqlServer)]
    public void Create_ReturnsCorrectExtractor(DatabaseType databaseType)
    {
        // Act
        var extractor = SchemaExtractorFactory.Create(databaseType);

        // Assert
        Assert.NotNull(extractor);
        Assert.IsAssignableFrom<IDatabaseSchemaExtractor>(extractor);
    }

    [Fact]
    public void Create_Sqlite_ReturnsSqliteExtractor()
    {
        // Act
        var extractor = SchemaExtractorFactory.Create(DatabaseType.Sqlite);

        // Assert
        Assert.IsType<SqliteSchemaExtractor>(extractor);
    }

    [Fact]
    public void Create_PostgreSql_ReturnsPostgresExtractor()
    {
        // Act
        var extractor = SchemaExtractorFactory.Create(DatabaseType.PostgreSql);

        // Assert
        Assert.IsType<PostgresSchemaExtractor>(extractor);
    }

    [Fact]
    public void Create_MySql_ReturnsMySqlExtractor()
    {
        // Act
        var extractor = SchemaExtractorFactory.Create(DatabaseType.MySql);

        // Assert
        Assert.IsType<MySqlSchemaExtractor>(extractor);
    }

    [Fact]
    public void Create_SqlServer_ReturnsSqlServerExtractor()
    {
        // Act
        var extractor = SchemaExtractorFactory.Create(DatabaseType.SqlServer);

        // Assert
        Assert.IsType<SqlServerSchemaExtractor>(extractor);
    }

    [Fact]
    public void Create_Oracle_ThrowsNotSupportedException()
    {
        // Act & Assert
        var exception = Assert.Throws<NotSupportedException>(() => 
            SchemaExtractorFactory.Create(DatabaseType.Oracle));
        Assert.Contains("Oracle", exception.Message);
    }

    [Theory]
    [InlineData("Data Source=test.db", DatabaseType.Sqlite)]
    [InlineData("Data Source=:memory:", DatabaseType.Sqlite)]
    [InlineData("Data Source=mydb.sqlite", DatabaseType.Sqlite)]
    public void DetectDatabaseType_Sqlite_DetectsCorrectly(string connectionString, DatabaseType expected)
    {
        // Act
        var result = SchemaExtractorFactory.DetectDatabaseType(connectionString);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Host=localhost;Database=testdb;Username=test;Password=test", DatabaseType.PostgreSql)]
    [InlineData("Host=myserver;Database=mydb;Username=user;Password=pass", DatabaseType.PostgreSql)]
    public void DetectDatabaseType_PostgreSql_DetectsCorrectly(string connectionString, DatabaseType expected)
    {
        // Act
        var result = SchemaExtractorFactory.DetectDatabaseType(connectionString);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Server=localhost;Database=testdb;User Id=sa;Password=Test@12345;TrustServerCertificate=True", DatabaseType.SqlServer)]
    [InlineData("Server=myserver;Initial Catalog=mydb;Integrated Security=True", DatabaseType.SqlServer)]
    [InlineData("Data Source=myserver;Initial Catalog=mydb;Trusted_Connection=True", DatabaseType.SqlServer)]
    public void DetectDatabaseType_SqlServer_DetectsCorrectly(string connectionString, DatabaseType expected)
    {
        // Act
        var result = SchemaExtractorFactory.DetectDatabaseType(connectionString);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Server=localhost;Database=testdb;User=test;Password=test", DatabaseType.MySql)]
    [InlineData("Server=myserver;Database=mydb;Uid=user;Pwd=pass", DatabaseType.MySql)]
    public void DetectDatabaseType_MySql_DetectsCorrectly(string connectionString, DatabaseType expected)
    {
        // Act
        var result = SchemaExtractorFactory.DetectDatabaseType(connectionString);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown connection string format")]
    public void DetectDatabaseType_UnknownFormat_ReturnsNull(string connectionString)
    {
        // Act
        var result = SchemaExtractorFactory.DetectDatabaseType(connectionString);

        // Assert
        Assert.Null(result);
    }
}

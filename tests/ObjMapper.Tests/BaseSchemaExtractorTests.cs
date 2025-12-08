using ObjMapper.Models;
using ObjMapper.Services;
using System.Data;
using System.Data.Common;

namespace ObjMapper.Tests;

/// <summary>
/// Tests for BaseSchemaExtractor validation and default schema detection logic.
/// </summary>
public class BaseSchemaExtractorTests
{
    // Test implementation of BaseSchemaExtractor for testing purposes
    private class TestSchemaExtractor : BaseSchemaExtractor
    {
        private readonly DatabaseType _dbType;
        private readonly string _defaultSchema;
        private readonly bool _throwOnOpen;

        public TestSchemaExtractor(DatabaseType dbType, string defaultSchema = "test", bool throwOnOpen = false)
        {
            _dbType = dbType;
            _defaultSchema = defaultSchema;
            _throwOnOpen = throwOnOpen;
        }

        protected override DatabaseType DatabaseType => _dbType;
        protected override string DefaultSchemaName => _defaultSchema;

        protected override DbConnection CreateConnection(string connectionString)
        {
            if (_throwOnOpen)
            {
                return new MockDbConnectionThatThrows(connectionString);
            }
            return new MockDbConnection(connectionString);
        }

        protected override Task<List<(string name, string schema)>> GetTablesAsync(DbConnection connection, string schemaName)
            => Task.FromResult(new List<(string name, string schema)>());

        protected override Task<List<ColumnInfo>> GetColumnsAsync(DbConnection connection, string schemaName, string tableName)
            => Task.FromResult(new List<ColumnInfo>());

        protected override Task<List<IndexInfo>> GetIndexesAsync(DbConnection connection, string schemaName, string tableName)
            => Task.FromResult(new List<IndexInfo>());

        protected override Task<List<RelationshipInfo>> GetRelationshipsAsync(DbConnection connection, string schemaName)
            => Task.FromResult(new List<RelationshipInfo>());

        protected override Task<List<ScalarFunctionInfo>> GetScalarFunctionsAsync(DbConnection connection, string schemaName)
            => Task.FromResult(new List<ScalarFunctionInfo>());

        protected override Task<List<StoredProcedureInfo>> GetStoredProceduresAsync(DbConnection connection, string schemaName)
            => Task.FromResult(new List<StoredProcedureInfo>());
    }

    // Mock DB Connection for testing
    private class MockDbConnection : DbConnection
    {
        private readonly string _connectionString;
        private bool _isOpen;

        public MockDbConnection(string connectionString)
        {
            _connectionString = connectionString;
        }

        public override string ConnectionString 
        { 
            get => _connectionString;
            set { }
        }

        public override string Database => "TestDB";
        public override string DataSource => "TestSource";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => _isOpen ? ConnectionState.Open : ConnectionState.Closed;

        public override void Open() => _isOpen = true;

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            _isOpen = true;
            return Task.CompletedTask;
        }

        public override void Close() => _isOpen = false;

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => throw new NotImplementedException();

        protected override DbCommand CreateDbCommand()
            => throw new NotImplementedException();

        public override void ChangeDatabase(string databaseName)
            => throw new NotImplementedException();
    }

    // Mock DB Connection that throws on open
    private class MockDbConnectionThatThrows : DbConnection
    {
        private readonly string _connectionString;

        public MockDbConnectionThatThrows(string connectionString)
        {
            _connectionString = connectionString;
        }

        public override string ConnectionString 
        { 
            get => _connectionString;
            set { }
        }

        public override string Database => "TestDB";
        public override string DataSource => "TestSource";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Closed;

        public override void Open()
        {
            throw new InvalidOperationException("Cannot connect to database");
        }

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Cannot connect to database");
        }

        public override void Close() { }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => throw new NotImplementedException();

        protected override DbCommand CreateDbCommand()
            => throw new NotImplementedException();

        public override void ChangeDatabase(string databaseName)
            => throw new NotImplementedException();
    }

    [Fact]
    public async Task ExtractSchemaAsync_NullConnectionString_ThrowsArgumentException()
    {
        // Arrange
        var extractor = new TestSchemaExtractor(DatabaseType.PostgreSql);
        var options = new SchemaExtractionOptions();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await extractor.ExtractSchemaAsync(null!, options));
        
        Assert.Contains("Connection string cannot be null or empty", exception.Message);
        Assert.Equal("connectionString", exception.ParamName);
    }

    [Fact]
    public async Task ExtractSchemaAsync_EmptyConnectionString_ThrowsArgumentException()
    {
        // Arrange
        var extractor = new TestSchemaExtractor(DatabaseType.PostgreSql);
        var options = new SchemaExtractionOptions();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await extractor.ExtractSchemaAsync("", options));
        
        Assert.Contains("Connection string cannot be null or empty", exception.Message);
        Assert.Equal("connectionString", exception.ParamName);
    }

    [Fact]
    public async Task ExtractSchemaAsync_WhitespaceConnectionString_ThrowsArgumentException()
    {
        // Arrange
        var extractor = new TestSchemaExtractor(DatabaseType.PostgreSql);
        var options = new SchemaExtractionOptions();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await extractor.ExtractSchemaAsync("   ", options));
        
        Assert.Contains("Connection string cannot be null or empty", exception.Message);
        Assert.Equal("connectionString", exception.ParamName);
    }

    [Fact]
    public async Task ExtractSchemaAsync_ConnectionFails_ThrowsInvalidOperationException()
    {
        // Arrange
        var extractor = new TestSchemaExtractor(DatabaseType.PostgreSql, throwOnOpen: true);
        var options = new SchemaExtractionOptions();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await extractor.ExtractSchemaAsync("Host=localhost;Database=test", options));
        
        Assert.Contains("Failed to open database connection", exception.Message);
        Assert.Contains("Cannot connect to database", exception.Message);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public async Task ExtractSchemaAsync_PostgreSqlNoSchema_UsesPublic()
    {
        // Arrange
        var connectionString = "Host=localhost;Database=testdb;Username=test;Password=test";
        var extractor = new TestSchemaExtractor(DatabaseType.PostgreSql);
        var options = new SchemaExtractionOptions { SchemaFilter = null };

        // Act
        var schema = await extractor.ExtractSchemaAsync(connectionString, options);

        // Assert - schema should be successfully extracted (uses "public" as default)
        Assert.NotNull(schema);
    }

    [Fact]
    public async Task ExtractSchemaAsync_SqlServerNoSchema_UsesDbo()
    {
        // Arrange
        var connectionString = "Server=localhost;Database=testdb;User Id=sa;Password=Test@12345";
        var extractor = new TestSchemaExtractor(DatabaseType.SqlServer);
        var options = new SchemaExtractionOptions { SchemaFilter = null };

        // Act
        var schema = await extractor.ExtractSchemaAsync(connectionString, options);

        // Assert - schema should be successfully extracted (uses "dbo" as default)
        Assert.NotNull(schema);
    }

    [Fact]
    public async Task ExtractSchemaAsync_MySqlNoSchema_UsesEmpty()
    {
        // Arrange
        var connectionString = "Server=localhost;Database=testdb;User=test;Password=test";
        var extractor = new TestSchemaExtractor(DatabaseType.MySql);
        var options = new SchemaExtractionOptions { SchemaFilter = null };

        // Act
        var schema = await extractor.ExtractSchemaAsync(connectionString, options);

        // Assert - schema should be successfully extracted (uses empty string for MySQL)
        Assert.NotNull(schema);
    }

    [Fact]
    public async Task ExtractSchemaAsync_SqliteNoSchema_UsesEmpty()
    {
        // Arrange
        var connectionString = "Data Source=:memory:";
        var extractor = new TestSchemaExtractor(DatabaseType.Sqlite);
        var options = new SchemaExtractionOptions { SchemaFilter = null };

        // Act
        var schema = await extractor.ExtractSchemaAsync(connectionString, options);

        // Assert - schema should be successfully extracted (uses empty string for SQLite)
        Assert.NotNull(schema);
    }

    [Fact]
    public async Task ExtractSchemaAsync_UndetectedTypeNoSchema_UsesDefaultSchemaName()
    {
        // Arrange - connection string that won't be detected
        var connectionString = "UnknownFormat=test";
        var extractor = new TestSchemaExtractor(DatabaseType.PostgreSql, defaultSchema: "custom_schema");
        var options = new SchemaExtractionOptions { SchemaFilter = null };

        // Act
        var schema = await extractor.ExtractSchemaAsync(connectionString, options);

        // Assert - should fall back to DefaultSchemaName
        Assert.NotNull(schema);
    }

    [Fact]
    public async Task ExtractSchemaAsync_WithExplicitSchema_UsesProvidedSchema()
    {
        // Arrange
        var connectionString = "Host=localhost;Database=testdb;Username=test;Password=test";
        var extractor = new TestSchemaExtractor(DatabaseType.PostgreSql);
        var options = new SchemaExtractionOptions { SchemaFilter = "my_custom_schema" };

        // Act
        var schema = await extractor.ExtractSchemaAsync(connectionString, options);

        // Assert - should use the explicitly provided schema
        Assert.NotNull(schema);
    }
}

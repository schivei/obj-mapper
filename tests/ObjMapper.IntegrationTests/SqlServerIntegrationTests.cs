using Microsoft.Data.SqlClient;

namespace ObjMapper.IntegrationTests;

/// <summary>
/// Integration tests for SQL Server.
/// These tests require a running SQL Server instance.
/// Set SQLSERVER_CONNECTION environment variable to run these tests.
/// </summary>
public class SqlServerIntegrationTests : IDisposable
{
    private readonly SqlConnection? _connection;
    private readonly string? _connectionString;
    private readonly bool _canRun;

    public SqlServerIntegrationTests()
    {
        _connectionString = Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION");
        _canRun = !string.IsNullOrEmpty(_connectionString);

        if (_canRun)
        {
            try
            {
                _connection = new SqlConnection(_connectionString);
                _connection.Open();
                SetupDatabase();
            }
            catch
            {
                _canRun = false;
                _connection?.Dispose();
                _connection = null;
            }
        }
    }

    public void Dispose()
    {
        if (_connection != null)
        {
            CleanupDatabase();
            _connection.Dispose();
        }
        GC.SuppressFinalize(this);
    }

    private void SetupDatabase()
    {
        // Create test database if not exists
        using (var command = _connection!.CreateCommand())
        {
            command.CommandText = @"
                IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'omap_test')
                BEGIN
                    CREATE DATABASE omap_test
                END";
            command.ExecuteNonQuery();
        }

        // Switch to test database
        _connection.ChangeDatabase("omap_test");

        using (var command = _connection.CreateCommand())
        {
            command.CommandText = @"
                IF OBJECT_ID('order_items', 'U') IS NOT NULL DROP TABLE order_items;
                IF OBJECT_ID('orders', 'U') IS NOT NULL DROP TABLE orders;
                IF OBJECT_ID('users', 'U') IS NOT NULL DROP TABLE users;
                
                CREATE TABLE users (
                    id INT IDENTITY(1,1) PRIMARY KEY,
                    name NVARCHAR(100) NOT NULL,
                    email NVARCHAR(255),
                    created_at DATETIME2 DEFAULT GETDATE()
                );
                
                CREATE TABLE orders (
                    id INT IDENTITY(1,1) PRIMARY KEY,
                    user_id INT NOT NULL,
                    total DECIMAL(10,2) NOT NULL,
                    status NVARCHAR(50) DEFAULT 'pending',
                    created_at DATETIME2 DEFAULT GETDATE(),
                    CONSTRAINT FK_orders_users FOREIGN KEY (user_id) REFERENCES users(id)
                );
                
                CREATE TABLE order_items (
                    id INT IDENTITY(1,1) PRIMARY KEY,
                    order_id INT NOT NULL,
                    product_name NVARCHAR(255) NOT NULL,
                    quantity INT NOT NULL,
                    price DECIMAL(10,2) NOT NULL,
                    CONSTRAINT FK_order_items_orders FOREIGN KEY (order_id) REFERENCES orders(id)
                );
                
                CREATE INDEX idx_orders_user_id ON orders(user_id);
                CREATE INDEX idx_order_items_order_id ON order_items(order_id);
                CREATE UNIQUE INDEX idx_users_email ON users(email);
            ";
            command.ExecuteNonQuery();
        }
    }

    private void CleanupDatabase()
    {
        try
        {
            _connection?.ChangeDatabase("omap_test");
            using var command = _connection!.CreateCommand();
            command.CommandText = @"
                IF OBJECT_ID('order_items', 'U') IS NOT NULL DROP TABLE order_items;
                IF OBJECT_ID('orders', 'U') IS NOT NULL DROP TABLE orders;
                IF OBJECT_ID('users', 'U') IS NOT NULL DROP TABLE users;
            ";
            command.ExecuteNonQuery();
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [SkippableFact]
    public void CanQuerySchemaFromSqlServer()
    {
        Skip.IfNot(_canRun, "SQL Server connection not available");

        // Arrange
        using var command = _connection!.CreateCommand();
        command.CommandText = @"
            SELECT TABLE_NAME 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_NAME";

        // Act
        var tables = new List<string>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }
        }

        // Assert
        Assert.Contains("users", tables);
        Assert.Contains("orders", tables);
        Assert.Contains("order_items", tables);
    }

    [SkippableFact]
    public void CanQueryTableColumnsFromSqlServer()
    {
        Skip.IfNot(_canRun, "SQL Server connection not available");

        // Arrange
        using var command = _connection!.CreateCommand();
        command.CommandText = @"
            SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE 
            FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_NAME = 'users'
            ORDER BY ORDINAL_POSITION";

        // Act
        var columns = new List<(string name, string type, bool nullable)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                columns.Add((
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2) == "YES"
                ));
            }
        }

        // Assert
        Assert.Equal(4, columns.Count);
        Assert.Contains(columns, c => c.name == "id" && c.type == "int");
        Assert.Contains(columns, c => c.name == "name" && c.type == "nvarchar" && !c.nullable);
    }

    [SkippableFact]
    public void CanQueryForeignKeysFromSqlServer()
    {
        Skip.IfNot(_canRun, "SQL Server connection not available");

        // Arrange
        using var command = _connection!.CreateCommand();
        command.CommandText = @"
            SELECT 
                fk.name AS constraint_name,
                OBJECT_NAME(fk.parent_object_id) AS table_name,
                c.name AS column_name,
                OBJECT_NAME(fk.referenced_object_id) AS referenced_table,
                rc.name AS referenced_column
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            INNER JOIN sys.columns c ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
            INNER JOIN sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id";

        // Act
        var foreignKeys = new List<(string name, string table, string column, string refTable, string refColumn)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                foreignKeys.Add((
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4)
                ));
            }
        }

        // Assert
        Assert.Equal(2, foreignKeys.Count);
        Assert.Contains(foreignKeys, fk => fk.table == "orders" && fk.refTable == "users");
        Assert.Contains(foreignKeys, fk => fk.table == "order_items" && fk.refTable == "orders");
    }

    [SkippableFact]
    public void CanQueryIndexesFromSqlServer()
    {
        Skip.IfNot(_canRun, "SQL Server connection not available");

        // Arrange
        using var command = _connection!.CreateCommand();
        command.CommandText = @"
            SELECT i.name, OBJECT_NAME(i.object_id) AS table_name
            FROM sys.indexes i
            WHERE i.name IS NOT NULL
              AND i.is_primary_key = 0
              AND i.name NOT LIKE 'PK_%'
              AND OBJECT_NAME(i.object_id) IN ('users', 'orders', 'order_items')";

        // Act
        var indexes = new List<(string name, string table)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                indexes.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        // Assert
        Assert.Contains(indexes, i => i.name == "idx_users_email");
        Assert.Contains(indexes, i => i.name == "idx_orders_user_id");
    }
}

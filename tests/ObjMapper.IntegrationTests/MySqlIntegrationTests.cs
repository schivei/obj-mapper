using MySqlConnector;

namespace ObjMapper.IntegrationTests;

/// <summary>
/// Integration tests for MySQL.
/// These tests require a running MySQL instance.
/// Set MYSQL_CONNECTION environment variable to run these tests.
/// </summary>
public class MySqlIntegrationTests : IDisposable
{
    private readonly MySqlConnection? _connection;
    private readonly string? _connectionString;
    private readonly bool _canRun;

    public MySqlIntegrationTests()
    {
        _connectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION");
        _canRun = !string.IsNullOrEmpty(_connectionString);

        if (_canRun)
        {
            try
            {
                _connection = new MySqlConnection(_connectionString);
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
        using var command = _connection!.CreateCommand();
        command.CommandText = @"
            SET FOREIGN_KEY_CHECKS = 0;
            DROP TABLE IF EXISTS order_items;
            DROP TABLE IF EXISTS orders;
            DROP TABLE IF EXISTS users;
            SET FOREIGN_KEY_CHECKS = 1;
            
            CREATE TABLE users (
                id INT AUTO_INCREMENT PRIMARY KEY,
                name VARCHAR(100) NOT NULL,
                email VARCHAR(255),
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );
            
            CREATE TABLE orders (
                id INT AUTO_INCREMENT PRIMARY KEY,
                user_id INT NOT NULL,
                total DECIMAL(10,2) NOT NULL,
                status VARCHAR(50) DEFAULT 'pending',
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (user_id) REFERENCES users(id)
            );
            
            CREATE TABLE order_items (
                id INT AUTO_INCREMENT PRIMARY KEY,
                order_id INT NOT NULL,
                product_name VARCHAR(255) NOT NULL,
                quantity INT NOT NULL,
                price DECIMAL(10,2) NOT NULL,
                FOREIGN KEY (order_id) REFERENCES orders(id)
            );
            
            CREATE INDEX idx_orders_user_id ON orders(user_id);
            CREATE INDEX idx_order_items_order_id ON order_items(order_id);
            CREATE UNIQUE INDEX idx_users_email ON users(email);
        ";
        command.ExecuteNonQuery();
    }

    private void CleanupDatabase()
    {
        using var command = _connection!.CreateCommand();
        command.CommandText = @"
            SET FOREIGN_KEY_CHECKS = 0;
            DROP TABLE IF EXISTS order_items;
            DROP TABLE IF EXISTS orders;
            DROP TABLE IF EXISTS users;
            SET FOREIGN_KEY_CHECKS = 1;
        ";
        command.ExecuteNonQuery();
    }

    [SkippableFact]
    public void CanQuerySchemaFromMySql()
    {
        Skip.IfNot(_canRun, "MySQL connection not available");

        // Arrange
        using var command = _connection!.CreateCommand();
        command.CommandText = @"
            SELECT table_name 
            FROM information_schema.tables 
            WHERE table_schema = DATABASE()
            ORDER BY table_name";

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
    public void CanQueryTableColumnsFromMySql()
    {
        Skip.IfNot(_canRun, "MySQL connection not available");

        // Arrange
        using var command = _connection!.CreateCommand();
        command.CommandText = @"
            SELECT column_name, data_type, is_nullable 
            FROM information_schema.columns 
            WHERE table_schema = DATABASE() AND table_name = 'users'
            ORDER BY ordinal_position";

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
        Assert.Contains(columns, c => c.name == "name" && c.type == "varchar" && !c.nullable);
    }

    [SkippableFact]
    public void CanQueryForeignKeysFromMySql()
    {
        Skip.IfNot(_canRun, "MySQL connection not available");

        // Arrange
        using var command = _connection!.CreateCommand();
        command.CommandText = @"
            SELECT
                constraint_name,
                table_name,
                column_name,
                referenced_table_name,
                referenced_column_name
            FROM information_schema.key_column_usage
            WHERE table_schema = DATABASE()
              AND referenced_table_name IS NOT NULL";

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
    public void CanQueryIndexesFromMySql()
    {
        Skip.IfNot(_canRun, "MySQL connection not available");

        // Arrange
        using var command = _connection!.CreateCommand();
        command.CommandText = @"
            SELECT DISTINCT index_name, table_name
            FROM information_schema.statistics
            WHERE table_schema = DATABASE()
              AND index_name != 'PRIMARY'";

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

using Npgsql;

namespace ObjMapper.IntegrationTests;

/// <summary>
/// Integration tests for PostgreSQL.
/// These tests require a running PostgreSQL instance.
/// Set POSTGRES_CONNECTION environment variable to run these tests.
/// </summary>
public class PostgresIntegrationTests : IDisposable
{
    private readonly NpgsqlConnection? _connection;
    private readonly string? _connectionString;
    private readonly bool _canRun;

    public PostgresIntegrationTests()
    {
        _connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION");
        _canRun = !string.IsNullOrEmpty(_connectionString);

        if (_canRun)
        {
            try
            {
                _connection = new NpgsqlConnection(_connectionString);
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
            DROP TABLE IF EXISTS order_items CASCADE;
            DROP TABLE IF EXISTS orders CASCADE;
            DROP TABLE IF EXISTS users CASCADE;
            
            CREATE TABLE users (
                id SERIAL PRIMARY KEY,
                name VARCHAR(100) NOT NULL,
                email VARCHAR(255),
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );
            
            CREATE TABLE orders (
                id SERIAL PRIMARY KEY,
                user_id INTEGER NOT NULL REFERENCES users(id),
                total DECIMAL(10,2) NOT NULL,
                status VARCHAR(50) DEFAULT 'pending',
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );
            
            CREATE TABLE order_items (
                id SERIAL PRIMARY KEY,
                order_id INTEGER NOT NULL REFERENCES orders(id),
                product_name VARCHAR(255) NOT NULL,
                quantity INTEGER NOT NULL,
                price DECIMAL(10,2) NOT NULL
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
            DROP TABLE IF EXISTS order_items CASCADE;
            DROP TABLE IF EXISTS orders CASCADE;
            DROP TABLE IF EXISTS users CASCADE;
        ";
        command.ExecuteNonQuery();
    }

    [SkippableFact]
    public void CanQuerySchemaFromPostgres()
    {
        Skip.IfNot(_canRun, "PostgreSQL connection not available");

        // Arrange
        using var command = _connection!.CreateCommand();
        command.CommandText = @"
            SELECT table_name 
            FROM information_schema.tables 
            WHERE table_schema = 'public' 
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
    public void CanQueryTableColumnsFromPostgres()
    {
        Skip.IfNot(_canRun, "PostgreSQL connection not available");

        // Arrange
        using var command = _connection!.CreateCommand();
        command.CommandText = @"
            SELECT column_name, data_type, is_nullable 
            FROM information_schema.columns 
            WHERE table_schema = 'public' AND table_name = 'users'
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
        Assert.Contains(columns, c => c.name == "id" && c.type == "integer");
        Assert.Contains(columns, c => c.name == "name" && c.type == "character varying" && !c.nullable);
        Assert.Contains(columns, c => c.name == "email" && c.nullable);
    }

    [SkippableFact]
    public void CanQueryForeignKeysFromPostgres()
    {
        Skip.IfNot(_canRun, "PostgreSQL connection not available");

        // Arrange
        using var command = _connection!.CreateCommand();
        command.CommandText = @"
            SELECT
                tc.constraint_name,
                tc.table_name,
                kcu.column_name,
                ccu.table_name AS foreign_table_name,
                ccu.column_name AS foreign_column_name
            FROM information_schema.table_constraints AS tc
            JOIN information_schema.key_column_usage AS kcu
              ON tc.constraint_name = kcu.constraint_name
              AND tc.table_schema = kcu.table_schema
            JOIN information_schema.constraint_column_usage AS ccu
              ON ccu.constraint_name = tc.constraint_name
              AND ccu.table_schema = tc.table_schema
            WHERE tc.constraint_type = 'FOREIGN KEY'
              AND tc.table_schema = 'public'";

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
    public void CanQueryIndexesFromPostgres()
    {
        Skip.IfNot(_canRun, "PostgreSQL connection not available");

        // Arrange
        using var command = _connection!.CreateCommand();
        command.CommandText = @"
            SELECT indexname, tablename
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND indexname NOT LIKE '%_pkey'";

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

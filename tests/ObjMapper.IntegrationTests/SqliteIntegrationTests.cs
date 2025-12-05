using Microsoft.Data.Sqlite;

namespace ObjMapper.IntegrationTests;

/// <summary>
/// Integration tests using SQLite (always available, no external dependencies).
/// </summary>
public class SqliteIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _tempDir;

    public SqliteIntegrationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _tempDir = Path.Combine(Path.GetTempPath(), $"omap_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        SetupDatabase();
    }

    public void Dispose()
    {
        _connection.Dispose();
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
        GC.SuppressFinalize(this);
    }

    private void SetupDatabase()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE users (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                email TEXT,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP
            );
            
            CREATE TABLE orders (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id INTEGER NOT NULL,
                total REAL NOT NULL,
                status TEXT DEFAULT 'pending',
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (user_id) REFERENCES users(id)
            );
            
            CREATE TABLE order_items (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                order_id INTEGER NOT NULL,
                product_name TEXT NOT NULL,
                quantity INTEGER NOT NULL,
                price REAL NOT NULL,
                FOREIGN KEY (order_id) REFERENCES orders(id)
            );
            
            CREATE INDEX idx_orders_user_id ON orders(user_id);
            CREATE INDEX idx_order_items_order_id ON order_items(order_id);
            CREATE UNIQUE INDEX idx_users_email ON users(email);
        ";
        command.ExecuteNonQuery();
    }

    [Fact]
    public void CanQuerySchemaFromSqlite()
    {
        // Arrange
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";

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

    [Fact]
    public void CanQueryTableColumnsFromSqlite()
    {
        // Arrange
        using var command = _connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(users)";

        // Act
        var columns = new List<(string name, string type, bool notNull)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                columns.Add((
                    reader.GetString(1),     // name
                    reader.GetString(2),     // type
                    reader.GetInt32(3) == 1  // notnull
                ));
            }
        }

        // Assert
        Assert.Equal(4, columns.Count);
        Assert.Contains(columns, c => c.name == "id" && c.type == "INTEGER");
        Assert.Contains(columns, c => c.name == "name" && c.type == "TEXT" && c.notNull);
        Assert.Contains(columns, c => c.name == "email" && c.type == "TEXT");
        Assert.Contains(columns, c => c.name == "created_at" && c.type == "DATETIME");
    }

    [Fact]
    public void CanQueryForeignKeysFromSqlite()
    {
        // Arrange
        using var command = _connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_list(orders)";

        // Act
        var foreignKeys = new List<(string table, string from, string to)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                foreignKeys.Add((
                    reader.GetString(2),  // table
                    reader.GetString(3),  // from
                    reader.GetString(4)   // to
                ));
            }
        }

        // Assert
        Assert.Single(foreignKeys);
        Assert.Contains(foreignKeys, fk => fk.table == "users" && fk.from == "user_id" && fk.to == "id");
    }

    [Fact]
    public void CanQueryIndexesFromSqlite()
    {
        // Arrange
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND tbl_name='users'";

        // Act
        var indexes = new List<string>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                if (!reader.IsDBNull(0))
                    indexes.Add(reader.GetString(0));
            }
        }

        // Assert
        Assert.Contains("idx_users_email", indexes);
    }

    [Fact]
    public void CanGenerateSchemaCSVFromSqlite()
    {
        // Arrange
        var schemaPath = Path.Combine(_tempDir, "schema.csv");
        var lines = new List<string> { "schema,table,column,nullable,type,comment" };

        // Query all tables
        using var tableCommand = _connection.CreateCommand();
        tableCommand.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
        var tables = new List<string>();
        using (var reader = tableCommand.ExecuteReader())
        {
            while (reader.Read())
                tables.Add(reader.GetString(0));
        }

        // Query columns for each table
        foreach (var table in tables)
        {
            using var colCommand = _connection.CreateCommand();
            colCommand.CommandText = $"PRAGMA table_info({table})";
            using var reader = colCommand.ExecuteReader();
            while (reader.Read())
            {
                var name = reader.GetString(1);
                var type = reader.GetString(2);
                var notNull = reader.GetInt32(3) == 1;
                lines.Add($"main,{table},{name},{!notNull},{type.ToLower()},");
            }
        }

        // Act
        File.WriteAllLines(schemaPath, lines);

        // Assert
        Assert.True(File.Exists(schemaPath));
        var content = File.ReadAllText(schemaPath);
        Assert.Contains("main,users,id", content);
        Assert.Contains("main,orders,user_id", content);
        Assert.Contains("main,order_items,product_name", content);
    }

    [Fact]
    public void CanGenerateRelationshipsCSVFromSqlite()
    {
        // Arrange
        var relPath = Path.Combine(_tempDir, "relationships.csv");
        var lines = new List<string> { "name,schema_from,schema_to,table_from,table_to,key,foreign" };

        // Query all tables
        using var tableCommand = _connection.CreateCommand();
        tableCommand.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
        var tables = new List<string>();
        using (var reader = tableCommand.ExecuteReader())
        {
            while (reader.Read())
                tables.Add(reader.GetString(0));
        }

        // Query foreign keys for each table
        foreach (var table in tables)
        {
            using var fkCommand = _connection.CreateCommand();
            fkCommand.CommandText = $"PRAGMA foreign_key_list({table})";
            using var reader = fkCommand.ExecuteReader();
            while (reader.Read())
            {
                var refTable = reader.GetString(2);
                var from = reader.GetString(3);
                var to = reader.GetString(4);
                lines.Add($"fk_{table}_{refTable},main,main,{table},{refTable},{to},{from}");
            }
        }

        // Act
        File.WriteAllLines(relPath, lines);

        // Assert
        Assert.True(File.Exists(relPath));
        var content = File.ReadAllText(relPath);
        Assert.Contains("fk_orders_users", content);
        Assert.Contains("fk_order_items_orders", content);
    }

    [Fact]
    public void CanGenerateIndexesCSVFromSqlite()
    {
        // Arrange
        var idxPath = Path.Combine(_tempDir, "indexes.csv");
        var lines = new List<string> { "schema,table,name,key,type" };

        // Query all indexes
        using var idxCommand = _connection.CreateCommand();
        idxCommand.CommandText = @"
            SELECT m.tbl_name, m.name, ii.name, ii.seqno,
                   (SELECT i.origin FROM pragma_index_list(m.tbl_name) i WHERE i.name = m.name) as idx_type
            FROM sqlite_master m
            JOIN pragma_index_info(m.name) ii
            WHERE m.type = 'index' 
              AND m.name NOT LIKE 'sqlite_%'
            ORDER BY m.name, ii.seqno
        ";

        var indexColumns = new Dictionary<string, (string table, List<string> columns, string type)>();
        using (var reader = idxCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                var table = reader.GetString(0);
                var indexName = reader.GetString(1);
                var columnName = reader.GetString(2);
                var type = reader.IsDBNull(4) ? "btree" : reader.GetString(4) == "u" ? "unique" : "btree";

                if (!indexColumns.ContainsKey(indexName))
                    indexColumns[indexName] = (table, new List<string>(), type);
                indexColumns[indexName].columns.Add(columnName);
            }
        }

        foreach (var (indexName, (table, columns, type)) in indexColumns)
        {
            lines.Add($"main,{table},{indexName},{string.Join(",", columns)},{type}");
        }

        // Act
        File.WriteAllLines(idxPath, lines);

        // Assert
        Assert.True(File.Exists(idxPath));
        var content = File.ReadAllText(idxPath);
        Assert.Contains("idx_users_email", content);
        Assert.Contains("idx_orders_user_id", content);
    }
}

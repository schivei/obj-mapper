using ObjMapper.Models;
using ObjMapper.Parsers;

namespace ObjMapper.Tests;

public class CsvSchemaParserTests
{
    [Fact]
    public void ParseSchemaFile_ReturnsCorrectColumns()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, """
            schema,table,column,nullable,type,comment
            public,users,id,false,int,Primary key
            public,users,name,false,varchar(100),User name
            public,users,email,true,varchar(255),Email address
            """);

        try
        {
            var parser = new CsvSchemaParser();

            // Act
            var columns = parser.ParseSchemaFile(tempFile);

            // Assert
            Assert.Equal(3, columns.Count);
            Assert.Equal("public", columns[0].Schema);
            Assert.Equal("users", columns[0].Table);
            Assert.Equal("id", columns[0].Column);
            Assert.False(columns[0].Nullable);
            Assert.Equal("int", columns[0].Type);
            Assert.Equal("Primary key", columns[0].Comment);

            Assert.True(columns[2].Nullable);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ParseRelationshipsFile_ReturnsCorrectRelationships()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, """
            name,schema_from,schema_to,table_from,table_to,key,foreign
            fk_orders_users,public,public,orders,users,id,user_id
            fk_order_items_orders,public,public,order_items,orders,id,order_id
            """);

        try
        {
            var parser = new CsvSchemaParser();

            // Act
            var relationships = parser.ParseRelationshipsFile(tempFile);

            // Assert
            Assert.Equal(2, relationships.Count);
            Assert.Equal("fk_orders_users", relationships[0].Name);
            Assert.Equal("public", relationships[0].SchemaFrom);
            Assert.Equal("public", relationships[0].SchemaTo);
            Assert.Equal("orders", relationships[0].TableFrom);
            Assert.Equal("users", relationships[0].TableTo);
            Assert.Equal("id", relationships[0].Key);
            Assert.Equal("user_id", relationships[0].Foreign);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ParseIndexesFile_ReturnsCorrectIndexes()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, """
            schema,table,name,key,type
            public,users,idx_users_email,email,unique
            public,orders,idx_orders_user_id,user_id,btree
            """);

        try
        {
            var parser = new CsvSchemaParser();

            // Act
            var indexes = parser.ParseIndexesFile(tempFile);

            // Assert
            Assert.Equal(2, indexes.Count);
            Assert.Equal("public", indexes[0].Schema);
            Assert.Equal("users", indexes[0].Table);
            Assert.Equal("idx_users_email", indexes[0].Name);
            Assert.Equal("email", indexes[0].Key);
            Assert.Equal("unique", indexes[0].Type);
            Assert.True(indexes[0].IsUnique);
            Assert.False(indexes[1].IsUnique);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void BuildSchema_GroupsColumnsIntoTables()
    {
        // Arrange
        var columns = new List<ColumnInfo>
        {
            new() { Schema = "public", Table = "users", Column = "id", Nullable = false, Type = "int" },
            new() { Schema = "public", Table = "users", Column = "name", Nullable = false, Type = "varchar(100)" },
            new() { Schema = "public", Table = "orders", Column = "id", Nullable = false, Type = "int" },
            new() { Schema = "public", Table = "orders", Column = "user_id", Nullable = false, Type = "int" }
        };

        var parser = new CsvSchemaParser();

        // Act
        var schema = parser.BuildSchema(columns, null, null);

        // Assert
        Assert.Equal(2, schema.Tables.Count);
        Assert.Contains(schema.Tables, t => t.Name == "users" && t.Columns.Count == 2);
        Assert.Contains(schema.Tables, t => t.Name == "orders" && t.Columns.Count == 2);
    }

    [Fact]
    public void BuildSchema_AssignsRelationshipsToTables()
    {
        // Arrange
        var columns = new List<ColumnInfo>
        {
            new() { Schema = "public", Table = "users", Column = "id", Nullable = false, Type = "int" },
            new() { Schema = "public", Table = "orders", Column = "id", Nullable = false, Type = "int" },
            new() { Schema = "public", Table = "orders", Column = "user_id", Nullable = false, Type = "int" }
        };

        var relationships = new List<RelationshipInfo>
        {
            new() { Name = "fk_orders_users", SchemaFrom = "public", SchemaTo = "public", TableFrom = "orders", TableTo = "users", Key = "id", Foreign = "user_id" }
        };

        var parser = new CsvSchemaParser();

        // Act
        var schema = parser.BuildSchema(columns, relationships, null);

        // Assert
        var usersTable = schema.Tables.First(t => t.Name == "users");
        var ordersTable = schema.Tables.First(t => t.Name == "orders");

        Assert.Single(usersTable.IncomingRelationships);
        Assert.Empty(usersTable.OutgoingRelationships);
        Assert.Single(ordersTable.OutgoingRelationships);
        Assert.Empty(ordersTable.IncomingRelationships);
    }

    [Fact]
    public void BuildSchema_AssignsIndexesToTables()
    {
        // Arrange
        var columns = new List<ColumnInfo>
        {
            new() { Schema = "public", Table = "users", Column = "id", Nullable = false, Type = "int" },
            new() { Schema = "public", Table = "users", Column = "email", Nullable = false, Type = "varchar(255)" }
        };

        var indexes = new List<IndexInfo>
        {
            new() { Schema = "public", Table = "users", Name = "idx_users_email", Key = "email", Type = "unique" }
        };

        var parser = new CsvSchemaParser();

        // Act
        var schema = parser.BuildSchema(columns, null, indexes);

        // Assert
        var usersTable = schema.Tables.First(t => t.Name == "users");

        Assert.Single(usersTable.Indexes);
        Assert.Equal("idx_users_email", usersTable.Indexes[0].Name);
    }
}

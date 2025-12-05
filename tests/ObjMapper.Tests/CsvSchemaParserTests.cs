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
            from,to,keys,foreignkeys
            orders,users,id,user_id
            order_items,orders,id,order_id
            """);

        try
        {
            var parser = new CsvSchemaParser();

            // Act
            var relationships = parser.ParseRelationshipsFile(tempFile);

            // Assert
            Assert.Equal(2, relationships.Count);
            Assert.Equal("orders", relationships[0].From);
            Assert.Equal("users", relationships[0].To);
            Assert.Equal("id", relationships[0].Keys);
            Assert.Equal("user_id", relationships[0].ForeignKeys);
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
        var schema = parser.BuildSchema(columns, null);

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
            new() { From = "orders", To = "users", Keys = "id", ForeignKeys = "user_id" }
        };

        var parser = new CsvSchemaParser();

        // Act
        var schema = parser.BuildSchema(columns, relationships);

        // Assert
        var usersTable = schema.Tables.First(t => t.Name == "users");
        var ordersTable = schema.Tables.First(t => t.Name == "orders");

        Assert.Single(usersTable.IncomingRelationships);
        Assert.Empty(usersTable.OutgoingRelationships);
        Assert.Single(ordersTable.OutgoingRelationships);
        Assert.Empty(ordersTable.IncomingRelationships);
    }
}

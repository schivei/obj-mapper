using ObjMapper.Models;
using ObjMapper.Services;

namespace ObjMapper.Tests;

public class TypeMapperTests
{
    [Theory]
    [InlineData("int", false, DatabaseType.PostgreSql, "int")]
    [InlineData("int", true, DatabaseType.PostgreSql, "int?")]
    [InlineData("bigint", false, DatabaseType.PostgreSql, "long")]
    [InlineData("varchar(100)", false, DatabaseType.PostgreSql, "string")]
    [InlineData("varchar(255)", true, DatabaseType.PostgreSql, "string")]
    [InlineData("text", false, DatabaseType.PostgreSql, "string")]
    [InlineData("boolean", false, DatabaseType.PostgreSql, "bool")]
    [InlineData("bool", true, DatabaseType.PostgreSql, "bool?")]
    [InlineData("decimal(10,2)", false, DatabaseType.PostgreSql, "decimal")]
    [InlineData("numeric", true, DatabaseType.PostgreSql, "decimal?")]
    [InlineData("timestamp", false, DatabaseType.PostgreSql, "DateTime")]
    [InlineData("date", false, DatabaseType.PostgreSql, "DateOnly")]
    [InlineData("time", false, DatabaseType.PostgreSql, "TimeOnly")]
    [InlineData("uuid", false, DatabaseType.PostgreSql, "Guid")]
    [InlineData("bytea", false, DatabaseType.PostgreSql, "byte[]")]
    [InlineData("json", false, DatabaseType.PostgreSql, "string")]
    [InlineData("float", false, DatabaseType.PostgreSql, "double")]
    [InlineData("real", false, DatabaseType.PostgreSql, "float")]
    public void MapToCSharpType_ReturnsCorrectType(string dbType, bool nullable, DatabaseType databaseType, string expected)
    {
        var mapper = new TypeMapper(databaseType);
        var result = mapper.MapToCSharpType(dbType, nullable);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("INT", false, DatabaseType.SqlServer, "int")]
    [InlineData("NVARCHAR(MAX)", false, DatabaseType.SqlServer, "string")]
    [InlineData("UNIQUEIDENTIFIER", false, DatabaseType.SqlServer, "Guid")]
    [InlineData("DATETIME2", false, DatabaseType.SqlServer, "DateTime")]
    [InlineData("DATETIMEOFFSET", false, DatabaseType.SqlServer, "DateTimeOffset")]
    [InlineData("BIT", false, DatabaseType.SqlServer, "bool")]
    [InlineData("VARBINARY", false, DatabaseType.SqlServer, "byte[]")]
    public void MapToCSharpType_SqlServer_ReturnsCorrectType(string dbType, bool nullable, DatabaseType databaseType, string expected)
    {
        var mapper = new TypeMapper(databaseType);
        var result = mapper.MapToCSharpType(dbType, nullable);
        Assert.Equal(expected, result);
    }
}

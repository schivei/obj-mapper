using ObjMapper.Models;
using ObjMapper.Generators;
using Xunit;

namespace ObjMapper.Tests;

/// <summary>
/// Tests for stored procedure extraction and code generation.
/// </summary>
public class StoredProcedureTests
{
    [Fact]
    public void StoredProcedureInfo_DefaultOutputType_IsNone()
    {
        var proc = new StoredProcedureInfo
        {
            Name = "TestProc",
            Schema = "dbo"
        };
        
        Assert.Equal(StoredProcedureOutputType.None, proc.OutputType);
    }
    
    [Fact]
    public void StoredProcedureInfo_FullName_WithSchema()
    {
        var proc = new StoredProcedureInfo
        {
            Name = "GetUsers",
            Schema = "dbo"
        };
        
        Assert.Equal("dbo.GetUsers", proc.FullName);
    }
    
    [Fact]
    public void StoredProcedureInfo_FullName_WithoutSchema()
    {
        var proc = new StoredProcedureInfo
        {
            Name = "GetUsers",
            Schema = ""
        };
        
        Assert.Equal("GetUsers", proc.FullName);
    }
    
    [Fact]
    public void StoredProcedureParameter_DefaultValues()
    {
        var param = new StoredProcedureParameter();
        
        Assert.Equal(string.Empty, param.Name);
        Assert.Equal(string.Empty, param.DataType);
        Assert.Equal(0, param.OrdinalPosition);
        Assert.False(param.IsOutput);
        Assert.False(param.HasDefault);
    }
    
    [Fact]
    public void StoredProcedureColumn_DefaultValues()
    {
        var col = new StoredProcedureColumn();
        
        Assert.Equal(string.Empty, col.Name);
        Assert.Equal(string.Empty, col.DataType);
        Assert.False(col.IsNullable);
        Assert.Equal(0, col.OrdinalPosition);
    }
    
    [Fact]
    public void EfCoreGenerator_GenerateStoredProcedures_EmptySchema_ReturnsEmpty()
    {
        var generator = new EfCoreGenerator(DatabaseType.SqlServer, "Test");
        var schema = new DatabaseSchema();
        
        var result = generator.GenerateStoredProcedures(schema);
        
        Assert.Empty(result);
    }
    
    [Fact]
    public void EfCoreGenerator_GenerateStoredProcedures_VoidProcedure()
    {
        var generator = new EfCoreGenerator(DatabaseType.SqlServer, "Test");
        var schema = new DatabaseSchema
        {
            StoredProcedures =
            [
                new StoredProcedureInfo
                {
                    Name = "CleanupData",
                    Schema = "dbo",
                    OutputType = StoredProcedureOutputType.None,
                    Parameters =
                    [
                        new StoredProcedureParameter { Name = "olderThan", DataType = "datetime", OrdinalPosition = 1 }
                    ]
                }
            ]
        };
        
        var result = generator.GenerateStoredProcedures(schema);
        
        Assert.Single(result);
        Assert.Contains("StoredProcedures.cs", result.Keys);
        Assert.Contains("CleanupData", result["StoredProcedures.cs"]);
        Assert.Contains("CleanupDataAsync", result["StoredProcedures.cs"]);
    }
    
    [Fact]
    public void EfCoreGenerator_GenerateStoredProcedures_ScalarProcedure()
    {
        var generator = new EfCoreGenerator(DatabaseType.SqlServer, "Test");
        var schema = new DatabaseSchema
        {
            StoredProcedures =
            [
                new StoredProcedureInfo
                {
                    Name = "GetUserCount",
                    Schema = "dbo",
                    OutputType = StoredProcedureOutputType.Scalar,
                    Parameters =
                    [
                        new StoredProcedureParameter { Name = "result", DataType = "int", OrdinalPosition = 1, IsOutput = true }
                    ]
                }
            ]
        };
        
        var result = generator.GenerateStoredProcedures(schema);
        
        Assert.Contains("StoredProcedures.cs", result.Keys);
        Assert.Contains("GetUserCount", result["StoredProcedures.cs"]);
        Assert.Contains("ParameterDirection.Output", result["StoredProcedures.cs"]);
    }
    
    [Fact]
    public void EfCoreGenerator_GenerateStoredProcedures_TabularProcedure()
    {
        var generator = new EfCoreGenerator(DatabaseType.SqlServer, "Test");
        var schema = new DatabaseSchema
        {
            StoredProcedures =
            [
                new StoredProcedureInfo
                {
                    Name = "GetOrders",
                    Schema = "dbo",
                    OutputType = StoredProcedureOutputType.Tabular,
                    ResultColumns =
                    [
                        new StoredProcedureColumn { Name = "OrderId", DataType = "int", OrdinalPosition = 1 },
                        new StoredProcedureColumn { Name = "OrderDate", DataType = "datetime", OrdinalPosition = 2 }
                    ]
                }
            ]
        };
        
        var result = generator.GenerateStoredProcedures(schema);
        
        Assert.Equal(2, result.Count);
        Assert.Contains("StoredProcedures.cs", result.Keys);
        Assert.Contains("StoredProcedureResultTypes.cs", result.Keys);
        Assert.Contains("GetOrdersResult", result["StoredProcedureResultTypes.cs"]);
        Assert.Contains("[Keyless]", result["StoredProcedureResultTypes.cs"]);
    }
    
    [Fact]
    public void DapperGenerator_GenerateStoredProcedures_EmptySchema_ReturnsEmpty()
    {
        var generator = new DapperGenerator(DatabaseType.SqlServer, "Test");
        var schema = new DatabaseSchema();
        
        var result = generator.GenerateStoredProcedures(schema);
        
        Assert.Empty(result);
    }
    
    [Fact]
    public void DapperGenerator_GenerateStoredProcedures_VoidProcedure()
    {
        var generator = new DapperGenerator(DatabaseType.SqlServer, "Test");
        var schema = new DatabaseSchema
        {
            StoredProcedures =
            [
                new StoredProcedureInfo
                {
                    Name = "DeleteOldRecords",
                    Schema = "dbo",
                    OutputType = StoredProcedureOutputType.None
                }
            ]
        };
        
        var result = generator.GenerateStoredProcedures(schema);
        
        Assert.Single(result);
        Assert.Contains("StoredProcedures.cs", result.Keys);
        Assert.Contains("connection.Execute", result["StoredProcedures.cs"]);
        Assert.Contains("CommandType.StoredProcedure", result["StoredProcedures.cs"]);
    }
    
    [Fact]
    public void DapperGenerator_GenerateStoredProcedures_TabularProcedure()
    {
        var generator = new DapperGenerator(DatabaseType.SqlServer, "Test");
        var schema = new DatabaseSchema
        {
            StoredProcedures =
            [
                new StoredProcedureInfo
                {
                    Name = "SearchUsers",
                    Schema = "dbo",
                    OutputType = StoredProcedureOutputType.Tabular,
                    Parameters =
                    [
                        new StoredProcedureParameter { Name = "searchTerm", DataType = "varchar", OrdinalPosition = 1 }
                    ],
                    ResultColumns =
                    [
                        new StoredProcedureColumn { Name = "UserId", DataType = "int", OrdinalPosition = 1 },
                        new StoredProcedureColumn { Name = "UserName", DataType = "varchar", OrdinalPosition = 2 }
                    ]
                }
            ]
        };
        
        var result = generator.GenerateStoredProcedures(schema);
        
        Assert.Equal(2, result.Count);
        Assert.Contains("connection.Query<SearchUsersResult>", result["StoredProcedures.cs"]);
        Assert.Contains("connection.QueryAsync<SearchUsersResult>", result["StoredProcedures.cs"]);
    }
    
    [Theory]
    [InlineData(DatabaseType.SqlServer, "SqlParameter")]
    [InlineData(DatabaseType.PostgreSql, "NpgsqlParameter")]
    [InlineData(DatabaseType.MySql, "MySqlParameter")]
    [InlineData(DatabaseType.Sqlite, "SqliteParameter")]
    public void EfCoreGenerator_GenerateStoredProcedures_UsesDatabaseSpecificParameters(DatabaseType dbType, string expectedParam)
    {
        var generator = new EfCoreGenerator(dbType, "Test");
        var schema = new DatabaseSchema
        {
            StoredProcedures =
            [
                new StoredProcedureInfo
                {
                    Name = "TestProc",
                    Schema = "dbo",
                    OutputType = StoredProcedureOutputType.Scalar,
                    Parameters =
                    [
                        new StoredProcedureParameter { Name = "output", DataType = "int", IsOutput = true, OrdinalPosition = 1 }
                    ]
                }
            ]
        };
        
        var result = generator.GenerateStoredProcedures(schema);
        
        Assert.Contains(expectedParam, result["StoredProcedures.cs"]);
    }
}

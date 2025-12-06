using ObjMapper.Generators;
using ObjMapper.Models;

namespace ObjMapper.Tests;

/// <summary>
/// Tests for scalar function code generation.
/// </summary>
public class ScalarFunctionTests
{
    private static DatabaseSchema CreateSchemaWithScalarFunctions()
    {
        return new DatabaseSchema
        {
            ScalarFunctions =
            [
                new ScalarFunctionInfo
                {
                    Schema = "dbo",
                    Name = "calculate_tax",
                    ReturnType = "decimal",
                    Parameters =
                    [
                        new ScalarFunctionParameter { Name = "amount", DataType = "decimal", OrdinalPosition = 1 },
                        new ScalarFunctionParameter { Name = "rate", DataType = "decimal", OrdinalPosition = 2 }
                    ]
                },
                new ScalarFunctionInfo
                {
                    Schema = "dbo",
                    Name = "get_full_name",
                    ReturnType = "varchar",
                    Parameters =
                    [
                        new ScalarFunctionParameter { Name = "first_name", DataType = "varchar", OrdinalPosition = 1 },
                        new ScalarFunctionParameter { Name = "last_name", DataType = "varchar", OrdinalPosition = 2 }
                    ]
                },
                new ScalarFunctionInfo
                {
                    Schema = "public",
                    Name = "get_current_timestamp",
                    ReturnType = "timestamp",
                    Parameters = []
                }
            ]
        };
    }

    [Fact]
    public void EfCoreGenerator_GeneratesScalarFunctionClass()
    {
        // Arrange
        var schema = CreateSchemaWithScalarFunctions();
        var generator = new EfCoreGenerator(DatabaseType.SqlServer, "TestNamespace");
        
        // Act
        var functions = generator.GenerateScalarFunctions(schema);
        
        // Assert
        Assert.Single(functions);
        Assert.Contains("DbFunctions.cs", functions.Keys);
    }
    
    [Fact]
    public void EfCoreGenerator_GeneratesDbFunctionAttribute()
    {
        // Arrange
        var schema = CreateSchemaWithScalarFunctions();
        var generator = new EfCoreGenerator(DatabaseType.SqlServer, "TestNamespace");
        
        // Act
        var functions = generator.GenerateScalarFunctions(schema);
        var code = functions["DbFunctions.cs"];
        
        // Assert
        Assert.Contains("[DbFunction(\"calculate_tax\", \"dbo\")]", code);
        Assert.Contains("[DbFunction(\"get_full_name\", \"dbo\")]", code);
    }
    
    [Fact]
    public void EfCoreGenerator_GeneratesCorrectMethodSignatures()
    {
        // Arrange
        var schema = CreateSchemaWithScalarFunctions();
        var generator = new EfCoreGenerator(DatabaseType.SqlServer, "TestNamespace");
        
        // Act
        var functions = generator.GenerateScalarFunctions(schema);
        var code = functions["DbFunctions.cs"];
        
        // Assert
        Assert.Contains("CalculateTax(decimal? amount, decimal? rate)", code);
        // Note: strings don't need ? annotation since they are reference types
        Assert.Contains("GetFullName(string firstName, string lastName)", code);
        Assert.Contains("GetCurrentTimestamp()", code);
    }
    
    [Fact]
    public void EfCoreGenerator_GeneratesNotSupportedException()
    {
        // Arrange
        var schema = CreateSchemaWithScalarFunctions();
        var generator = new EfCoreGenerator(DatabaseType.SqlServer, "TestNamespace");
        
        // Act
        var functions = generator.GenerateScalarFunctions(schema);
        var code = functions["DbFunctions.cs"];
        
        // Assert
        Assert.Contains("throw new NotSupportedException", code);
    }
    
    [Fact]
    public void DapperGenerator_GeneratesScalarFunctionClass()
    {
        // Arrange
        var schema = CreateSchemaWithScalarFunctions();
        var generator = new DapperGenerator(DatabaseType.SqlServer, "TestNamespace");
        
        // Act
        var functions = generator.GenerateScalarFunctions(schema);
        
        // Assert
        Assert.Single(functions);
        Assert.Contains("DbFunctions.cs", functions.Keys);
    }
    
    [Fact]
    public void DapperGenerator_GeneratesExecuteScalar()
    {
        // Arrange
        var schema = CreateSchemaWithScalarFunctions();
        var generator = new DapperGenerator(DatabaseType.SqlServer, "TestNamespace");
        
        // Act
        var functions = generator.GenerateScalarFunctions(schema);
        var code = functions["DbFunctions.cs"];
        
        // Assert
        Assert.Contains("ExecuteScalar<decimal?>", code);
        Assert.Contains("ExecuteScalarAsync<decimal?>", code);
    }
    
    [Fact]
    public void DapperGenerator_GeneratesCorrectSqlCall()
    {
        // Arrange
        var schema = CreateSchemaWithScalarFunctions();
        var generator = new DapperGenerator(DatabaseType.SqlServer, "TestNamespace");
        
        // Act
        var functions = generator.GenerateScalarFunctions(schema);
        var code = functions["DbFunctions.cs"];
        
        // Assert
        Assert.Contains("SELECT dbo.calculate_tax(@amount, @rate)", code);
        Assert.Contains("SELECT dbo.get_full_name(@firstName, @lastName)", code);
        Assert.Contains("SELECT public.get_current_timestamp()", code);
    }
    
    [Fact]
    public void DapperGenerator_GeneratesAsyncMethods()
    {
        // Arrange
        var schema = CreateSchemaWithScalarFunctions();
        var generator = new DapperGenerator(DatabaseType.SqlServer, "TestNamespace");
        
        // Act
        var functions = generator.GenerateScalarFunctions(schema);
        var code = functions["DbFunctions.cs"];
        
        // Assert
        Assert.Contains("async Task<decimal?>", code);
        Assert.Contains("CalculateTaxAsync", code);
        Assert.Contains("GetFullNameAsync", code);
    }
    
    [Fact]
    public void EfCoreGenerator_ReturnsEmptyDictionary_WhenNoFunctions()
    {
        // Arrange
        var schema = new DatabaseSchema { ScalarFunctions = [] };
        var generator = new EfCoreGenerator(DatabaseType.SqlServer, "TestNamespace");
        
        // Act
        var functions = generator.GenerateScalarFunctions(schema);
        
        // Assert
        Assert.Empty(functions);
    }
    
    [Fact]
    public void DapperGenerator_ReturnsEmptyDictionary_WhenNoFunctions()
    {
        // Arrange
        var schema = new DatabaseSchema { ScalarFunctions = [] };
        var generator = new DapperGenerator(DatabaseType.SqlServer, "TestNamespace");
        
        // Act
        var functions = generator.GenerateScalarFunctions(schema);
        
        // Assert
        Assert.Empty(functions);
    }
    
    [Fact]
    public void ScalarFunctionInfo_FullName_ReturnsSchemaAndName()
    {
        // Arrange
        var func = new ScalarFunctionInfo { Schema = "dbo", Name = "my_function" };
        
        // Act & Assert
        Assert.Equal("dbo.my_function", func.FullName);
    }
    
    [Fact]
    public void ScalarFunctionInfo_FullName_ReturnsOnlyName_WhenNoSchema()
    {
        // Arrange
        var func = new ScalarFunctionInfo { Schema = "", Name = "my_function" };
        
        // Act & Assert
        Assert.Equal("my_function", func.FullName);
    }
}

using ObjMapper.Models;
using ObjMapper.Services;
using ObjMapper.Services.TypeInference;

namespace ObjMapper.Tests;

/// <summary>
/// Tests for type inference functionality.
/// </summary>
public class TypeInferenceTests
{
    [Theory]
    [InlineData("is_active", "tinyint", true)]
    [InlineData("is_deleted", "smallint", true)]
    [InlineData("has_access", "int", true)]
    [InlineData("active", "tinyint", true)]
    [InlineData("enabled", "bit", true)]
    [InlineData("verified", "smallint", true)]
    public void TypeInferenceService_InfersBooleanByName(string columnName, string dbType, bool expected)
    {
        // Arrange
        var service = new TypeInferenceService();
        var typeMapper = new TypeMapper(DatabaseType.SqlServer);
        var column = new ColumnInfo
        {
            Column = columnName,
            Type = dbType,
            Nullable = false,
            Comment = ""
        };
        
        // Act
        var result = service.InferType(column, couldBeBoolean: false, typeMapper);
        
        // Assert
        if (expected)
        {
            Assert.Equal("bool", result);
        }
    }
    
    [Theory]
    [InlineData("uuid", "varchar(36)", "Guid")]
    [InlineData("guid", "char(36)", "Guid")]
    [InlineData("external_id", "varchar(36)", "Guid")]
    [InlineData("correlation_id", "char(36)", "Guid")]
    public void TypeInferenceService_InfersGuidByName(string columnName, string dbType, string expected)
    {
        // Arrange
        var service = new TypeInferenceService();
        var typeMapper = new TypeMapper(DatabaseType.SqlServer);
        var column = new ColumnInfo
        {
            Column = columnName,
            Type = dbType,
            Nullable = false,
            Comment = ""
        };
        
        // Act
        var result = service.InferType(column, couldBeBoolean: false, typeMapper);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public void TypeInferenceService_InfersGuidFromComment()
    {
        // Arrange
        var service = new TypeInferenceService();
        var typeMapper = new TypeMapper(DatabaseType.SqlServer);
        var column = new ColumnInfo
        {
            Column = "reference",
            Type = "varchar(36)",
            Nullable = false,
            Comment = "UUID format identifier"
        };
        
        // Act
        var result = service.InferType(column, couldBeBoolean: false, typeMapper);
        
        // Assert
        Assert.Equal("Guid", result);
    }
    
    [Fact]
    public void TypeInferenceService_InfersBooleanWhenCouldBeBoolean()
    {
        // Arrange
        var service = new TypeInferenceService();
        var typeMapper = new TypeMapper(DatabaseType.SqlServer);
        var column = new ColumnInfo
        {
            Column = "status_code",  // Not a typical boolean name
            Type = "tinyint",
            Nullable = false,
            Comment = ""
        };
        
        // Act - When data analysis says it could be boolean
        var result = service.InferType(column, couldBeBoolean: true, typeMapper);
        
        // Assert
        Assert.Equal("bool", result);
    }
    
    [Fact]
    public void TypeInferenceService_ReturnsNullableBoolean_WhenNullable()
    {
        // Arrange
        var service = new TypeInferenceService();
        var typeMapper = new TypeMapper(DatabaseType.SqlServer);
        var column = new ColumnInfo
        {
            Column = "is_active",
            Type = "tinyint",
            Nullable = true,
            Comment = ""
        };
        
        // Act
        var result = service.InferType(column, couldBeBoolean: false, typeMapper);
        
        // Assert
        Assert.Equal("bool?", result);
    }
    
    [Theory]
    [InlineData("created_at", "varchar", "DateTime")]
    [InlineData("updated_at", "text", "DateTime")]
    [InlineData("birth_date", "varchar(10)", "DateOnly")]
    [InlineData("start_time", "varchar", "TimeOnly")]
    public void TypeInferenceService_InfersDateTimeByName(string columnName, string dbType, string expected)
    {
        // Arrange
        var service = new TypeInferenceService();
        var typeMapper = new TypeMapper(DatabaseType.SqlServer);
        var column = new ColumnInfo
        {
            Column = columnName,
            Type = dbType,
            Nullable = false,
            Comment = ""
        };
        
        // Act
        var result = service.InferType(column, couldBeBoolean: false, typeMapper);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("tinyint", true)]
    [InlineData("smallint", true)]
    [InlineData("bit", true)]
    [InlineData("int2", true)]
    [InlineData("mediumint", true)]
    [InlineData("int", false)]  // int is not a small integer type
    [InlineData("bigint", false)]
    [InlineData("varchar", false)]
    public void BooleanColumnAnalyzer_IsSmallIntegerType(string dbType, bool expected)
    {
        // Act
        var result = BooleanColumnAnalyzer.IsSmallIntegerType(dbType);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public void TypeMapper_MapColumnToCSharpType_UsesBooleanInference()
    {
        // Arrange
        var typeMapper = new TypeMapper(DatabaseType.SqlServer);
        var column = new ColumnInfo
        {
            Column = "some_column",
            Type = "tinyint",
            Nullable = false,
            InferredAsBoolean = true
        };
        
        // Act
        var result = typeMapper.MapColumnToCSharpType(column);
        
        // Assert
        Assert.Equal("bool", result);
    }
    
    [Fact]
    public void TypeMapper_MapColumnToCSharpType_UsesNullableBooleanInference()
    {
        // Arrange
        var typeMapper = new TypeMapper(DatabaseType.SqlServer);
        var column = new ColumnInfo
        {
            Column = "some_column",
            Type = "tinyint",
            Nullable = true,
            InferredAsBoolean = true
        };
        
        // Act
        var result = typeMapper.MapColumnToCSharpType(column);
        
        // Assert
        Assert.Equal("bool?", result);
    }
    
    [Fact]
    public void TypeMapper_MapColumnToCSharpType_FallsBackToStandardMapping()
    {
        // Arrange
        var typeMapper = new TypeMapper(DatabaseType.SqlServer);
        var column = new ColumnInfo
        {
            Column = "some_column",
            Type = "int",
            Nullable = false,
            InferredAsBoolean = false
        };
        
        // Act
        var result = typeMapper.MapColumnToCSharpType(column);
        
        // Assert
        Assert.Equal("int", result);
    }
    
    [Fact]
    public void TypeMapper_WithTypeInference_UsesMLInference()
    {
        // Arrange
        var typeMapper = new TypeMapper(DatabaseType.SqlServer) { UseTypeInference = true };
        var column = new ColumnInfo
        {
            Column = "is_active",
            Type = "tinyint",
            Nullable = false,
            InferredAsBoolean = false  // Even without data analysis, name suggests boolean
        };
        
        // Act
        var result = typeMapper.MapColumnToCSharpType(column);
        
        // Assert
        Assert.Equal("bool", result);
    }
}

using ObjMapper.Models;

namespace ObjMapper.Tests;

/// <summary>
/// Tests for model classes.
/// </summary>
public class ModelTests
{
    [Fact]
    public void DatabaseSchema_DefaultValues_AreEmptyCollections()
    {
        // Arrange & Act
        var schema = new DatabaseSchema();
        
        // Assert
        Assert.Empty(schema.Tables);
        Assert.Empty(schema.Relationships);
        Assert.Empty(schema.Indexes);
        Assert.Empty(schema.ScalarFunctions);
    }
    
    [Fact]
    public void TableInfo_DefaultValues_AreEmptyCollections()
    {
        // Arrange & Act
        var table = new TableInfo();
        
        // Assert
        Assert.Empty(table.Columns);
        Assert.Empty(table.Indexes);
        Assert.Empty(table.OutgoingRelationships);
        Assert.Empty(table.IncomingRelationships);
        Assert.Equal(string.Empty, table.Schema);
        Assert.Equal(string.Empty, table.Name);
    }
    
    [Fact]
    public void ColumnInfo_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var column = new ColumnInfo();
        
        // Assert
        Assert.Equal(string.Empty, column.Schema);
        Assert.Equal(string.Empty, column.Table);
        Assert.Equal(string.Empty, column.Column);
        Assert.Equal(string.Empty, column.Type);
        Assert.Equal(string.Empty, column.Comment);
        Assert.False(column.Nullable);
    }
    
    [Fact]
    public void RelationshipInfo_Keys_SplitsCorrectly()
    {
        // Arrange
        var rel = new RelationshipInfo { Key = "id" };
        
        // Act
        var keys = rel.Keys;
        
        // Assert
        Assert.Single(keys);
        Assert.Equal("id", keys[0]);
    }
    
    [Fact]
    public void RelationshipInfo_ForeignKeys_SplitsCorrectly()
    {
        // Arrange
        var rel = new RelationshipInfo { Foreign = "user_id, order_id" };
        
        // Act
        var foreignKeys = rel.ForeignKeys;
        
        // Assert
        Assert.Equal(2, foreignKeys.Length);
        Assert.Equal("user_id", foreignKeys[0]);
        Assert.Equal("order_id", foreignKeys[1]);
    }
    
    [Fact]
    public void ScalarFunctionInfo_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var func = new ScalarFunctionInfo();
        
        // Assert
        Assert.Equal(string.Empty, func.Schema);
        Assert.Equal(string.Empty, func.Name);
        Assert.Equal(string.Empty, func.ReturnType);
        Assert.Empty(func.Parameters);
    }
    
    [Fact]
    public void ScalarFunctionParameter_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var param = new ScalarFunctionParameter();
        
        // Assert
        Assert.Equal(string.Empty, param.Name);
        Assert.Equal(string.Empty, param.DataType);
        Assert.Equal(0, param.OrdinalPosition);
    }
    
    [Fact]
    public void IndexInfo_Keys_TrimsWhitespace()
    {
        // Arrange
        var index = new IndexInfo { Key = " col1 , col2 , col3 " };
        
        // Act
        var keys = index.Keys;
        
        // Assert
        Assert.Equal(3, keys.Length);
        Assert.Equal("col1", keys[0]);
        Assert.Equal("col2", keys[1]);
        Assert.Equal("col3", keys[2]);
    }
    
    [Fact]
    public void IndexInfo_IsUnique_CaseInsensitive()
    {
        // Arrange
        var index1 = new IndexInfo { Type = "UNIQUE" };
        var index2 = new IndexInfo { Type = "Unique" };
        var index3 = new IndexInfo { Type = "unique_btree" };
        
        // Act & Assert
        Assert.True(index1.IsUnique);
        Assert.True(index2.IsUnique);
        Assert.True(index3.IsUnique);
    }
}

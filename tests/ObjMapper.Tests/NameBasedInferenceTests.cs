using ObjMapper.Models;
using Xunit;

namespace ObjMapper.Tests;

/// <summary>
/// Tests for name-based type inference patterns.
/// </summary>
public class NameBasedInferenceTests
{
    [Theory]
    [InlineData("is_active", "tinyint", true)]
    [InlineData("is_deleted", "smallint", true)]
    [InlineData("has_permission", "bit", true)]
    [InlineData("can_edit", "tinyint", true)]
    [InlineData("should_notify", "smallint", true)]
    [InlineData("allow_login", "bit", true)]
    [InlineData("enable_feature", "tinyint", true)]
    [InlineData("status_flag", "tinyint", true)]
    [InlineData("is_enabled", "tinyint", true)]
    [InlineData("user_active", "tinyint", true)]
    [InlineData("account_deleted", "smallint", true)]
    [InlineData("active", "tinyint", true)]
    [InlineData("enabled", "bit", true)]
    [InlineData("deleted", "smallint", true)]
    [InlineData("published", "tinyint", true)]
    [InlineData("verified", "bit", true)]
    public void IsBooleanNamePattern_DetectsPatterns(string columnName, string type, bool expected)
    {
        var result = IsBooleanNamePattern(columnName) && IsSmallIntegerType(type);
        
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("user_name", "varchar", false)]
    [InlineData("is_active", "varchar", false)] // Wrong type
    [InlineData("status", "int", false)] // Not small int
    [InlineData("count", "tinyint", false)] // Wrong name pattern
    [InlineData("order_id", "int", false)]
    public void IsBooleanNamePattern_RejectsNonBooleans(string columnName, string type, bool expected)
    {
        var result = IsBooleanNamePattern(columnName) && IsSmallIntegerType(type);
        
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("uuid", "char(36)", true)]
    [InlineData("guid", "varchar(36)", true)]
    [InlineData("correlation_id", "char(36)", true)]
    [InlineData("tracking_id", "varchar(36)", true)]
    [InlineData("external_id", "char(36)", true)]
    [InlineData("request_id", "varchar(36)", true)]
    [InlineData("session_id", "char(36)", true)]
    [InlineData("transaction_id", "varchar(36)", true)]
    [InlineData("user_uuid", "char(36)", true)]
    [InlineData("order_guid", "varchar(36)", true)]
    public void IsGuidNamePattern_DetectsPatterns(string columnName, string type, bool expected)
    {
        var result = IsGuidNamePattern(columnName) && IsGuidCompatibleType(type);
        
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("menuuid", "char(36)", false)] // Should NOT match - could be "menu id"
    [InlineData("continuum", "char(36)", false)] // Should NOT match - false positive
    [InlineData("uuid", "varchar(255)", false)] // Wrong type length
    [InlineData("user_id", "int", false)] // Not GUID type
    [InlineData("name", "varchar(36)", false)] // Wrong name pattern
    public void IsGuidNamePattern_RejectsFalsePositives(string columnName, string type, bool expected)
    {
        var result = IsGuidNamePattern(columnName) && IsGuidCompatibleType(type);
        
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("tinyint", true)]
    [InlineData("smallint", true)]
    [InlineData("bit", true)]
    [InlineData("boolean", true)]
    [InlineData("bool", true)]
    [InlineData("int2", true)]
    [InlineData("int", false)]
    [InlineData("bigint", false)]
    [InlineData("varchar", false)]
    [InlineData("decimal", false)]
    public void IsSmallIntegerType_CorrectlyIdentifiesTypes(string typeName, bool expected)
    {
        var result = IsSmallIntegerType(typeName);
        
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("char(36)", true)]
    [InlineData("varchar(36)", true)]
    [InlineData("character(36)", true)]
    [InlineData("uuid", true)]
    [InlineData("uniqueidentifier", true)]
    [InlineData("char(10)", false)]
    [InlineData("varchar(255)", false)]
    [InlineData("text", false)]
    [InlineData("int", false)]
    public void IsGuidCompatibleType_CorrectlyIdentifiesTypes(string typeName, bool expected)
    {
        var result = IsGuidCompatibleType(typeName);
        
        Assert.Equal(expected, result);
    }
    
    // Helper methods that mirror the logic in BaseSchemaExtractor
    private static bool IsBooleanNamePattern(string columnName)
    {
        var lowerName = columnName.ToLowerInvariant();
        return lowerName.StartsWith("is_") ||
               lowerName.StartsWith("has_") ||
               lowerName.StartsWith("can_") ||
               lowerName.StartsWith("should_") ||
               lowerName.StartsWith("will_") ||
               lowerName.StartsWith("allow_") ||
               lowerName.StartsWith("enable_") ||
               lowerName.StartsWith("disable_") ||
               lowerName.EndsWith("_flag") ||
               lowerName.EndsWith("_enabled") ||
               lowerName.EndsWith("_disabled") ||
               lowerName.EndsWith("_active") ||
               lowerName.EndsWith("_deleted") ||
               lowerName.EndsWith("_visible") ||
               lowerName.EndsWith("_hidden") ||
               lowerName.EndsWith("_required") ||
               lowerName == "active" ||
               lowerName == "enabled" ||
               lowerName == "disabled" ||
               lowerName == "deleted" ||
               lowerName == "visible" ||
               lowerName == "hidden" ||
               lowerName == "published" ||
               lowerName == "approved" ||
               lowerName == "verified" ||
               lowerName == "confirmed";
    }
    
    private static bool IsSmallIntegerType(string typeName)
    {
        var lowerType = typeName.ToLowerInvariant();
        return lowerType.Contains("tinyint") ||
               lowerType.Contains("smallint") ||
               lowerType.Contains("bit") ||
               lowerType.Contains("boolean") ||
               lowerType.Contains("bool") ||
               lowerType == "int2" ||
               lowerType == "int1";
    }
    
    private static bool IsGuidNamePattern(string columnName)
    {
        var lowerName = columnName.ToLowerInvariant();
        return lowerName == "uuid" ||
               lowerName == "guid" ||
               lowerName.EndsWith("_uuid") ||
               lowerName.EndsWith("_guid") ||
               lowerName == "correlation_id" ||
               lowerName == "tracking_id" ||
               lowerName == "external_id" ||
               lowerName == "request_id" ||
               lowerName == "session_id" ||
               lowerName == "transaction_id";
    }
    
    private static bool IsGuidCompatibleType(string typeName)
    {
        var lowerType = typeName.ToLowerInvariant();
        return lowerType.Contains("char(36)") ||
               lowerType.Contains("varchar(36)") ||
               lowerType.Contains("character(36)") ||
               lowerType == "uuid" ||
               lowerType == "uniqueidentifier";
    }
}

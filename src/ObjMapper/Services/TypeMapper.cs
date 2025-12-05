using ObjMapper.Models;

namespace ObjMapper.Services;

/// <summary>
/// Maps database column types to C# types.
/// </summary>
public class TypeMapper
{
    private readonly DatabaseType _databaseType;

    public TypeMapper(DatabaseType databaseType)
    {
        _databaseType = databaseType;
    }

    /// <summary>
    /// Maps a database type to a C# type.
    /// </summary>
    public string MapToCSharpType(string dbType, bool isNullable)
    {
        var baseType = ExtractBaseType(dbType);
        var csharpType = GetCSharpType(baseType);
        
        if (isNullable && IsValueType(csharpType))
        {
            return $"{csharpType}?";
        }
        
        return csharpType;
    }

    private string ExtractBaseType(string dbType)
    {
        // Remove size specifications like varchar(255), decimal(10,2)
        var parenIndex = dbType.IndexOf('(');
        return parenIndex > 0 ? dbType[..parenIndex].Trim() : dbType.Trim();
    }

    private string GetCSharpType(string dbType)
    {
        var normalizedType = dbType.ToUpperInvariant();

        return normalizedType switch
        {
            // Integer types
            "INT" or "INTEGER" or "INT4" => "int",
            "BIGINT" or "INT8" => "long",
            "SMALLINT" or "INT2" => "short",
            "TINYINT" => "byte",
            "MEDIUMINT" => "int",
            
            // Boolean
            "BIT" or "BOOLEAN" or "BOOL" => "bool",
            
            // Decimal/Numeric types
            "DECIMAL" or "NUMERIC" or "MONEY" or "SMALLMONEY" => "decimal",
            "FLOAT" or "FLOAT8" or "DOUBLE" or "DOUBLE PRECISION" => "double",
            "REAL" or "FLOAT4" => "float",
            
            // String types
            "VARCHAR" or "NVARCHAR" or "CHAR" or "NCHAR" or "TEXT" or "NTEXT" 
                or "LONGTEXT" or "MEDIUMTEXT" or "TINYTEXT" or "CHARACTER VARYING" => "string",
            "XML" => "string",
            "JSON" or "JSONB" => "string",
            
            // Date/Time types
            "DATE" => "DateOnly",
            "TIME" or "TIMETZ" or "TIME WITHOUT TIME ZONE" or "TIME WITH TIME ZONE" => "TimeOnly",
            "DATETIME" or "DATETIME2" or "SMALLDATETIME" or "TIMESTAMP" 
                or "TIMESTAMPTZ" or "TIMESTAMP WITHOUT TIME ZONE" or "TIMESTAMP WITH TIME ZONE" => "DateTime",
            "DATETIMEOFFSET" => "DateTimeOffset",
            
            // GUID
            "UNIQUEIDENTIFIER" or "UUID" or "GUID" => "Guid",
            
            // Binary types
            "BINARY" or "VARBINARY" or "IMAGE" or "BYTEA" or "BLOB" 
                or "LONGBLOB" or "MEDIUMBLOB" or "TINYBLOB" => "byte[]",
            
            // Special types
            "ROWVERSION" or "TIMESTAMP" when _databaseType == DatabaseType.SqlServer => "byte[]",
            "SERIAL" or "SERIAL4" => "int",
            "BIGSERIAL" or "SERIAL8" => "long",
            "SMALLSERIAL" or "SERIAL2" => "short",
            
            // Default to string for unknown types
            _ => "string"
        };
    }

    private static bool IsValueType(string csharpType)
    {
        return csharpType switch
        {
            "int" or "long" or "short" or "byte" or "bool" or "decimal" 
                or "double" or "float" or "DateTime" or "DateOnly" or "TimeOnly" 
                or "DateTimeOffset" or "Guid" => true,
            _ => false
        };
    }
}

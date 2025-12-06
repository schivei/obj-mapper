using ObjMapper.Models;
using ObjMapper.Services.TypeInference;

namespace ObjMapper.Services;

/// <summary>
/// Maps database column types to C# types.
/// </summary>
/// <param name="databaseType">The database type for special type mappings.</param>
public class TypeMapper(DatabaseType databaseType)
{
    private readonly DatabaseType _databaseType = databaseType;
    private TypeInferenceService? _typeInferenceService;
    
    /// <summary>
    /// Gets or sets whether to use ML-based type inference.
    /// </summary>
    public bool UseTypeInference { get; set; } = true; // Default to enabled
    
    /// <summary>
    /// Gets the type inference service (lazily initialized).
    /// </summary>
    private TypeInferenceService TypeInferenceService => 
        _typeInferenceService ??= new TypeInferenceService();

    /// <summary>
    /// Maps a database type to a C# type.
    /// </summary>
    public string MapToCSharpType(string dbType, bool isNullable)
    {
        var baseType = ExtractBaseType(dbType);
        var fullType = dbType.Trim().ToUpperInvariant();
        var csharpType = GetCSharpType(baseType, fullType);
        
        return isNullable && IsValueType(csharpType) ? $"{csharpType}?" : csharpType;
    }
    
    /// <summary>
    /// Maps a column to a C# type, optionally using ML-based type inference.
    /// </summary>
    public string MapColumnToCSharpType(ColumnInfo column)
    {
        // If column was inferred as boolean by data analysis
        if (column.InferredAsBoolean)
        {
            return column.Nullable ? "bool?" : "bool";
        }
        
        // If column was inferred as GUID by data analysis
        if (column.InferredAsGuid)
        {
            return column.Nullable ? "Guid?" : "Guid";
        }
        
        // Check for char(36) which should map to Guid
        if (IsChar36Type(column.Type))
        {
            return column.Nullable ? "Guid?" : "Guid";
        }
        
        // Use ML-based type inference if enabled
        if (UseTypeInference)
        {
            return TypeInferenceService.InferType(column, column.InferredAsBoolean, this);
        }
        
        // Fall back to standard mapping
        return MapToCSharpType(column.Type, column.Nullable);
    }
    
    /// <summary>
    /// Checks if a type is char(36) which should be mapped to Guid.
    /// </summary>
    private static bool IsChar36Type(string dbType)
    {
        var normalized = dbType.Trim().ToUpperInvariant().Replace(" ", "");
        return normalized is "CHAR(36)" or "NCHAR(36)" or "CHARACTER(36)";
    }

    private static string ExtractBaseType(string dbType)
    {
        // Remove size specifications like varchar(255), decimal(10,2)
        var parenIndex = dbType.IndexOf('(');
        return parenIndex > 0 ? dbType[..parenIndex].Trim() : dbType.Trim();
    }

    private string GetCSharpType(string baseType, string fullType)
    {
        var normalizedBase = baseType.ToUpperInvariant();
        var normalizedFull = fullType.Replace(" ", "");

        // Check for char(36) -> Guid
        if (normalizedFull is "CHAR(36)" or "NCHAR(36)" or "CHARACTER(36)")
        {
            return "Guid";
        }

        return normalizedBase switch
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
            "FLOAT" or "FLOAT8" or "DOUBLE" => "double",
            "REAL" or "FLOAT4" => "float",
            
            // String types
            "VARCHAR" or "NVARCHAR" or "CHAR" or "NCHAR" or "TEXT" or "NTEXT" 
                or "LONGTEXT" or "MEDIUMTEXT" or "TINYTEXT" => "string",
            "XML" => "string",
            "JSON" or "JSONB" => "string",
            
            // Date/Time types with timezone support
            "DATETIMEOFFSET" => "DateTimeOffset",
            "TIMESTAMPTZ" => "DateTimeOffset", // PostgreSQL timestamp with time zone
            
            // Date/Time types
            "DATE" => "DateOnly",
            "TIME" => GetTimeType(),
            "TIMETZ" => "TimeOnly", // PostgreSQL time with time zone (still TimeOnly, offset not preserved)
            "DATETIME" or "DATETIME2" or "SMALLDATETIME" => "DateTime",
            "TIMESTAMP" => GetTimestampType(),
            
            // GUID
            "UNIQUEIDENTIFIER" or "UUID" or "GUID" => "Guid",
            
            // Binary types
            "BINARY" or "VARBINARY" or "IMAGE" or "BYTEA" or "BLOB" 
                or "LONGBLOB" or "MEDIUMBLOB" or "TINYBLOB" => "byte[]",
            
            // Special types
            "ROWVERSION" when _databaseType == DatabaseType.SqlServer => "byte[]",
            "SERIAL" or "SERIAL4" => "int",
            "BIGSERIAL" or "SERIAL8" => "long",
            "SMALLSERIAL" or "SERIAL2" => "short",
            
            // Handle "CHARACTER VARYING" as a phrase
            "CHARACTER" when fullType.Contains("VARYING") => "string",
            
            // Handle timestamp variants with time zone
            _ when normalizedBase == "TIMESTAMP" && fullType.Contains("TIMEZONE") => "DateTimeOffset",
            _ when normalizedBase == "TIME" && fullType.Contains("TIMEZONE") => "TimeOnly",
            
            // Default to string for unknown types
            _ => "string"
        };
    }
    
    /// <summary>
    /// Gets the appropriate time type based on database.
    /// </summary>
    private string GetTimeType() => "TimeOnly";
    
    /// <summary>
    /// Gets the appropriate timestamp type based on database.
    /// For SQL Server, TIMESTAMP is actually ROWVERSION (binary).
    /// For other databases, it's DateTime.
    /// </summary>
    private string GetTimestampType() =>
        _databaseType == DatabaseType.SqlServer ? "byte[]" : "DateTime";

    private static bool IsValueType(string csharpType) =>
        csharpType is "int" or "long" or "short" or "byte" or "bool" or "decimal" 
            or "double" or "float" or "DateTime" or "DateOnly" or "TimeOnly" 
            or "DateTimeOffset" or "Guid";
}

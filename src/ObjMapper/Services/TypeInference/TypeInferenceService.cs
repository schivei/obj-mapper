using Microsoft.ML;
using Microsoft.ML.Data;
using ObjMapper.Models;

namespace ObjMapper.Services.TypeInference;

/// <summary>
/// ML-based type inference service that analyzes column names, types, and comments
/// to infer the best C# type mapping.
/// </summary>
public class TypeInferenceService
{
    private readonly MLContext _mlContext;
    private readonly ITransformer? _model;
    private readonly PredictionEngine<ColumnFeatures, TypePrediction>? _predictionEngine;
    
    // Training data based on common patterns
    private static readonly List<ColumnFeatures> TrainingData =
    [
        // Boolean patterns
        new() { ColumnName = "is_active", DbType = "tinyint", Comment = "", InferredType = "bool" },
        new() { ColumnName = "is_deleted", DbType = "bit", Comment = "", InferredType = "bool" },
        new() { ColumnName = "is_enabled", DbType = "smallint", Comment = "", InferredType = "bool" },
        new() { ColumnName = "has_access", DbType = "int", Comment = "", InferredType = "bool" },
        new() { ColumnName = "active", DbType = "tinyint", Comment = "active flag", InferredType = "bool" },
        new() { ColumnName = "enabled", DbType = "smallint", Comment = "", InferredType = "bool" },
        new() { ColumnName = "deleted", DbType = "bit", Comment = "", InferredType = "bool" },
        new() { ColumnName = "verified", DbType = "tinyint", Comment = "", InferredType = "bool" },
        new() { ColumnName = "confirmed", DbType = "smallint", Comment = "", InferredType = "bool" },
        new() { ColumnName = "flag", DbType = "tinyint", Comment = "", InferredType = "bool" },
        new() { ColumnName = "status_flag", DbType = "int", Comment = "boolean flag", InferredType = "bool" },
        new() { ColumnName = "can_edit", DbType = "tinyint", Comment = "", InferredType = "bool" },
        new() { ColumnName = "allow_access", DbType = "smallint", Comment = "", InferredType = "bool" },
        
        // GUID patterns
        new() { ColumnName = "uuid", DbType = "varchar", Comment = "", InferredType = "Guid" },
        new() { ColumnName = "guid", DbType = "char(36)", Comment = "", InferredType = "Guid" },
        new() { ColumnName = "external_id", DbType = "varchar(36)", Comment = "UUID", InferredType = "Guid" },
        new() { ColumnName = "correlation_id", DbType = "char(36)", Comment = "", InferredType = "Guid" },
        new() { ColumnName = "tracking_id", DbType = "varchar(36)", Comment = "GUID format", InferredType = "Guid" },
        
        // Date patterns
        new() { ColumnName = "created_at", DbType = "varchar", Comment = "", InferredType = "DateTime" },
        new() { ColumnName = "updated_at", DbType = "text", Comment = "timestamp", InferredType = "DateTime" },
        new() { ColumnName = "birth_date", DbType = "varchar(10)", Comment = "", InferredType = "DateOnly" },
        new() { ColumnName = "hire_date", DbType = "varchar(10)", Comment = "date only", InferredType = "DateOnly" },
        new() { ColumnName = "start_time", DbType = "varchar", Comment = "", InferredType = "TimeOnly" },
        new() { ColumnName = "end_time", DbType = "text", Comment = "time of day", InferredType = "TimeOnly" },
        
        // Email patterns
        new() { ColumnName = "email", DbType = "varchar", Comment = "", InferredType = "string" },
        new() { ColumnName = "email_address", DbType = "text", Comment = "", InferredType = "string" },
        
        // JSON patterns
        new() { ColumnName = "metadata", DbType = "text", Comment = "JSON data", InferredType = "string" },
        new() { ColumnName = "settings", DbType = "varchar", Comment = "JSON", InferredType = "string" },
        new() { ColumnName = "config", DbType = "text", Comment = "json configuration", InferredType = "string" },
        new() { ColumnName = "properties", DbType = "longtext", Comment = "", InferredType = "string" },
        
        // Numeric patterns - keep as numeric
        new() { ColumnName = "count", DbType = "int", Comment = "", InferredType = "int" },
        new() { ColumnName = "quantity", DbType = "smallint", Comment = "", InferredType = "short" },
        new() { ColumnName = "amount", DbType = "decimal", Comment = "", InferredType = "decimal" },
        new() { ColumnName = "price", DbType = "decimal", Comment = "", InferredType = "decimal" },
        new() { ColumnName = "total", DbType = "decimal", Comment = "", InferredType = "decimal" },
        new() { ColumnName = "age", DbType = "tinyint", Comment = "", InferredType = "byte" },
        new() { ColumnName = "level", DbType = "smallint", Comment = "", InferredType = "short" },
        new() { ColumnName = "priority", DbType = "tinyint", Comment = "priority level", InferredType = "byte" },
        new() { ColumnName = "order", DbType = "int", Comment = "sort order", InferredType = "int" },
        new() { ColumnName = "sort_order", DbType = "smallint", Comment = "", InferredType = "short" },
        new() { ColumnName = "sequence", DbType = "int", Comment = "", InferredType = "int" },
        new() { ColumnName = "year", DbType = "smallint", Comment = "", InferredType = "short" },
        new() { ColumnName = "month", DbType = "tinyint", Comment = "", InferredType = "byte" },
        new() { ColumnName = "day", DbType = "tinyint", Comment = "", InferredType = "byte" },
        
        // ID patterns
        new() { ColumnName = "id", DbType = "int", Comment = "", InferredType = "int" },
        new() { ColumnName = "user_id", DbType = "bigint", Comment = "", InferredType = "long" },
        new() { ColumnName = "order_id", DbType = "int", Comment = "", InferredType = "int" },
        
        // Binary patterns
        new() { ColumnName = "data", DbType = "blob", Comment = "", InferredType = "byte[]" },
        new() { ColumnName = "image", DbType = "longblob", Comment = "", InferredType = "byte[]" },
        new() { ColumnName = "file_content", DbType = "varbinary", Comment = "", InferredType = "byte[]" },
        new() { ColumnName = "avatar", DbType = "blob", Comment = "image data", InferredType = "byte[]" },
        
        // Enum-like patterns (kept as int/string depending on context)
        new() { ColumnName = "status", DbType = "tinyint", Comment = "", InferredType = "byte" },
        new() { ColumnName = "type", DbType = "smallint", Comment = "", InferredType = "short" },
        new() { ColumnName = "category", DbType = "int", Comment = "", InferredType = "int" },
    ];
    
    public TypeInferenceService()
    {
        _mlContext = new MLContext(seed: 42);
        
        try
        {
            _model = TrainModel();
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<ColumnFeatures, TypePrediction>(_model);
        }
        catch
        {
            // If ML training fails, we'll fall back to rule-based inference
            _model = null;
            _predictionEngine = null;
        }
    }
    
    private ITransformer TrainModel()
    {
        var dataView = _mlContext.Data.LoadFromEnumerable(TrainingData);
        
        var pipeline = _mlContext.Transforms.Text.FeaturizeText("ColumnNameFeatures", nameof(ColumnFeatures.ColumnName))
            .Append(_mlContext.Transforms.Text.FeaturizeText("DbTypeFeatures", nameof(ColumnFeatures.DbType)))
            .Append(_mlContext.Transforms.Text.FeaturizeText("CommentFeatures", nameof(ColumnFeatures.Comment)))
            .Append(_mlContext.Transforms.Concatenate("Features", "ColumnNameFeatures", "DbTypeFeatures", "CommentFeatures"))
            .Append(_mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(ColumnFeatures.InferredType)))
            .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
            .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));
        
        return pipeline.Fit(dataView);
    }
    
    /// <summary>
    /// Infers the best C# type for a column based on its metadata.
    /// </summary>
    public string InferType(ColumnInfo column, bool couldBeBoolean, TypeMapper typeMapper)
    {
        // If data analysis determined it could be boolean
        if (couldBeBoolean && IsSmallIntegerType(column.Type))
        {
            return column.Nullable ? "bool?" : "bool";
        }
        
        // Try ML-based inference
        if (_predictionEngine is not null)
        {
            var prediction = _predictionEngine.Predict(new ColumnFeatures
            {
                ColumnName = column.Column.ToLowerInvariant(),
                DbType = column.Type.ToLowerInvariant(),
                Comment = column.Comment?.ToLowerInvariant() ?? ""
            });
            
            // If ML is confident enough, use its prediction
            if (prediction.Score?.Max() > 0.7f)
            {
                var predictedType = prediction.PredictedLabel;
                if (!string.IsNullOrEmpty(predictedType))
                {
                    return AdjustForNullability(predictedType, column.Nullable);
                }
            }
        }
        
        // Fall back to rule-based inference
        var ruleBasedType = InferTypeByRules(column);
        if (ruleBasedType is not null)
        {
            return AdjustForNullability(ruleBasedType, column.Nullable);
        }
        
        // Fall back to standard type mapping
        return typeMapper.MapToCSharpType(column.Type, column.Nullable);
    }
    
    private static string? InferTypeByRules(ColumnInfo column)
    {
        var name = column.Column.ToLowerInvariant();
        var type = column.Type.ToLowerInvariant();
        var comment = column.Comment?.ToLowerInvariant() ?? "";
        
        // Boolean patterns by name
        if (IsBooleanColumnName(name) && IsSmallIntegerType(type))
        {
            return "bool";
        }
        
        // GUID patterns
        if (IsGuidColumnName(name) && IsStringType(type))
        {
            return "Guid";
        }
        
        // UUID in comment
        if ((comment.Contains("uuid") || comment.Contains("guid")) && IsStringType(type))
        {
            return "Guid";
        }
        
        // JSON in comment
        if (comment.Contains("json") && IsStringType(type))
        {
            return "string"; // Keep as string but could be JsonDocument
        }
        
        // DateTime patterns for string columns
        if (IsDateTimeColumnName(name) && IsStringType(type))
        {
            if (name.Contains("date") && !name.Contains("time") && !name.Contains("datetime"))
            {
                return "DateOnly";
            }
            if (name.Contains("time") && !name.Contains("date"))
            {
                return "TimeOnly";
            }
            return "DateTime";
        }
        
        return null;
    }
    
    private static bool IsBooleanColumnName(string name) =>
        name.StartsWith("is_") || name.StartsWith("has_") || name.StartsWith("can_") ||
        name.StartsWith("allow_") || name.StartsWith("should_") || name.StartsWith("will_") ||
        name.EndsWith("_flag") || name == "active" || name == "enabled" || name == "deleted" ||
        name == "verified" || name == "confirmed" || name == "published" || name == "visible" ||
        name == "locked" || name == "archived" || name == "approved";
    
    private static bool IsGuidColumnName(string name) =>
        name == "uuid" || name == "guid" || name.EndsWith("_uuid") || name.EndsWith("_guid") ||
        name == "correlation_id" || name == "tracking_id" || name == "external_id" ||
        name == "reference_id" || name == "transaction_id";
    
    private static bool IsDateTimeColumnName(string name) =>
        name.Contains("date") || name.Contains("time") || name.Contains("_at") ||
        name.EndsWith("_on") || name == "created" || name == "updated" || name == "modified" ||
        name == "timestamp" || name == "expires" || name == "scheduled";
    
    private static bool IsSmallIntegerType(string type)
    {
        var normalizedType = type.ToUpperInvariant();
        // Only small integer types - exclude INT which is a 32-bit integer
        return normalizedType.StartsWith("TINYINT") || normalizedType.StartsWith("SMALLINT") ||
               normalizedType.StartsWith("BIT") || normalizedType == "INT2" ||
               normalizedType.StartsWith("MEDIUMINT") || normalizedType == "BYTE";
    }
    
    private static bool IsStringType(string type)
    {
        var normalizedType = type.ToUpperInvariant();
        return normalizedType.StartsWith("VARCHAR") || normalizedType.StartsWith("NVARCHAR") ||
               normalizedType.StartsWith("CHAR") || normalizedType.StartsWith("NCHAR") ||
               normalizedType == "TEXT" || normalizedType == "NTEXT" ||
               normalizedType == "LONGTEXT" || normalizedType == "MEDIUMTEXT" ||
               normalizedType == "TINYTEXT" || normalizedType.StartsWith("CHARACTER");
    }
    
    private static string AdjustForNullability(string type, bool isNullable)
    {
        if (!isNullable)
            return type;
        
        // Value types need nullable annotation
        return type switch
        {
            "bool" or "int" or "long" or "short" or "byte" or "decimal" or "double" or "float" 
                or "DateTime" or "DateOnly" or "TimeOnly" or "DateTimeOffset" or "Guid" => $"{type}?",
            _ => type
        };
    }
    
    /// <summary>
    /// ML training features for a column.
    /// </summary>
    private class ColumnFeatures
    {
        public string ColumnName { get; set; } = string.Empty;
        public string DbType { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string InferredType { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// ML prediction output.
    /// </summary>
    private class TypePrediction
    {
        [ColumnName("PredictedLabel")]
        public string PredictedLabel { get; set; } = string.Empty;
        
        [ColumnName("Score")]
        public float[]? Score { get; set; }
    }
}

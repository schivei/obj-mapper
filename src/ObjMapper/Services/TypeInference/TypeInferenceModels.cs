namespace ObjMapper.Services.TypeInference;

/// <summary>
/// Input data for type inference ML model.
/// </summary>
public class TypeInferenceInput
{
    /// <summary>
    /// The column name (used for pattern matching).
    /// </summary>
    public string ColumnName { get; set; } = string.Empty;
    
    /// <summary>
    /// The database type as reported by the database.
    /// </summary>
    public string DatabaseType { get; set; } = string.Empty;
    
    /// <summary>
    /// The column comment/description.
    /// </summary>
    public string Comment { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether the column is nullable.
    /// </summary>
    public bool IsNullable { get; set; }
    
    /// <summary>
    /// Whether the column could be boolean (based on data analysis).
    /// </summary>
    public bool CouldBeBoolean { get; set; }
}

/// <summary>
/// Output/prediction from type inference ML model.
/// </summary>
public class TypeInferencePrediction
{
    /// <summary>
    /// The predicted C# type.
    /// </summary>
    public string PredictedType { get; set; } = string.Empty;
    
    /// <summary>
    /// Confidence score of the prediction.
    /// </summary>
    public float Score { get; set; }
}

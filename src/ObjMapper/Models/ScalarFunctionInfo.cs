namespace ObjMapper.Models;

/// <summary>
/// Represents a scalar user-defined function from the database.
/// </summary>
public class ScalarFunctionInfo
{
    /// <summary>
    /// Schema of the function.
    /// </summary>
    public string Schema { get; set; } = string.Empty;
    
    /// <summary>
    /// Name of the function.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Return type of the function (database-specific type).
    /// </summary>
    public string ReturnType { get; set; } = string.Empty;
    
    /// <summary>
    /// List of parameters for the function.
    /// </summary>
    public List<ScalarFunctionParameter> Parameters { get; set; } = [];
    
    /// <summary>
    /// Gets the full name of the function (schema.name or just name if no schema).
    /// </summary>
    public string FullName => string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";
}

/// <summary>
/// Represents a parameter for a scalar user-defined function.
/// </summary>
public class ScalarFunctionParameter
{
    /// <summary>
    /// Name of the parameter.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Data type of the parameter (database-specific type).
    /// </summary>
    public string DataType { get; set; } = string.Empty;
    
    /// <summary>
    /// Ordinal position of the parameter.
    /// </summary>
    public int OrdinalPosition { get; set; }
}

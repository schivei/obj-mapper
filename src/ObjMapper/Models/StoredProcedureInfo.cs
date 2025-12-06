namespace ObjMapper.Models;

/// <summary>
/// Represents the type of output a stored procedure produces.
/// </summary>
public enum StoredProcedureOutputType
{
    /// <summary>
    /// The stored procedure does not return any output.
    /// </summary>
    None,
    
    /// <summary>
    /// The stored procedure returns a scalar value.
    /// </summary>
    Scalar,
    
    /// <summary>
    /// The stored procedure returns a table/result set.
    /// </summary>
    Tabular
}

/// <summary>
/// Represents a stored procedure from the database.
/// </summary>
public class StoredProcedureInfo
{
    /// <summary>
    /// Schema of the stored procedure.
    /// </summary>
    public string Schema { get; set; } = string.Empty;
    
    /// <summary>
    /// Name of the stored procedure.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The type of output this stored procedure produces.
    /// </summary>
    public StoredProcedureOutputType OutputType { get; set; } = StoredProcedureOutputType.None;
    
    /// <summary>
    /// Return type when OutputType is Scalar (database-specific type).
    /// </summary>
    public string? ScalarReturnType { get; set; }
    
    /// <summary>
    /// Result columns when OutputType is Tabular.
    /// </summary>
    public List<StoredProcedureColumn> ResultColumns { get; set; } = [];
    
    /// <summary>
    /// List of parameters for the stored procedure.
    /// </summary>
    public List<StoredProcedureParameter> Parameters { get; set; } = [];
    
    /// <summary>
    /// Gets the full name of the stored procedure (schema.name or just name if no schema).
    /// </summary>
    public string FullName => string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";
}

/// <summary>
/// Represents a parameter for a stored procedure.
/// </summary>
public class StoredProcedureParameter
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
    
    /// <summary>
    /// Whether this parameter is an output parameter.
    /// </summary>
    public bool IsOutput { get; set; }
    
    /// <summary>
    /// Whether this parameter has a default value.
    /// </summary>
    public bool HasDefault { get; set; }
}

/// <summary>
/// Represents a column in a stored procedure's tabular result.
/// </summary>
public class StoredProcedureColumn
{
    /// <summary>
    /// Name of the column.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Data type of the column (database-specific type).
    /// </summary>
    public string DataType { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this column is nullable.
    /// </summary>
    public bool IsNullable { get; set; }
    
    /// <summary>
    /// Ordinal position of the column in the result set.
    /// </summary>
    public int OrdinalPosition { get; set; }
}

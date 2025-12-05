namespace ObjMapper.Models;

/// <summary>
/// Specifies the type of C# construct to use for generating entities.
/// </summary>
public enum EntityTypeMode
{
    /// <summary>
    /// Generate entities as classes (default).
    /// </summary>
    Class,
    
    /// <summary>
    /// Generate entities as records.
    /// </summary>
    Record,
    
    /// <summary>
    /// Generate entities as structs.
    /// </summary>
    Struct,
    
    /// <summary>
    /// Generate entities as record structs.
    /// </summary>
    RecordStruct
}

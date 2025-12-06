using System.Text;

namespace ObjMapper.Generators.Converters;

/// <summary>
/// Generates EF Core value converters for types not natively supported by database drivers.
/// </summary>
public static class EfCoreConverterGenerator
{
    /// <summary>
    /// Generates the DateOnly value converter class.
    /// </summary>
    public static string GenerateDateOnlyConverter(string namespaceName)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("using Microsoft.EntityFrameworkCore.Storage.ValueConversion;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName}.Converters;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Converts DateOnly to DateTime for database drivers that don't support DateOnly.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class DateOnlyConverter : ValueConverter<DateOnly, DateTime>");
        sb.AppendLine("{");
        sb.AppendLine("    public DateOnlyConverter() : base(");
        sb.AppendLine("        dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue),");
        sb.AppendLine("        dateTime => DateOnly.FromDateTime(dateTime))");
        sb.AppendLine("    { }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Converts nullable DateOnly to DateTime for database drivers that don't support DateOnly.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class NullableDateOnlyConverter : ValueConverter<DateOnly?, DateTime?>");
        sb.AppendLine("{");
        sb.AppendLine("    public NullableDateOnlyConverter() : base(");
        sb.AppendLine("        dateOnly => dateOnly.HasValue ? dateOnly.Value.ToDateTime(TimeOnly.MinValue) : null,");
        sb.AppendLine("        dateTime => dateTime.HasValue ? DateOnly.FromDateTime(dateTime.Value) : null)");
        sb.AppendLine("    { }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Generates the TimeOnly value converter class.
    /// </summary>
    public static string GenerateTimeOnlyConverter(string namespaceName)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("using Microsoft.EntityFrameworkCore.Storage.ValueConversion;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName}.Converters;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Converts TimeOnly to TimeSpan for database drivers that don't support TimeOnly.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class TimeOnlyConverter : ValueConverter<TimeOnly, TimeSpan>");
        sb.AppendLine("{");
        sb.AppendLine("    public TimeOnlyConverter() : base(");
        sb.AppendLine("        timeOnly => timeOnly.ToTimeSpan(),");
        sb.AppendLine("        timeSpan => TimeOnly.FromTimeSpan(timeSpan))");
        sb.AppendLine("    { }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Converts nullable TimeOnly to TimeSpan for database drivers that don't support TimeOnly.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class NullableTimeOnlyConverter : ValueConverter<TimeOnly?, TimeSpan?>");
        sb.AppendLine("{");
        sb.AppendLine("    public NullableTimeOnlyConverter() : base(");
        sb.AppendLine("        timeOnly => timeOnly.HasValue ? timeOnly.Value.ToTimeSpan() : null,");
        sb.AppendLine("        timeSpan => timeSpan.HasValue ? TimeOnly.FromTimeSpan(timeSpan.Value) : null)");
        sb.AppendLine("    { }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Generates the DateTimeOffset value converter class for databases that don't support it natively.
    /// </summary>
    public static string GenerateDateTimeOffsetConverter(string namespaceName)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("using Microsoft.EntityFrameworkCore.Storage.ValueConversion;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName}.Converters;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Converts DateTimeOffset to DateTime for database drivers that don't support DateTimeOffset.");
        sb.AppendLine("/// Note: This converter stores the UTC time and loses the original offset information.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class DateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTime>");
        sb.AppendLine("{");
        sb.AppendLine("    public DateTimeOffsetConverter() : base(");
        sb.AppendLine("        dto => dto.UtcDateTime,");
        sb.AppendLine("        dt => new DateTimeOffset(dt, TimeSpan.Zero))");
        sb.AppendLine("    { }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Converts nullable DateTimeOffset to DateTime for database drivers that don't support DateTimeOffset.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class NullableDateTimeOffsetConverter : ValueConverter<DateTimeOffset?, DateTime?>");
        sb.AppendLine("{");
        sb.AppendLine("    public NullableDateTimeOffsetConverter() : base(");
        sb.AppendLine("        dto => dto.HasValue ? dto.Value.UtcDateTime : null,");
        sb.AppendLine("        dt => dt.HasValue ? new DateTimeOffset(dt.Value, TimeSpan.Zero) : null)");
        sb.AppendLine("    { }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Generates code to register converters in OnConfiguring or OnModelCreating.
    /// </summary>
    public static string GenerateConverterRegistration(bool hasDateOnly, bool hasTimeOnly, bool hasDateTimeOffset, bool needsDateTimeOffsetConverter)
    {
        var sb = new StringBuilder();
        
        if (hasDateOnly || hasTimeOnly || (hasDateTimeOffset && needsDateTimeOffsetConverter))
        {
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Configures value converters for types not natively supported by all database drivers.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    private static void ConfigureConverters(ModelBuilder modelBuilder)");
            sb.AppendLine("    {");
            sb.AppendLine("        foreach (var entityType in modelBuilder.Model.GetEntityTypes())");
            sb.AppendLine("        {");
            sb.AppendLine("            foreach (var property in entityType.GetProperties())");
            sb.AppendLine("            {");
            
            if (hasDateOnly)
            {
                sb.AppendLine("                if (property.ClrType == typeof(DateOnly))");
                sb.AppendLine("                    property.SetValueConverter(new Converters.DateOnlyConverter());");
                sb.AppendLine("                else if (property.ClrType == typeof(DateOnly?))");
                sb.AppendLine("                    property.SetValueConverter(new Converters.NullableDateOnlyConverter());");
            }
            
            if (hasTimeOnly)
            {
                sb.AppendLine("                else if (property.ClrType == typeof(TimeOnly))");
                sb.AppendLine("                    property.SetValueConverter(new Converters.TimeOnlyConverter());");
                sb.AppendLine("                else if (property.ClrType == typeof(TimeOnly?))");
                sb.AppendLine("                    property.SetValueConverter(new Converters.NullableTimeOnlyConverter());");
            }
            
            if (hasDateTimeOffset && needsDateTimeOffsetConverter)
            {
                sb.AppendLine("                else if (property.ClrType == typeof(DateTimeOffset))");
                sb.AppendLine("                    property.SetValueConverter(new Converters.DateTimeOffsetConverter());");
                sb.AppendLine("                else if (property.ClrType == typeof(DateTimeOffset?))");
                sb.AppendLine("                    property.SetValueConverter(new Converters.NullableDateTimeOffsetConverter());");
            }
            
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
        }
        
        return sb.ToString();
    }
}

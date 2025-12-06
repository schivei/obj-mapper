using System.Text;

namespace ObjMapper.Generators.Converters;

/// <summary>
/// Generates Dapper type handlers for types not natively supported by database drivers.
/// </summary>
public static class DapperConverterGenerator
{
    /// <summary>
    /// Generates all type handler classes for Dapper.
    /// </summary>
    public static string GenerateTypeHandlers(string namespaceName, bool hasDateOnly, bool hasTimeOnly, bool hasDateTimeOffset, bool needsDateTimeOffsetHandler)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("using System.Data;");
        sb.AppendLine("using Dapper;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName}.TypeHandlers;");
        sb.AppendLine();
        
        if (hasDateOnly)
        {
            GenerateDateOnlyHandler(sb);
            sb.AppendLine();
        }
        
        if (hasTimeOnly)
        {
            GenerateTimeOnlyHandler(sb);
            sb.AppendLine();
        }
        
        if (hasDateTimeOffset && needsDateTimeOffsetHandler)
        {
            GenerateDateTimeOffsetHandler(sb);
            sb.AppendLine();
        }
        
        // Generate registration helper
        GenerateRegistrationHelper(sb, hasDateOnly, hasTimeOnly, hasDateTimeOffset && needsDateTimeOffsetHandler);
        
        return sb.ToString();
    }
    
    private static void GenerateDateOnlyHandler(StringBuilder sb)
    {
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Dapper type handler for DateOnly.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>");
        sb.AppendLine("{");
        sb.AppendLine("    public override DateOnly Parse(object value)");
        sb.AppendLine("    {");
        sb.AppendLine("        return value switch");
        sb.AppendLine("        {");
        sb.AppendLine("            DateTime dt => DateOnly.FromDateTime(dt),");
        sb.AppendLine("            DateOnly d => d,");
        sb.AppendLine("            string s => DateOnly.Parse(s),");
        sb.AppendLine("            _ => throw new InvalidCastException($\"Cannot convert {value.GetType()} to DateOnly\")");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override void SetValue(IDbDataParameter parameter, DateOnly value)");
        sb.AppendLine("    {");
        sb.AppendLine("        parameter.DbType = DbType.Date;");
        sb.AppendLine("        parameter.Value = value.ToDateTime(TimeOnly.MinValue);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Dapper type handler for nullable DateOnly.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class NullableDateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly?>");
        sb.AppendLine("{");
        sb.AppendLine("    public override DateOnly? Parse(object value)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (value is null or DBNull) return null;");
        sb.AppendLine("        return value switch");
        sb.AppendLine("        {");
        sb.AppendLine("            DateTime dt => DateOnly.FromDateTime(dt),");
        sb.AppendLine("            DateOnly d => d,");
        sb.AppendLine("            string s => DateOnly.Parse(s),");
        sb.AppendLine("            _ => throw new InvalidCastException($\"Cannot convert {value.GetType()} to DateOnly\")");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override void SetValue(IDbDataParameter parameter, DateOnly? value)");
        sb.AppendLine("    {");
        sb.AppendLine("        parameter.DbType = DbType.Date;");
        sb.AppendLine("        parameter.Value = value.HasValue ? value.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }
    
    private static void GenerateTimeOnlyHandler(StringBuilder sb)
    {
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Dapper type handler for TimeOnly.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class TimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly>");
        sb.AppendLine("{");
        sb.AppendLine("    public override TimeOnly Parse(object value)");
        sb.AppendLine("    {");
        sb.AppendLine("        return value switch");
        sb.AppendLine("        {");
        sb.AppendLine("            TimeSpan ts => TimeOnly.FromTimeSpan(ts),");
        sb.AppendLine("            TimeOnly t => t,");
        sb.AppendLine("            DateTime dt => TimeOnly.FromDateTime(dt),");
        sb.AppendLine("            string s => TimeOnly.Parse(s),");
        sb.AppendLine("            _ => throw new InvalidCastException($\"Cannot convert {value.GetType()} to TimeOnly\")");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override void SetValue(IDbDataParameter parameter, TimeOnly value)");
        sb.AppendLine("    {");
        sb.AppendLine("        parameter.DbType = DbType.Time;");
        sb.AppendLine("        parameter.Value = value.ToTimeSpan();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Dapper type handler for nullable TimeOnly.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class NullableTimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly?>");
        sb.AppendLine("{");
        sb.AppendLine("    public override TimeOnly? Parse(object value)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (value is null or DBNull) return null;");
        sb.AppendLine("        return value switch");
        sb.AppendLine("        {");
        sb.AppendLine("            TimeSpan ts => TimeOnly.FromTimeSpan(ts),");
        sb.AppendLine("            TimeOnly t => t,");
        sb.AppendLine("            DateTime dt => TimeOnly.FromDateTime(dt),");
        sb.AppendLine("            string s => TimeOnly.Parse(s),");
        sb.AppendLine("            _ => throw new InvalidCastException($\"Cannot convert {value.GetType()} to TimeOnly\")");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override void SetValue(IDbDataParameter parameter, TimeOnly? value)");
        sb.AppendLine("    {");
        sb.AppendLine("        parameter.DbType = DbType.Time;");
        sb.AppendLine("        parameter.Value = value.HasValue ? value.Value.ToTimeSpan() : DBNull.Value;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }
    
    private static void GenerateDateTimeOffsetHandler(StringBuilder sb)
    {
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Dapper type handler for DateTimeOffset for databases that don't support it natively.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class DateTimeOffsetTypeHandler : SqlMapper.TypeHandler<DateTimeOffset>");
        sb.AppendLine("{");
        sb.AppendLine("    public override DateTimeOffset Parse(object value)");
        sb.AppendLine("    {");
        sb.AppendLine("        return value switch");
        sb.AppendLine("        {");
        sb.AppendLine("            DateTimeOffset dto => dto,");
        sb.AppendLine("            DateTime dt => new DateTimeOffset(dt, TimeSpan.Zero),");
        sb.AppendLine("            string s => DateTimeOffset.Parse(s),");
        sb.AppendLine("            _ => throw new InvalidCastException($\"Cannot convert {value.GetType()} to DateTimeOffset\")");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)");
        sb.AppendLine("    {");
        sb.AppendLine("        parameter.DbType = DbType.DateTime;");
        sb.AppendLine("        parameter.Value = value.UtcDateTime;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Dapper type handler for nullable DateTimeOffset for databases that don't support it natively.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class NullableDateTimeOffsetTypeHandler : SqlMapper.TypeHandler<DateTimeOffset?>");
        sb.AppendLine("{");
        sb.AppendLine("    public override DateTimeOffset? Parse(object value)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (value is null or DBNull) return null;");
        sb.AppendLine("        return value switch");
        sb.AppendLine("        {");
        sb.AppendLine("            DateTimeOffset dto => dto,");
        sb.AppendLine("            DateTime dt => new DateTimeOffset(dt, TimeSpan.Zero),");
        sb.AppendLine("            string s => DateTimeOffset.Parse(s),");
        sb.AppendLine("            _ => throw new InvalidCastException($\"Cannot convert {value.GetType()} to DateTimeOffset\")");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override void SetValue(IDbDataParameter parameter, DateTimeOffset? value)");
        sb.AppendLine("    {");
        sb.AppendLine("        parameter.DbType = DbType.DateTime;");
        sb.AppendLine("        parameter.Value = value.HasValue ? value.Value.UtcDateTime : DBNull.Value;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }
    
    private static void GenerateRegistrationHelper(StringBuilder sb, bool hasDateOnly, bool hasTimeOnly, bool hasDateTimeOffset)
    {
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Helper class to register all type handlers.");
        sb.AppendLine("/// Call RegisterAll() at application startup.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class TypeHandlerRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    private static bool _registered;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Registers all custom type handlers with Dapper.");
        sb.AppendLine("    /// This method is safe to call multiple times.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static void RegisterAll()");
        sb.AppendLine("    {");
        sb.AppendLine("        if (_registered) return;");
        sb.AppendLine();
        
        if (hasDateOnly)
        {
            sb.AppendLine("        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());");
            sb.AppendLine("        SqlMapper.AddTypeHandler(new NullableDateOnlyTypeHandler());");
        }
        
        if (hasTimeOnly)
        {
            sb.AppendLine("        SqlMapper.AddTypeHandler(new TimeOnlyTypeHandler());");
            sb.AppendLine("        SqlMapper.AddTypeHandler(new NullableTimeOnlyTypeHandler());");
        }
        
        if (hasDateTimeOffset)
        {
            sb.AppendLine("        SqlMapper.AddTypeHandler(new DateTimeOffsetTypeHandler());");
            sb.AppendLine("        SqlMapper.AddTypeHandler(new NullableDateTimeOffsetTypeHandler());");
        }
        
        sb.AppendLine();
        sb.AppendLine("        _registered = true;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }
}

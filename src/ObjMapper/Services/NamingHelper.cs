using System.Text;
using System.Text.RegularExpressions;

namespace ObjMapper.Services;

/// <summary>
/// Utility methods for name conversions.
/// </summary>
public static partial class NamingHelper
{
    private static PluralizerService _pluralizer = new("en-us", false);

    /// <summary>
    /// Configures the pluralizer with the specified locale and disabled state.
    /// </summary>
    public static void Configure(string locale, bool disabled)
    {
        _pluralizer = new PluralizerService(locale, disabled);
    }

    /// <summary>
    /// Converts a database name to PascalCase class name.
    /// </summary>
    public static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var words = SplitWords(name);
        var sb = new StringBuilder();
        
        foreach (var word in words)
        {
            if (word.Length > 0)
            {
                sb.Append(char.ToUpperInvariant(word[0]));
                if (word.Length > 1)
                {
                    sb.Append(word[1..].ToLowerInvariant());
                }
            }
        }

        var result = sb.ToString();
        
        // Ensure it's a valid C# identifier
        if (result.Length > 0 && char.IsDigit(result[0]))
        {
            result = "_" + result;
        }

        return result;
    }

    /// <summary>
    /// Converts a database name to camelCase.
    /// </summary>
    public static string ToCamelCase(string name)
    {
        var pascalCase = ToPascalCase(name);
        if (string.IsNullOrEmpty(pascalCase))
            return pascalCase;

        return char.ToLowerInvariant(pascalCase[0]) + pascalCase[1..];
    }

    /// <summary>
    /// Converts table name to entity name (singular form).
    /// </summary>
    public static string ToEntityName(string tableName)
    {
        var pascalCase = ToPascalCase(tableName);
        return _pluralizer.Singularize(pascalCase);
    }

    /// <summary>
    /// Converts entity name to collection property name (plural form).
    /// </summary>
    public static string ToCollectionName(string entityName)
    {
        return _pluralizer.Pluralize(entityName);
    }

    private static string[] SplitWords(string name)
    {
        // Replace underscores and hyphens with spaces
        var withSpaces = name.Replace('_', ' ').Replace('-', ' ');
        
        // Insert space before uppercase letters in camelCase
        var withCamelSpaces = CamelCaseRegex().Replace(withSpaces, "$1 $2");
        
        // Split by spaces and filter empty entries
        return withCamelSpaces.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    [GeneratedRegex(@"([a-z])([A-Z])")]
    private static partial Regex CamelCaseRegex();
}

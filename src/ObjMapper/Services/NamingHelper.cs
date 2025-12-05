using System.Text;
using System.Text.RegularExpressions;

namespace ObjMapper.Services;

/// <summary>
/// Utility methods for name conversions.
/// </summary>
public static partial class NamingHelper
{
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
        return Singularize(pascalCase);
    }

    /// <summary>
    /// Converts entity name to collection property name (plural form).
    /// </summary>
    public static string ToCollectionName(string entityName)
    {
        return Pluralize(entityName);
    }

    /// <summary>
    /// Simple singularization logic.
    /// </summary>
    private static string Singularize(string word)
    {
        if (string.IsNullOrEmpty(word) || word.Length < 2)
            return word;

        // Handle common plural endings
        if (word.EndsWith("ies", StringComparison.OrdinalIgnoreCase))
            return word[..^3] + "y";
        
        if (word.EndsWith("es", StringComparison.OrdinalIgnoreCase) && 
            (word.EndsWith("sses", StringComparison.OrdinalIgnoreCase) ||
             word.EndsWith("xes", StringComparison.OrdinalIgnoreCase) ||
             word.EndsWith("ches", StringComparison.OrdinalIgnoreCase) ||
             word.EndsWith("shes", StringComparison.OrdinalIgnoreCase)))
            return word[..^2];
        
        if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase) && 
            !word.EndsWith("ss", StringComparison.OrdinalIgnoreCase))
            return word[..^1];

        return word;
    }

    /// <summary>
    /// Simple pluralization logic.
    /// </summary>
    private static string Pluralize(string word)
    {
        if (string.IsNullOrEmpty(word))
            return word;

        if (word.EndsWith("y", StringComparison.OrdinalIgnoreCase) && 
            !EndsWithVowelPlusY(word))
            return word[..^1] + "ies";
        
        if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("x", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("sh", StringComparison.OrdinalIgnoreCase))
            return word + "es";

        return word + "s";
    }

    private static bool EndsWithVowelPlusY(string word)
    {
        if (word.Length < 2)
            return false;
        
        var secondToLast = char.ToLowerInvariant(word[^2]);
        return secondToLast is 'a' or 'e' or 'i' or 'o' or 'u';
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

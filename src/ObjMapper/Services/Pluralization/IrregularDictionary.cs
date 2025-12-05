namespace ObjMapper.Services.Pluralization;

/// <summary>
/// Base class for irregular word dictionaries.
/// </summary>
public abstract class IrregularDictionary
{
    /// <summary>
    /// Dictionary mapping singular forms to plural forms.
    /// </summary>
    protected abstract Dictionary<string, string> SingularToPlural { get; }

    /// <summary>
    /// Dictionary mapping plural forms to singular forms (auto-generated from SingularToPlural).
    /// </summary>
    protected Dictionary<string, string>? _pluralToSingular;

    protected Dictionary<string, string> PluralToSingular
    {
        get
        {
            _pluralToSingular ??= SingularToPlural
                .ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);
            return _pluralToSingular;
        }
    }

    /// <summary>
    /// Tries to get the plural form of an irregular word.
    /// </summary>
    public bool TryGetPlural(string singular, out string? plural)
    {
        return SingularToPlural.TryGetValue(singular, out plural);
    }

    /// <summary>
    /// Tries to get the singular form of an irregular word.
    /// </summary>
    public bool TryGetSingular(string plural, out string? singular)
    {
        return PluralToSingular.TryGetValue(plural, out singular);
    }
}

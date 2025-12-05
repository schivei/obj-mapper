namespace ObjMapper.Services;

/// <summary>
/// Service for pluralization and singularization in multiple languages.
/// </summary>
public class PluralizerService
{
    private readonly string _locale;
    private readonly bool _disabled;

    /// <summary>
    /// List of supported locales.
    /// </summary>
    public static readonly string[] SupportedLocales =
    [
        "en-us", "en-gb", "en",       // English
        "pt-br", "pt-pt", "pt",       // Portuguese
        "es-es", "es-mx", "es",       // Spanish
        "fr-fr", "fr-ca", "fr",       // French
        "de-de", "de",                // German
        "it-it", "it",                // Italian
        "nl-nl", "nl",                // Dutch
        "ru-ru", "ru",                // Russian
        "pl-pl", "pl",                // Polish
        "tr-tr", "tr",                // Turkish
        "ja-jp", "ja",                // Japanese
        "ko-kr", "ko",                // Korean
        "zh-cn", "zh-tw", "zh"        // Chinese
    ];

    public PluralizerService(string locale = "en-us", bool disabled = false)
    {
        _locale = NormalizeLocale(locale);
        _disabled = disabled;
    }

    /// <summary>
    /// Normalizes the locale string to lowercase with hyphen.
    /// </summary>
    private static string NormalizeLocale(string locale)
    {
        return locale.ToLowerInvariant().Replace('_', '-');
    }

    /// <summary>
    /// Gets the language code from the locale (e.g., "en" from "en-us").
    /// </summary>
    private string GetLanguageCode()
    {
        var hyphenIndex = _locale.IndexOf('-');
        return hyphenIndex > 0 ? _locale[..hyphenIndex] : _locale;
    }

    /// <summary>
    /// Converts a word to its singular form.
    /// </summary>
    public string Singularize(string word)
    {
        if (_disabled || string.IsNullOrEmpty(word) || word.Length < 2)
            return word;

        return GetLanguageCode() switch
        {
            "en" => SingularizeEnglish(word),
            "pt" => SingularizePortuguese(word),
            "es" => SingularizeSpanish(word),
            "fr" => SingularizeFrench(word),
            "de" => SingularizeGerman(word),
            "it" => SingularizeItalian(word),
            "nl" => SingularizeDutch(word),
            "ru" => SingularizeRussian(word),
            "pl" => SingularizePolish(word),
            "tr" => SingularizeTurkish(word),
            "ja" => word, // Japanese doesn't typically inflect for plural
            "ko" => word, // Korean doesn't typically inflect for plural
            "zh" => word, // Chinese doesn't typically inflect for plural
            _ => SingularizeEnglish(word)
        };
    }

    /// <summary>
    /// Converts a word to its plural form.
    /// </summary>
    public string Pluralize(string word)
    {
        if (_disabled || string.IsNullOrEmpty(word))
            return word;

        return GetLanguageCode() switch
        {
            "en" => PluralizeEnglish(word),
            "pt" => PluralizePortuguese(word),
            "es" => PluralizeSpanish(word),
            "fr" => PluralizeFrench(word),
            "de" => PluralizeGerman(word),
            "it" => PluralizeItalian(word),
            "nl" => PluralizeDutch(word),
            "ru" => PluralizeRussian(word),
            "pl" => PluralizePolish(word),
            "tr" => PluralizeTurkish(word),
            "ja" => word, // Japanese doesn't typically inflect for plural
            "ko" => word, // Korean doesn't typically inflect for plural
            "zh" => word, // Chinese doesn't typically inflect for plural
            _ => PluralizeEnglish(word)
        };
    }

    #region English

    private static string SingularizeEnglish(string word)
    {
        // Handle irregular plurals
        var irregulars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "people", "person" }, { "men", "man" }, { "women", "woman" },
            { "children", "child" }, { "teeth", "tooth" }, { "feet", "foot" },
            { "mice", "mouse" }, { "geese", "goose" }, { "oxen", "ox" },
            { "data", "datum" }, { "criteria", "criterion" }, { "phenomena", "phenomenon" }
        };

        if (irregulars.TryGetValue(word, out var singular))
            return PreserveCase(word, singular);

        // Handle common plural endings
        if (word.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && word.Length > 3)
            return word[..^3] + "y";

        if (word.EndsWith("ves", StringComparison.OrdinalIgnoreCase))
            return word[..^3] + "f";

        if (word.EndsWith("es", StringComparison.OrdinalIgnoreCase) &&
            (word.EndsWith("sses", StringComparison.OrdinalIgnoreCase) ||
             word.EndsWith("xes", StringComparison.OrdinalIgnoreCase) ||
             word.EndsWith("ches", StringComparison.OrdinalIgnoreCase) ||
             word.EndsWith("shes", StringComparison.OrdinalIgnoreCase) ||
             word.EndsWith("oes", StringComparison.OrdinalIgnoreCase)))
            return word[..^2];

        if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
            !word.EndsWith("ss", StringComparison.OrdinalIgnoreCase) &&
            !word.EndsWith("us", StringComparison.OrdinalIgnoreCase))
            return word[..^1];

        return word;
    }

    private static string PluralizeEnglish(string word)
    {
        // Handle irregular plurals
        var irregulars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "person", "people" }, { "man", "men" }, { "woman", "women" },
            { "child", "children" }, { "tooth", "teeth" }, { "foot", "feet" },
            { "mouse", "mice" }, { "goose", "geese" }, { "ox", "oxen" },
            { "datum", "data" }, { "criterion", "criteria" }, { "phenomenon", "phenomena" }
        };

        if (irregulars.TryGetValue(word, out var plural))
            return PreserveCase(word, plural);

        // Handle words ending in consonant + y
        if (word.EndsWith("y", StringComparison.OrdinalIgnoreCase) && !EndsWithVowelPlusY(word))
            return word[..^1] + "ies";

        // Handle words ending in f/fe
        if (word.EndsWith("f", StringComparison.OrdinalIgnoreCase))
            return word[..^1] + "ves";
        if (word.EndsWith("fe", StringComparison.OrdinalIgnoreCase))
            return word[..^2] + "ves";

        // Handle words ending in s, x, ch, sh, o
        if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("x", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("sh", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("o", StringComparison.OrdinalIgnoreCase))
            return word + "es";

        return word + "s";
    }

    #endregion

    #region Portuguese

    private static string SingularizePortuguese(string word)
    {
        // Handle plural endings
        if (word.EndsWith("ões", StringComparison.OrdinalIgnoreCase))
            return word[..^3] + "ão";

        if (word.EndsWith("ães", StringComparison.OrdinalIgnoreCase))
            return word[..^3] + "ão";

        if (word.EndsWith("ais", StringComparison.OrdinalIgnoreCase))
            return word[..^2] + "l";

        if (word.EndsWith("éis", StringComparison.OrdinalIgnoreCase))
            return word[..^3] + "el";

        if (word.EndsWith("óis", StringComparison.OrdinalIgnoreCase))
            return word[..^3] + "ol";

        if (word.EndsWith("is", StringComparison.OrdinalIgnoreCase) && word.Length > 2)
            return word[..^2] + "l";

        if (word.EndsWith("ns", StringComparison.OrdinalIgnoreCase))
            return word[..^2] + "m";

        if (word.EndsWith("res", StringComparison.OrdinalIgnoreCase))
            return word[..^2];

        if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            return word[..^1];

        return word;
    }

    private static string PluralizePortuguese(string word)
    {
        // Handle words ending in ão
        if (word.EndsWith("ão", StringComparison.OrdinalIgnoreCase))
            return word[..^2] + "ões";

        // Handle words ending in l
        if (word.EndsWith("al", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("el", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("ol", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("ul", StringComparison.OrdinalIgnoreCase))
            return word[..^1] + "is";

        if (word.EndsWith("il", StringComparison.OrdinalIgnoreCase))
            return word[..^2] + "is";

        // Handle words ending in m
        if (word.EndsWith("m", StringComparison.OrdinalIgnoreCase))
            return word[..^1] + "ns";

        // Handle words ending in r, s, z
        if (word.EndsWith("r", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("z", StringComparison.OrdinalIgnoreCase))
            return word + "es";

        if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("x", StringComparison.OrdinalIgnoreCase))
            return word;

        return word + "s";
    }

    #endregion

    #region Spanish

    private static string SingularizeSpanish(string word)
    {
        if (word.EndsWith("ces", StringComparison.OrdinalIgnoreCase))
            return word[..^3] + "z";

        if (word.EndsWith("es", StringComparison.OrdinalIgnoreCase) && word.Length > 2)
            return word[..^2];

        if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            return word[..^1];

        return word;
    }

    private static string PluralizeSpanish(string word)
    {
        // Words ending in z -> ces
        if (word.EndsWith("z", StringComparison.OrdinalIgnoreCase))
            return word[..^1] + "ces";

        // Words ending in consonant -> +es
        if (word.EndsWith("r", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("l", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("n", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("d", StringComparison.OrdinalIgnoreCase))
            return word + "es";

        // Words ending in vowel -> +s
        if (EndsWithVowel(word))
            return word + "s";

        return word + "es";
    }

    #endregion

    #region French

    private static string SingularizeFrench(string word)
    {
        if (word.EndsWith("aux", StringComparison.OrdinalIgnoreCase))
            return word[..^2] + "l";

        if (word.EndsWith("eaux", StringComparison.OrdinalIgnoreCase))
            return word[..^1];

        if (word.EndsWith("eux", StringComparison.OrdinalIgnoreCase))
            return word[..^1];

        if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("x", StringComparison.OrdinalIgnoreCase))
            return word[..^1];

        return word;
    }

    private static string PluralizeFrench(string word)
    {
        // Words ending in -al -> -aux
        if (word.EndsWith("al", StringComparison.OrdinalIgnoreCase))
            return word[..^2] + "aux";

        // Words ending in -eau, -au, -eu -> +x
        if (word.EndsWith("eau", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("au", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("eu", StringComparison.OrdinalIgnoreCase))
            return word + "x";

        // Words already ending in s, x, z -> unchanged
        if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("x", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("z", StringComparison.OrdinalIgnoreCase))
            return word;

        return word + "s";
    }

    #endregion

    #region German

    private static string SingularizeGerman(string word)
    {
        // German plural forms are complex - simplified approach
        if (word.EndsWith("en", StringComparison.OrdinalIgnoreCase))
            return word[..^2];

        if (word.EndsWith("er", StringComparison.OrdinalIgnoreCase))
            return word[..^2];

        if (word.EndsWith("e", StringComparison.OrdinalIgnoreCase))
            return word[..^1];

        if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            return word[..^1];

        return word;
    }

    private static string PluralizeGerman(string word)
    {
        // German pluralization is complex - simplified rules
        if (word.EndsWith("e", StringComparison.OrdinalIgnoreCase))
            return word + "n";

        if (word.EndsWith("er", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("en", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("el", StringComparison.OrdinalIgnoreCase))
            return word;

        return word + "en";
    }

    #endregion

    #region Italian

    private static string SingularizeItalian(string word)
    {
        if (word.EndsWith("i", StringComparison.OrdinalIgnoreCase))
            return word[..^1] + "o";

        if (word.EndsWith("e", StringComparison.OrdinalIgnoreCase))
            return word[..^1] + "a";

        return word;
    }

    private static string PluralizeItalian(string word)
    {
        // Masculine words ending in -o -> -i
        if (word.EndsWith("o", StringComparison.OrdinalIgnoreCase))
            return word[..^1] + "i";

        // Feminine words ending in -a -> -e
        if (word.EndsWith("a", StringComparison.OrdinalIgnoreCase))
            return word[..^1] + "e";

        // Words ending in -e -> -i
        if (word.EndsWith("e", StringComparison.OrdinalIgnoreCase))
            return word[..^1] + "i";

        return word + "i";
    }

    #endregion

    #region Dutch

    private static string SingularizeDutch(string word)
    {
        if (word.EndsWith("en", StringComparison.OrdinalIgnoreCase))
            return word[..^2];

        if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            return word[..^1];

        return word;
    }

    private static string PluralizeDutch(string word)
    {
        // Words ending in certain patterns get -en
        if (word.EndsWith("el", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("em", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("en", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("er", StringComparison.OrdinalIgnoreCase))
            return word + "s";

        return word + "en";
    }

    #endregion

    #region Russian

    private static string SingularizeRussian(string word)
    {
        // Simplified Russian singularization
        if (word.EndsWith("ы", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("и", StringComparison.OrdinalIgnoreCase))
            return word[..^1];

        if (word.EndsWith("а", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("я", StringComparison.OrdinalIgnoreCase))
            return word[..^1];

        return word;
    }

    private static string PluralizeRussian(string word)
    {
        // Simplified Russian pluralization
        if (word.EndsWith("а", StringComparison.OrdinalIgnoreCase))
            return word[..^1] + "ы";

        if (word.EndsWith("я", StringComparison.OrdinalIgnoreCase))
            return word[..^1] + "и";

        if (word.EndsWith("й", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("ь", StringComparison.OrdinalIgnoreCase))
            return word[..^1] + "и";

        return word + "ы";
    }

    #endregion

    #region Polish

    private static string SingularizePolish(string word)
    {
        if (word.EndsWith("y", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("i", StringComparison.OrdinalIgnoreCase))
            return word[..^1];

        return word;
    }

    private static string PluralizePolish(string word)
    {
        // Simplified Polish pluralization
        if (word.EndsWith("a", StringComparison.OrdinalIgnoreCase))
            return word[..^1] + "y";

        return word + "y";
    }

    #endregion

    #region Turkish

    private static string SingularizeTurkish(string word)
    {
        // Turkish uses -lar/-ler for plural
        if (word.EndsWith("lar", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("ler", StringComparison.OrdinalIgnoreCase))
            return word[..^3];

        return word;
    }

    private static string PluralizeTurkish(string word)
    {
        // Turkish vowel harmony: back vowels use -lar, front vowels use -ler
        var lastVowel = GetLastVowel(word);
        if (lastVowel is 'a' or 'ı' or 'o' or 'u')
            return word + "lar";
        else
            return word + "ler";
    }

    private static char GetLastVowel(string word)
    {
        var vowels = new[] { 'a', 'e', 'ı', 'i', 'o', 'ö', 'u', 'ü' };
        for (int i = word.Length - 1; i >= 0; i--)
        {
            if (vowels.Contains(char.ToLowerInvariant(word[i])))
                return char.ToLowerInvariant(word[i]);
        }
        return 'e'; // default
    }

    #endregion

    #region Helper Methods

    private static bool EndsWithVowelPlusY(string word)
    {
        if (word.Length < 2)
            return false;

        var secondToLast = char.ToLowerInvariant(word[^2]);
        return secondToLast is 'a' or 'e' or 'i' or 'o' or 'u';
    }

    private static bool EndsWithVowel(string word)
    {
        if (string.IsNullOrEmpty(word))
            return false;

        var lastChar = char.ToLowerInvariant(word[^1]);
        return lastChar is 'a' or 'e' or 'i' or 'o' or 'u';
    }

    private static string PreserveCase(string original, string replacement)
    {
        if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(replacement))
            return replacement;

        if (char.IsUpper(original[0]))
            return char.ToUpperInvariant(replacement[0]) + replacement[1..];

        return replacement;
    }

    #endregion
}

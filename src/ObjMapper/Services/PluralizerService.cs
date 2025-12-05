using ObjMapper.Services.Pluralization;

namespace ObjMapper.Services;

/// <summary>
/// Service for pluralization and singularization in multiple languages.
/// Uses language-specific irregular dictionaries for accurate pluralization.
/// </summary>
public class PluralizerService
{
    private readonly string _locale;
    private readonly bool _disabled;

    /// <summary>
    /// List of supported locales with dialects.
    /// </summary>
    public static readonly string[] SupportedLocales =
    [
        // English (and dialects)
        "en-us", "en-gb", "en-au", "en-ca", "en-nz", "en-ie", "en",
        
        // Portuguese (and dialects)
        "pt-br", "pt-pt", "pt-ao", "pt-mz", "pt",
        
        // Spanish (and dialects)
        "es-es", "es-mx", "es-ar", "es-co", "es-cl", "es-pe", "es",
        
        // French (and dialects)
        "fr-fr", "fr-ca", "fr-be", "fr-ch", "fr",
        
        // German (and dialects)
        "de-de", "de-at", "de-ch", "de",
        
        // Italian
        "it-it", "it-ch", "it",
        
        // Dutch
        "nl-nl", "nl-be", "nl",
        
        // Russian
        "ru-ru", "ru",
        
        // Polish
        "pl-pl", "pl",
        
        // Turkish
        "tr-tr", "tr",
        
        // Japanese (standard and regional)
        "ja-jp", "ja",
        
        // Korean (South and North)
        "ko-kr", "ko-kp", "ko",
        
        // Chinese (Simplified, Traditional, and regional)
        "zh-cn", "zh-tw", "zh-hk", "zh-sg", "zh",
        
        // Additional Asian languages
        "vi-vn", "vi",        // Vietnamese
        "th-th", "th",        // Thai
        "id-id", "id",        // Indonesian
        "ms-my", "ms",        // Malay
        
        // Additional European languages
        "cs-cz", "cs",        // Czech
        "sk-sk", "sk",        // Slovak
        "hu-hu", "hu",        // Hungarian
        "ro-ro", "ro",        // Romanian
        "bg-bg", "bg",        // Bulgarian
        "uk-ua", "uk",        // Ukrainian
        "el-gr", "el",        // Greek
        "sv-se", "sv",        // Swedish
        "da-dk", "da",        // Danish
        "no-no", "nb", "nn",  // Norwegian (Bokmål and Nynorsk)
        "fi-fi", "fi",        // Finnish
        
        // Middle Eastern
        "ar-sa", "ar-eg", "ar",  // Arabic
        "he-il", "he",           // Hebrew
        "fa-ir", "fa",           // Persian/Farsi
        
        // Indian subcontinent
        "hi-in", "hi",        // Hindi
        "bn-bd", "bn-in", "bn" // Bengali
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
            "ja" => JapaneseHelper.Instance.Singularize(word),
            "ko" => KoreanHelper.Instance.Singularize(word),
            "zh" => ChineseHelper.Instance.Singularize(word),
            "vi" or "th" or "id" or "ms" => word, // No plural forms
            "ar" or "he" or "fa" => word, // Complex plural systems, return as-is
            "hi" or "bn" => word, // Complex plural systems, return as-is
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
            "ja" => JapaneseHelper.Instance.Pluralize(word),
            "ko" => KoreanHelper.Instance.Pluralize(word),
            "zh" => ChineseHelper.Instance.Pluralize(word),
            "vi" or "th" or "id" or "ms" => word, // No plural forms
            "ar" or "he" or "fa" => word, // Complex plural systems, return as-is
            "hi" or "bn" => word, // Complex plural systems, return as-is
            _ => PluralizeEnglish(word)
        };
    }

    #region English

    private static string SingularizeEnglish(string word)
    {
        // Check dictionary first
        if (EnglishIrregulars.Instance.TryGetSingular(word, out var singular))
            return PreserveCase(word, singular!);

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
        // Check dictionary first
        if (EnglishIrregulars.Instance.TryGetPlural(word, out var plural))
            return PreserveCase(word, plural!);

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
        // Check dictionary first
        if (PortugueseIrregulars.Instance.TryGetSingular(word, out var singular))
            return PreserveCase(word, singular!);

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
        // Check dictionary first
        if (PortugueseIrregulars.Instance.TryGetPlural(word, out var plural))
            return PreserveCase(word, plural!);

        // Handle words ending in ão
        if (word.EndsWith("ão", StringComparison.OrdinalIgnoreCase))
            return word[..^2] + "ões";

        // Handle words ending in l
        if (word.EndsWith("al", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("ol", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("ul", StringComparison.OrdinalIgnoreCase))
            return word[..^1] + "is";

        if (word.EndsWith("el", StringComparison.OrdinalIgnoreCase))
            return word[..^2] + "éis";

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
        // Check dictionary first
        if (SpanishIrregulars.Instance.TryGetSingular(word, out var singular))
            return PreserveCase(word, singular!);

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
        // Check dictionary first
        if (SpanishIrregulars.Instance.TryGetPlural(word, out var plural))
            return PreserveCase(word, plural!);

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
        // Check dictionary first
        if (FrenchIrregulars.Instance.TryGetSingular(word, out var singular))
            return PreserveCase(word, singular!);

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
        // Check dictionary first
        if (FrenchIrregulars.Instance.TryGetPlural(word, out var plural))
            return PreserveCase(word, plural!);

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
        // Check dictionary first
        if (GermanIrregulars.Instance.TryGetSingular(word, out var singular))
            return PreserveCase(word, singular!);

        // German plural forms are complex - use conservative approach
        // Only handle clear patterns to avoid false positives
        
        // Words ending in -nen (like Studentinnen -> Studentin)
        if (word.EndsWith("nen", StringComparison.OrdinalIgnoreCase) && word.Length > 5)
            return word[..^2];

        // Foreign words ending in -s (like Autos -> Auto)
        if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase) && word.Length > 3)
            return word[..^1];

        return word;
    }

    private static string PluralizeGerman(string word)
    {
        // Check dictionary first
        if (GermanIrregulars.Instance.TryGetPlural(word, out var plural))
            return PreserveCase(word, plural!);

        // German pluralization is complex - simplified rules for common patterns
        // Words ending in -e often add -n
        if (word.EndsWith("e", StringComparison.OrdinalIgnoreCase))
            return word + "n";

        // Words already ending in typical plural markers remain unchanged
        if (word.EndsWith("er", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("en", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("el", StringComparison.OrdinalIgnoreCase))
            return word;

        // Foreign words add -s
        if (word.EndsWith("o", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("a", StringComparison.OrdinalIgnoreCase))
            return word + "s";

        return word + "en";
    }

    #endregion

    #region Italian

    private static string SingularizeItalian(string word)
    {
        // Check dictionary first
        if (ItalianIrregulars.Instance.TryGetSingular(word, out var singular))
            return PreserveCase(word, singular!);

        // Italian singularization - only handle clear patterns
        // Masculine plural -i -> singular -o (like libri -> libro)
        if (word.EndsWith("i", StringComparison.OrdinalIgnoreCase) && word.Length > 2)
        {
            // Only apply if it looks like a masculine plural
            var singularResult = word[..^1] + "o";
            return singularResult;
        }

        // Feminine plural -e from -a is ambiguous, leave as is
        return word;
    }

    private static string PluralizeItalian(string word)
    {
        // Check dictionary first
        if (ItalianIrregulars.Instance.TryGetPlural(word, out var plural))
            return PreserveCase(word, plural!);

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
        // Check dictionary first
        if (DutchIrregulars.Instance.TryGetSingular(word, out var singular))
            return PreserveCase(word, singular!);

        if (word.EndsWith("en", StringComparison.OrdinalIgnoreCase))
            return word[..^2];

        if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            return word[..^1];

        return word;
    }

    private static string PluralizeDutch(string word)
    {
        // Check dictionary first
        if (DutchIrregulars.Instance.TryGetPlural(word, out var plural))
            return PreserveCase(word, plural!);

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
        // Check dictionary first
        if (RussianIrregulars.Instance.TryGetSingular(word, out var singular))
            return PreserveCase(word, singular!);

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
        // Check dictionary first
        if (RussianIrregulars.Instance.TryGetPlural(word, out var plural))
            return PreserveCase(word, plural!);

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
        // Check dictionary first
        if (PolishIrregulars.Instance.TryGetSingular(word, out var singular))
            return PreserveCase(word, singular!);

        if (word.EndsWith("y", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("i", StringComparison.OrdinalIgnoreCase))
            return word[..^1];

        return word;
    }

    private static string PluralizePolish(string word)
    {
        // Check dictionary first
        if (PolishIrregulars.Instance.TryGetPlural(word, out var plural))
            return PreserveCase(word, plural!);

        // Simplified Polish pluralization
        if (word.EndsWith("a", StringComparison.OrdinalIgnoreCase))
            return word[..^1] + "y";

        return word + "y";
    }

    #endregion

    #region Turkish

    private static string SingularizeTurkish(string word)
    {
        // Check dictionary first
        if (TurkishIrregulars.Instance.TryGetSingular(word, out var singular))
            return PreserveCase(word, singular!);

        // Turkish uses -lar/-ler for plural
        if (word.EndsWith("lar", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("ler", StringComparison.OrdinalIgnoreCase))
            return word[..^3];

        return word;
    }

    private static string PluralizeTurkish(string word)
    {
        // Check dictionary first
        if (TurkishIrregulars.Instance.TryGetPlural(word, out var plural))
            return PreserveCase(word, plural!);

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

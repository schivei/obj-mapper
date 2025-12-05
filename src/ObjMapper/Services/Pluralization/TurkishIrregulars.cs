namespace ObjMapper.Services.Pluralization;

/// <summary>
/// Turkish irregular plurals dictionary.
/// Turkish uses vowel harmony for plural suffixes (-lar/-ler).
/// </summary>
public sealed class TurkishIrregulars : IrregularDictionary
{
    private static readonly Lazy<TurkishIrregulars> _instance = new(() => new TurkishIrregulars());
    public static TurkishIrregulars Instance => _instance.Value;

    private readonly Dictionary<string, string> _singularToPlural = new(StringComparer.OrdinalIgnoreCase)
    {
        // Words where plural form is unusual or colloquial
        { "su", "sular" },          // water(s)
        { "saç", "saçlar" },        // hair(s)
        { "ağaç", "ağaçlar" },      // tree(s)
        
        // Compound words
        { "başbakan", "başbakanlar" },    // prime minister(s)
        { "cumhurbaşkanı", "cumhurbaşkanları" }, // president(s)
        
        // Foreign words that follow Turkish rules
        { "bilgisayar", "bilgisayarlar" },   // computer(s)
        { "televizyon", "televizyonlar" },   // television(s)
        { "telefon", "telefonlar" },         // telephone(s)
        { "radyo", "radyolar" },             // radio(s)
        
        // Arabic/Persian loanwords (may keep original plural in formal contexts)
        { "memur", "memurlar" },     // official(s)
        { "makam", "makamlar" },     // position(s)
        { "mesele", "meseleler" },   // matter(s)
        { "müdür", "müdürler" },     // director(s)
        { "doktor", "doktorlar" },   // doctor(s)
        { "kitap", "kitaplar" },     // book(s)
        { "şehir", "şehirler" },     // city/cities
        { "devlet", "devletler" },   // state(s)
        { "millet", "milletler" },   // nation(s)
        
        // Words ending in vowels
        { "anne", "anneler" },       // mother(s)
        { "baba", "babalar" },       // father(s)
        { "dede", "dedeler" },       // grandfather(s)
        { "nine", "nineler" },       // grandmother(s)
        { "amca", "amcalar" },       // uncle(s) (paternal)
        { "teyze", "teyzeler" },     // aunt(s) (maternal)
        { "kardeş", "kardeşler" },   // sibling(s)
        
        // Words that don't pluralize (collective nouns)
        { "halk", "halk" },          // people (collective)
        { "millet", "milletler" },   // nation
        
        // Computing terms
        { "sunucu", "sunucular" },       // server(s)
        { "kullanıcı", "kullanıcılar" }, // user(s)
        { "dosya", "dosyalar" },         // file(s)
        { "klasör", "klasörler" },       // folder(s)
        { "veritabanı", "veritabanları" }, // database(s)
        { "arayüz", "arayüzler" },       // interface(s)
        { "tablo", "tablolar" },         // table(s)
        { "sütun", "sütunlar" },         // column(s)
        { "satır", "satırlar" },         // row(s)
    };

    protected override Dictionary<string, string> SingularToPlural => _singularToPlural;

    /// <summary>
    /// Determines the correct plural suffix based on vowel harmony.
    /// </summary>
    public static string GetPluralSuffix(string word)
    {
        if (string.IsNullOrEmpty(word))
            return "ler";  // default

        // Turkish vowels
        char[] backVowels = ['a', 'ı', 'o', 'u', 'A', 'I', 'O', 'U'];
        char[] frontVowels = ['e', 'i', 'ö', 'ü', 'E', 'İ', 'Ö', 'Ü'];

        // Find the last vowel
        for (int i = word.Length - 1; i >= 0; i--)
        {
            if (backVowels.Contains(word[i]))
                return "lar";
            if (frontVowels.Contains(word[i]))
                return "ler";
        }

        return "ler";  // default to front vowel harmony
    }
}

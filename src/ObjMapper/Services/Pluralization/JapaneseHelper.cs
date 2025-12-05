namespace ObjMapper.Services.Pluralization;

/// <summary>
/// Japanese language helper.
/// Japanese does not have grammatical plural forms like European languages.
/// However, for database table names, we can use common suffixes or leave as-is.
/// Supports dialects: ja-jp (standard), ja-kansai, ja-osaka, ja-kyoto
/// </summary>
public sealed class JapaneseHelper
{
    private static readonly Lazy<JapaneseHelper> _instance = new(() => new JapaneseHelper());
    public static JapaneseHelper Instance => _instance.Value;

    /// <summary>
    /// Common counter words (助数詞) for different types of objects.
    /// These are not plurals but quantity markers.
    /// </summary>
    private readonly Dictionary<string, string> _counterWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // People
        { "人", "人" },        // hito (people)
        { "ユーザー", "ユーザー" },  // users
        { "顧客", "顧客" },      // customers
        { "社員", "社員" },      // employees
        { "学生", "学生" },      // students
        
        // Animals
        { "匹", "匹" },        // small animals
        { "頭", "頭" },        // large animals
        { "羽", "羽" },        // birds
        
        // Flat objects
        { "枚", "枚" },        // flat objects (paper, tickets)
        { "ページ", "ページ" },   // pages
        { "画像", "画像" },      // images
        
        // Long objects
        { "本", "本" },        // long cylindrical objects
        { "行", "行" },        // lines/rows
        
        // Machines/vehicles
        { "台", "台" },        // machines, vehicles
        { "サーバー", "サーバー" }, // servers
        { "コンピュータ", "コンピュータ" }, // computers
        
        // Buildings/rooms
        { "軒", "軒" },        // buildings
        { "室", "室" },        // rooms
        
        // Abstract/general
        { "件", "件" },        // matters, cases
        { "個", "個" },        // general objects
        { "品", "品" },        // items, products
        { "種", "種" },        // types, kinds
        
        // Database/IT terms
        { "レコード", "レコード" },   // records
        { "テーブル", "テーブル" },   // tables
        { "カラム", "カラム" },      // columns
        { "データベース", "データベース" }, // databases
        { "ファイル", "ファイル" },   // files
        { "フォルダ", "フォルダ" },   // folders
        { "インデックス", "インデックス" }, // indexes
        { "エントリ", "エントリ" },   // entries
        { "オブジェクト", "オブジェクト" }, // objects
        { "クラス", "クラス" },      // classes
    };

    /// <summary>
    /// Optional plural suffixes (集合を示す接尾辞)
    /// These can be added to indicate collections in technical contexts.
    /// </summary>
    private readonly Dictionary<string, string> _collectionSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        // -tachi (達) - for people and sometimes animals
        { "人達", "人達" },
        { "子供達", "子供達" },
        
        // -ra (ら) - informal, for people
        { "彼ら", "彼ら" },
        { "彼女ら", "彼女ら" },
        
        // -gata (方) - polite, for people
        { "先生方", "先生方" },
        { "皆様方", "皆様方" },
        
        // -domo (ども) - humble or derogatory
        { "私ども", "私ども" },
    };

    /// <summary>
    /// In Japanese, pluralization is typically not needed.
    /// Returns the word as-is.
    /// </summary>
    public string Pluralize(string word) => word;

    /// <summary>
    /// In Japanese, singularization is typically not needed.
    /// Returns the word as-is.
    /// </summary>
    public string Singularize(string word) => word;

    /// <summary>
    /// Gets the appropriate counter word for a type of object.
    /// </summary>
    public string? GetCounterWord(string objectType)
    {
        return _counterWords.TryGetValue(objectType, out var counter) ? counter : null;
    }
}

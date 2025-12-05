namespace ObjMapper.Services.Pluralization;

/// <summary>
/// Chinese language helper.
/// Chinese (Mandarin) does not have grammatical plural forms.
/// Supports dialects: zh-cn (Simplified), zh-tw (Traditional), zh-hk (Hong Kong)
/// </summary>
public sealed class ChineseHelper
{
    private static readonly Lazy<ChineseHelper> _instance = new(() => new ChineseHelper());
    public static ChineseHelper Instance => _instance.Value;

    /// <summary>
    /// Common measure words (量词) for different types of objects.
    /// These are not plurals but classifiers used with numbers.
    /// </summary>
    private readonly Dictionary<string, (string simplified, string traditional)> _measureWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // General/default
        { "个/個", ("个", "個") },           // gè - most common, general
        
        // People
        { "位", ("位", "位") },              // wèi - polite for people
        { "口", ("口", "口") },              // kǒu - family members
        { "名", ("名", "名") },              // míng - formal for people
        
        // Animals
        { "只/隻", ("只", "隻") },           // zhī - small animals
        { "头/頭", ("头", "頭") },           // tóu - large animals
        { "条/條", ("条", "條") },           // tiáo - fish, snakes, roads
        
        // Flat objects
        { "张/張", ("张", "張") },           // zhāng - flat objects (paper, tables)
        { "页/頁", ("页", "頁") },           // yè - pages
        { "片", ("片", "片") },              // piàn - slices, pieces
        
        // Long/thin objects
        { "根", ("根", "根") },              // gēn - long thin objects
        { "支", ("支", "支") },              // zhī - pens, sticks
        { "行", ("行", "行") },              // háng - lines of text
        
        // Books/documents
        { "本", ("本", "本") },              // běn - books, magazines
        { "册/冊", ("册", "冊") },           // cè - volumes
        { "份", ("份", "份") },              // fèn - copies, portions
        
        // Machines/vehicles
        { "台/臺", ("台", "臺") },           // tái - machines, TVs
        { "辆/輛", ("辆", "輛") },           // liàng - vehicles
        { "部", ("部", "部") },              // bù - phones, movies, cars
        
        // Buildings
        { "座", ("座", "座") },              // zuò - buildings, mountains
        { "栋/棟", ("栋", "棟") },           // dòng - buildings
        { "间/間", ("间", "間") },           // jiān - rooms
        { "家", ("家", "家") },              // jiā - businesses, families
        
        // Groups/sets
        { "组/組", ("组", "組") },           // zǔ - groups, sets
        { "批", ("批", "批") },              // pī - batches
        { "套", ("套", "套") },              // tào - sets, suites
        
        // Abstract/general
        { "件", ("件", "件") },              // jiàn - matters, clothing
        { "项/項", ("项", "項") },           // xiàng - items, tasks
        { "种/種", ("种", "種") },           // zhǒng - types, kinds
        { "类/類", ("类", "類") },           // lèi - categories
    };

    /// <summary>
    /// Common computing terms in Simplified and Traditional Chinese.
    /// </summary>
    private readonly Dictionary<string, (string simplified, string traditional)> _computingTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        // Database terms
        { "database", ("数据库", "資料庫") },
        { "table", ("表", "表") },
        { "column", ("列", "欄") },
        { "row", ("行", "列") },
        { "record", ("记录", "記錄") },
        { "field", ("字段", "欄位") },
        { "index", ("索引", "索引") },
        { "key", ("键", "鍵") },
        { "query", ("查询", "查詢") },
        
        // Server/network terms
        { "server", ("服务器", "伺服器") },
        { "user", ("用户", "使用者") },
        { "file", ("文件", "檔案") },
        { "folder", ("文件夹", "資料夾") },
        { "interface", ("接口", "介面") },
        { "program", ("程序", "程式") },
        { "software", ("软件", "軟體") },
        { "hardware", ("硬件", "硬體") },
        { "network", ("网络", "網路") },
        { "memory", ("内存", "記憶體") },
    };

    /// <summary>
    /// In Chinese, pluralization is typically not needed.
    /// Returns the word as-is.
    /// </summary>
    public string Pluralize(string word) => word;

    /// <summary>
    /// In Chinese, singularization is typically not needed.
    /// Returns the word as-is.
    /// </summary>
    public string Singularize(string word) => word;

    /// <summary>
    /// Gets the appropriate measure word for a type of object.
    /// </summary>
    public (string simplified, string traditional)? GetMeasureWord(string objectType)
    {
        return _measureWords.TryGetValue(objectType, out var measure) ? measure : null;
    }

    /// <summary>
    /// Translates a computing term to Chinese (Simplified or Traditional).
    /// </summary>
    public string? TranslateComputingTerm(string term, bool useTraditional = false)
    {
        if (_computingTerms.TryGetValue(term, out var translation))
        {
            return useTraditional ? translation.traditional : translation.simplified;
        }
        return null;
    }
}

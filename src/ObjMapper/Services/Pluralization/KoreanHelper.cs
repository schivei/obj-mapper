namespace ObjMapper.Services.Pluralization;

/// <summary>
/// Korean language helper.
/// Korean does not typically use grammatical plural forms.
/// Supports dialects: ko-kr (Seoul standard), ko-kp (Pyongyang)
/// </summary>
public sealed class KoreanHelper
{
    private static readonly Lazy<KoreanHelper> _instance = new(() => new KoreanHelper());
    public static KoreanHelper Instance => _instance.Value;

    /// <summary>
    /// Optional plural suffixes in Korean.
    /// The suffix -들 (deul) can optionally mark plurality for nouns,
    /// especially animate nouns.
    /// </summary>
    private readonly Dictionary<string, string> _commonNouns = new(StringComparer.OrdinalIgnoreCase)
    {
        // People
        { "사람", "사람들" },      // person/people
        { "학생", "학생들" },      // student(s)
        { "선생님", "선생님들" },  // teacher(s)
        { "친구", "친구들" },      // friend(s)
        { "손님", "손님들" },      // guest(s)/customer(s)
        { "고객", "고객들" },      // customer(s)
        { "직원", "직원들" },      // employee(s)
        { "회원", "회원들" },      // member(s)
        { "사용자", "사용자들" },  // user(s)
        
        // Animals
        { "개", "개들" },          // dog(s)
        { "고양이", "고양이들" },  // cat(s)
        { "새", "새들" },          // bird(s)
        { "물고기", "물고기들" },  // fish(es)
        
        // Things (less common to pluralize)
        { "책", "책들" },          // book(s)
        { "차", "차들" },          // car(s)
        { "집", "집들" },          // house(s)
        { "나라", "나라들" },      // country/countries
        
        // Computing terms
        { "서버", "서버들" },      // server(s)
        { "파일", "파일들" },      // file(s)
        { "폴더", "폴더들" },      // folder(s)
        { "데이터베이스", "데이터베이스들" }, // database(s)
        { "테이블", "테이블들" },  // table(s)
        { "컬럼", "컬럼들" },      // column(s)
        { "인덱스", "인덱스들" },  // index(es)
        { "레코드", "레코드들" },  // record(s)
        { "클래스", "클래스들" },  // class(es)
        { "객체", "객체들" },      // object(s)
        { "인터페이스", "인터페이스들" }, // interface(s)
    };

    /// <summary>
    /// Counter words (분류사) used with numbers.
    /// Similar to Japanese and Chinese, Korean uses measure words.
    /// </summary>
    private readonly Dictionary<string, string> _counters = new(StringComparer.OrdinalIgnoreCase)
    {
        // General
        { "개", "개" },            // general counter
        
        // People
        { "명", "명" },            // people (formal)
        { "분", "분" },            // people (honorific)
        { "사람", "사람" },        // people (with native Korean numbers)
        
        // Animals
        { "마리", "마리" },        // animals
        
        // Flat objects
        { "장", "장" },            // sheets, pages
        
        // Machines/vehicles
        { "대", "대" },            // machines, vehicles
        
        // Books/volumes
        { "권", "권" },            // books, volumes
        
        // Buildings
        { "채", "채" },            // buildings (traditional)
        { "동", "동" },            // buildings (modern)
        
        // Bottles/cups
        { "병", "병" },            // bottles
        { "잔", "잔" },            // cups, glasses
    };

    /// <summary>
    /// In Korean, pluralization is typically optional.
    /// Returns the word as-is for database naming.
    /// </summary>
    public string Pluralize(string word) => word;

    /// <summary>
    /// In Korean, singularization removes -들 if present.
    /// </summary>
    public string Singularize(string word)
    {
        if (word.EndsWith("들"))
            return word[..^1];
        return word;
    }

    /// <summary>
    /// Adds the plural marker -들 to a word.
    /// </summary>
    public string AddPluralMarker(string word)
    {
        if (!word.EndsWith("들"))
            return word + "들";
        return word;
    }
}

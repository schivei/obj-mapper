namespace ObjMapper.Services.Pluralization;

/// <summary>
/// Polish irregular plurals dictionary.
/// </summary>
public sealed class PolishIrregulars : IrregularDictionary
{
    private static readonly Lazy<PolishIrregulars> _instance = new(() => new PolishIrregulars());
    public static PolishIrregulars Instance => _instance.Value;

    private readonly Dictionary<string, string> _singularToPlural = new(StringComparer.OrdinalIgnoreCase)
    {
        // Masculine personal (require special "virile" plural)
        { "człowiek", "ludzie" },
        { "brat", "bracia" },
        { "syn", "synowie" },
        { "mąż", "mężowie" },
        { "przyjaciel", "przyjaciele" },
        { "ksiądz", "księża" },
        { "książę", "książęta" },
        { "dziecko", "dzieci" },
        
        // Consonant alternations
        { "pies", "psy" },
        { "kot", "koty" },
        { "rok", "lata" },  // also "roki" in compounds
        { "dzień", "dni" },
        { "tydzień", "tygodnie" },
        { "miesiąc", "miesiące" },
        
        // -a -> -e (feminine nouns)
        { "kobieta", "kobiety" },
        { "książka", "książki" },
        { "noga", "nogi" },
        { "ręka", "ręce" },
        { "oko", "oczy" },
        { "ucho", "uszy" },
        
        // -um -> -a (neuter, Latin origin)
        { "muzeum", "muzea" },
        { "stadium", "stadia" },
        { "centrum", "centra" },
        { "akwarium", "akwaria" },
        { "audytorium", "audytoria" },
        { "kryterium", "kryteria" },
        { "medium", "media" },
        
        // Consonant softening
        { "gość", "goście" },
        { "koń", "konie" },
        { "liść", "liście" },
        { "niedźwiedź", "niedźwiedzie" },
        
        // -ość -> -ości
        { "możliwość", "możliwości" },
        { "trudność", "trudności" },
        { "własność", "własności" },
        
        // Irregular body parts
        { "oko", "oczy" },
        { "ucho", "uszy" },
        { "ręka", "ręce" },
        { "ramię", "ramiona" },
        { "imię", "imiona" },
        
        // Foreign words
        { "auto", "auta" },
        { "biuro", "biura" },
        { "hobby", "hobby" },
        { "menu", "menu" },
        { "jury", "jury" },
        { "tabu", "tabu" },
        
        // Computing terms
        { "serwer", "serwery" },
        { "użytkownik", "użytkownicy" },
        { "plik", "pliki" },
        { "folder", "foldery" },
        { "katalog", "katalogi" },
        { "baza danych", "bazy danych" },
        { "interfejs", "interfejsy" },
    };

    protected override Dictionary<string, string> SingularToPlural => _singularToPlural;
}

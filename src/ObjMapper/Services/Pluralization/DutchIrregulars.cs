namespace ObjMapper.Services.Pluralization;

/// <summary>
/// Dutch irregular plurals dictionary.
/// </summary>
public sealed class DutchIrregulars : IrregularDictionary
{
    private static readonly Lazy<DutchIrregulars> _instance = new(() => new DutchIrregulars());
    public static DutchIrregulars Instance => _instance.Value;

    private readonly Dictionary<string, string> _singularToPlural = new(StringComparer.OrdinalIgnoreCase)
    {
        // Vowel lengthening
        { "dag", "dagen" },
        { "pad", "paden" },
        { "bad", "baden" },
        { "slot", "sloten" },
        { "weg", "wegen" },
        { "god", "goden" },
        { "lid", "leden" },
        { "schip", "schepen" },
        { "stad", "steden" },
        { "rad", "raderen" },
        { "gelid", "gelederen" },
        
        // Consonant doubling
        { "bal", "ballen" },
        { "bel", "bellen" },
        { "bon", "bonnen" },
        { "bus", "bussen" },
        { "dak", "daken" },
        { "kat", "katten" },
        { "man", "mannen" },
        { "pan", "pannen" },
        { "pen", "pennen" },
        { "pot", "potten" },
        { "stem", "stemmen" },
        { "web", "webben" },
        
        // -heid -> -heden
        { "mogelijkheid", "mogelijkheden" },
        { "moeilijkheid", "moeilijkheden" },
        { "gelegenheid", "gelegenheden" },
        { "waarheid", "waarheden" },
        { "werkelijkheid", "werkelijkheden" },
        
        // Latin/Greek origins
        { "museum", "musea" },
        { "stadium", "stadia" },
        { "criterium", "criteria" },
        { "gymnasium", "gymnasia" },
        { "aquarium", "aquaria" },
        { "curriculum", "curricula" },
        { "medium", "media" },
        
        // Completely irregular
        { "ei", "eieren" },
        { "kind", "kinderen" },
        { "blad", "bladeren" },
        { "been", "beenderen" },
        { "kalf", "kalveren" },
        { "lam", "lammeren" },
        { "rund", "runderen" },
        { "hoen", "hoenderen" },
        { "volk", "volkeren" },
        
        // -s plurals (foreign words or short native words)
        { "auto", "auto's" },
        { "baby", "baby's" },
        { "café", "cafés" },
        { "foto", "foto's" },
        { "menu", "menu's" },
        { "paraplu", "paraplu's" },
        { "taxi", "taxi's" },
        
        // Same singular and plural
        { "schaap", "schapen" },
        { "ding", "dingen" },
        { "glas", "glazen" },
        
        // Computing terms
        { "server", "servers" },
        { "gebruiker", "gebruikers" },
        { "bestand", "bestanden" },
        { "map", "mappen" },
        { "database", "databases" },
        { "interface", "interfaces" },
    };

    protected override Dictionary<string, string> SingularToPlural => _singularToPlural;
}

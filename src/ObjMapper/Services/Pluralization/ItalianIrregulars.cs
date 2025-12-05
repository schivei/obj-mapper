namespace ObjMapper.Services.Pluralization;

/// <summary>
/// Italian irregular plurals dictionary.
/// </summary>
public sealed class ItalianIrregulars : IrregularDictionary
{
    private static readonly Lazy<ItalianIrregulars> _instance = new(() => new ItalianIrregulars());
    public static ItalianIrregulars Instance => _instance.Value;

    private readonly Dictionary<string, string> _singularToPlural = new(StringComparer.OrdinalIgnoreCase)
    {
        // Words ending in -co/-go (add h before i)
        { "amico", "amici" },
        { "antico", "antichi" },
        { "banco", "banchi" },
        { "buco", "buchi" },
        { "carico", "carichi" },
        { "chiosco", "chioschi" },
        { "circo", "circhi" },
        { "cuoco", "cuochi" },
        { "disco", "dischi" },
        { "duca", "duchi" },
        { "elenco", "elenchi" },
        { "falco", "falchi" },
        { "fico", "fichi" },
        { "fianco", "fianchi" },
        { "fuoco", "fuochi" },
        { "gioco", "giochi" },
        { "greco", "greci" },
        { "lago", "laghi" },
        { "luogo", "luoghi" },
        { "manico", "manici" },
        { "medico", "medici" },
        { "nemico", "nemici" },
        { "obbligo", "obblighi" },
        { "parco", "parchi" },
        { "porco", "porci" },
        { "sindaco", "sindaci" },
        { "stomaco", "stomaci" },
        { "tronco", "tronchi" },
        { "varco", "varchi" },
        
        // Words ending in -cia/-gia (lose the i)
        { "arancia", "arance" },
        { "camicia", "camicie" },
        { "ciliegia", "ciliegie" },
        { "doccia", "docce" },
        { "faccia", "facce" },
        { "frangia", "frange" },
        { "goccia", "gocce" },
        { "guancia", "guance" },
        { "roccia", "rocce" },
        { "spiaggia", "spiagge" },
        { "striscia", "strisce" },
        { "traccia", "tracce" },
        
        // Masculine words that become feminine in plural
        { "braccio", "braccia" },
        { "ciglio", "ciglia" },
        { "dito", "dita" },
        { "ginocchio", "ginocchia" },
        { "labbro", "labbra" },
        { "lenzuolo", "lenzuola" },
        { "muro", "mura" },
        { "osso", "ossa" },
        { "uovo", "uova" },
        
        // Completely irregular
        { "bue", "buoi" },
        { "dio", "dei" },
        { "uomo", "uomini" },
        
        // Invariable words
        { "bar", "bar" },
        { "caffè", "caffè" },
        { "città", "città" },
        { "crisi", "crisi" },
        { "film", "film" },
        { "re", "re" },
        { "serie", "serie" },
        { "sport", "sport" },
        { "tesi", "tesi" },
        { "università", "università" },
        { "virtù", "virtù" },
        
        // Words ending in accented vowel (invariable)
        { "caffè", "caffè" },
        { "tè", "tè" },
        { "perché", "perché" },
        
        // Foreign words
        { "computer", "computer" },
        { "hotel", "hotel" },
        { "mouse", "mouse" },
        { "software", "software" },
        { "weekend", "weekend" },
        
        // Computing terms
        { "server", "server" },
        { "utente", "utenti" },
        { "file", "file" },
        { "cartella", "cartelle" },
        { "database", "database" },
        { "interfaccia", "interfacce" },
    };

    protected override Dictionary<string, string> SingularToPlural => _singularToPlural;
}

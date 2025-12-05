namespace ObjMapper.Services.Pluralization;

/// <summary>
/// English irregular plurals dictionary.
/// </summary>
public sealed class EnglishIrregulars : IrregularDictionary
{
    private static readonly Lazy<EnglishIrregulars> _instance = new(() => new EnglishIrregulars());
    public static EnglishIrregulars Instance => _instance.Value;

    private readonly Dictionary<string, string> _singularToPlural = new(StringComparer.OrdinalIgnoreCase)
    {
        // Common irregulars
        { "person", "people" },
        { "man", "men" },
        { "woman", "women" },
        { "child", "children" },
        { "tooth", "teeth" },
        { "foot", "feet" },
        { "mouse", "mice" },
        { "goose", "geese" },
        { "ox", "oxen" },
        { "louse", "lice" },
        { "die", "dice" },
        
        // Latin/Greek origins
        { "datum", "data" },
        { "criterion", "criteria" },
        { "phenomenon", "phenomena" },
        { "analysis", "analyses" },
        { "basis", "bases" },
        { "crisis", "crises" },
        { "diagnosis", "diagnoses" },
        { "hypothesis", "hypotheses" },
        { "parenthesis", "parentheses" },
        { "synthesis", "syntheses" },
        { "thesis", "theses" },
        { "axis", "axes" },
        { "appendix", "appendices" },
        { "index", "indices" },
        { "matrix", "matrices" },
        { "vertex", "vertices" },
        { "vortex", "vortices" },
        { "apex", "apices" },
        { "focus", "foci" },
        { "nucleus", "nuclei" },
        { "radius", "radii" },
        { "stimulus", "stimuli" },
        { "syllabus", "syllabi" },
        { "alumnus", "alumni" },
        { "fungus", "fungi" },
        { "cactus", "cacti" },
        { "bacterium", "bacteria" },
        { "curriculum", "curricula" },
        { "medium", "media" },
        { "memorandum", "memoranda" },
        { "millennium", "millennia" },
        { "stratum", "strata" },
        { "addendum", "addenda" },
        { "erratum", "errata" },
        { "ovum", "ova" },
        { "formula", "formulae" },
        { "larva", "larvae" },
        { "antenna", "antennae" },
        { "vertebra", "vertebrae" },
        { "nebula", "nebulae" },
        
        // Same singular and plural
        { "sheep", "sheep" },
        { "fish", "fish" },
        { "deer", "deer" },
        { "species", "species" },
        { "series", "series" },
        { "aircraft", "aircraft" },
        { "spacecraft", "spacecraft" },
        { "hovercraft", "hovercraft" },
        { "moose", "moose" },
        { "salmon", "salmon" },
        { "trout", "trout" },
        { "swine", "swine" },
        { "bison", "bison" },
        { "corps", "corps" },
        { "means", "means" },
        { "offspring", "offspring" },
        { "headquarters", "headquarters" },
        { "news", "news" },
        { "information", "information" },
        { "equipment", "equipment" },
        { "furniture", "furniture" },
        { "luggage", "luggage" },
        { "software", "software" },
        { "hardware", "hardware" },
        { "knowledge", "knowledge" },
        { "advice", "advice" },
        
        // Special cases
        { "knife", "knives" },
        { "wife", "wives" },
        { "life", "lives" },
        { "leaf", "leaves" },
        { "shelf", "shelves" },
        { "calf", "calves" },
        { "half", "halves" },
        { "wolf", "wolves" },
        { "thief", "thieves" },
        { "loaf", "loaves" },
        { "elf", "elves" },
        { "self", "selves" },
        
        // -us to -i or -uses
        { "campus", "campuses" },
        { "census", "censuses" },
        { "status", "statuses" },
        { "virus", "viruses" },
        { "bonus", "bonuses" },
        { "corpus", "corpora" },
        { "genus", "genera" },
        
        // Technical/computing terms
        { "schema", "schemas" },
        { "database", "databases" },
        { "interface", "interfaces" },
    };

    protected override Dictionary<string, string> SingularToPlural => _singularToPlural;
}

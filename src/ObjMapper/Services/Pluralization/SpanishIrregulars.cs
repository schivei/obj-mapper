namespace ObjMapper.Services.Pluralization;

/// <summary>
/// Spanish irregular plurals dictionary.
/// </summary>
public sealed class SpanishIrregulars : IrregularDictionary
{
    private static readonly Lazy<SpanishIrregulars> _instance = new(() => new SpanishIrregulars());
    public static SpanishIrregulars Instance => _instance.Value;

    private readonly Dictionary<string, string> _singularToPlural = new(StringComparer.OrdinalIgnoreCase)
    {
        // Words with accent changes
        { "carácter", "caracteres" },
        { "espécimen", "especímenes" },
        { "régimen", "regímenes" },
        
        // Words ending in stressed vowel
        { "sofá", "sofás" },
        { "menú", "menús" },
        { "papá", "papás" },
        { "mamá", "mamás" },
        { "café", "cafés" },
        { "champú", "champús" },
        
        // Words ending in -z
        { "actriz", "actrices" },
        { "aprendiz", "aprendices" },
        { "capaz", "capaces" },
        { "cruz", "cruces" },
        { "disfraz", "disfraces" },
        { "eficaz", "eficaces" },
        { "feliz", "felices" },
        { "juez", "jueces" },
        { "lápiz", "lápices" },
        { "luz", "luces" },
        { "nariz", "narices" },
        { "nuez", "nueces" },
        { "pez", "peces" },
        { "raíz", "raíces" },
        { "veloz", "veloces" },
        { "vez", "veces" },
        { "voz", "voces" },
        
        // Latin origins
        { "campus", "campus" },
        { "corpus", "corpus" },
        { "currículum", "currículos" },
        { "déficit", "déficits" },
        { "estatus", "estatus" },
        { "memorándum", "memoranda" },
        { "superávit", "superávits" },
        { "tesis", "tesis" },
        { "virus", "virus" },
        
        // Invariable words (same singular and plural)
        { "crisis", "crisis" },
        { "análisis", "análisis" },
        { "caries", "caries" },
        { "cosmos", "cosmos" },
        { "dosis", "dosis" },
        { "énfasis", "énfasis" },
        { "génesis", "génesis" },
        { "hipótesis", "hipótesis" },
        { "lunes", "lunes" },
        { "martes", "martes" },
        { "miércoles", "miércoles" },
        { "jueves", "jueves" },
        { "viernes", "viernes" },
        { "oasis", "oasis" },
        { "paréntesis", "paréntesis" },
        { "síntesis", "síntesis" },
        
        // Words ending in -s with stress on penultimate syllable (invariable)
        { "atlas", "atlas" },
        { "bíceps", "bíceps" },
        { "fórceps", "fórceps" },
        { "tríceps", "tríceps" },
        
        // Compound words
        { "cualquiera", "cualesquiera" },
        { "quienquiera", "quienesquiera" },
        
        // Words with accent that changes position
        { "joven", "jóvenes" },
        { "orden", "órdenes" },
        { "origen", "orígenes" },
        { "imagen", "imágenes" },
        { "margen", "márgenes" },
        { "virgen", "vírgenes" },
        { "resumen", "resúmenes" },
        { "examen", "exámenes" },
        { "volumen", "volúmenes" },
        { "certamen", "certámenes" },
        { "crimen", "crímenes" },
        { "germen", "gérmenes" },
        
        // Computing terms
        { "servidor", "servidores" },
        { "usuario", "usuarios" },
        { "archivo", "archivos" },
        { "directorio", "directorios" },
        { "interfaz", "interfaces" },
        { "base de datos", "bases de datos" },
    };

    protected override Dictionary<string, string> SingularToPlural => _singularToPlural;
}

namespace ObjMapper.Services.Pluralization;

/// <summary>
/// French irregular plurals dictionary.
/// </summary>
public sealed class FrenchIrregulars : IrregularDictionary
{
    private static readonly Lazy<FrenchIrregulars> _instance = new(() => new FrenchIrregulars());
    public static FrenchIrregulars Instance => _instance.Value;

    private readonly Dictionary<string, string> _singularToPlural = new(StringComparer.OrdinalIgnoreCase)
    {
        // Words ending in -al -> -aux
        { "animal", "animaux" },
        { "bocal", "bocaux" },
        { "canal", "canaux" },
        { "capital", "capitaux" },
        { "cardinal", "cardinaux" },
        { "cheval", "chevaux" },
        { "général", "généraux" },
        { "hôpital", "hôpitaux" },
        { "idéal", "idéaux" },
        { "journal", "journaux" },
        { "local", "locaux" },
        { "métal", "métaux" },
        { "original", "originaux" },
        { "principal", "principaux" },
        { "signal", "signaux" },
        { "tribunal", "tribunaux" },
        
        // Exceptions to -al -> -aux (regular -als)
        { "bal", "bals" },
        { "carnaval", "carnavals" },
        { "chacal", "chacals" },
        { "festival", "festivals" },
        { "récital", "récitals" },
        { "régal", "régals" },
        
        // Words ending in -ail -> -aux
        { "bail", "baux" },
        { "corail", "coraux" },
        { "émail", "émaux" },
        { "soupirail", "soupiraux" },
        { "travail", "travaux" },
        { "ventail", "ventaux" },
        { "vitrail", "vitraux" },
        
        // Words ending in -au, -eau, -eu -> add x
        { "bateau", "bateaux" },
        { "beau", "beaux" },
        { "bureau", "bureaux" },
        { "cadeau", "cadeaux" },
        { "chapeau", "chapeaux" },
        { "château", "châteaux" },
        { "couteau", "couteaux" },
        { "drapeau", "drapeaux" },
        { "eau", "eaux" },
        { "feu", "feux" },
        { "gâteau", "gâteaux" },
        { "jeu", "jeux" },
        { "lieu", "lieux" },
        { "morceau", "morceaux" },
        { "neveu", "neveux" },
        { "nouveau", "nouveaux" },
        { "noyau", "noyaux" },
        { "oiseau", "oiseaux" },
        { "réseau", "réseaux" },
        { "tableau", "tableaux" },
        { "tuyau", "tuyaux" },
        { "veau", "veaux" },
        { "voeu", "voeux" },
        
        // Exceptions (add s instead of x)
        { "bleu", "bleus" },
        { "pneu", "pneus" },
        { "émeu", "émeus" },
        { "landau", "landaus" },
        { "sarrau", "sarraus" },
        
        // Words ending in -ou -> -oux
        { "bijou", "bijoux" },
        { "caillou", "cailloux" },
        { "chou", "choux" },
        { "genou", "genoux" },
        { "hibou", "hiboux" },
        { "joujou", "joujoux" },
        { "pou", "poux" },
        
        // Completely irregular
        { "oeil", "yeux" },
        { "ciel", "cieux" },
        { "aïeul", "aïeux" },
        { "ail", "aulx" },
        { "monsieur", "messieurs" },
        { "madame", "mesdames" },
        { "mademoiselle", "mesdemoiselles" },
        
        // Invariable words
        { "bois", "bois" },
        { "choix", "choix" },
        { "corps", "corps" },
        { "croix", "croix" },
        { "fois", "fois" },
        { "mois", "mois" },
        { "nez", "nez" },
        { "pays", "pays" },
        { "poids", "poids" },
        { "prix", "prix" },
        { "taux", "taux" },
        { "temps", "temps" },
        { "voix", "voix" },
        
        // Computing terms
        { "ordinateur", "ordinateurs" },
        { "serveur", "serveurs" },
        { "utilisateur", "utilisateurs" },
        { "fichier", "fichiers" },
        { "répertoire", "répertoires" },
        { "base de données", "bases de données" },
    };

    protected override Dictionary<string, string> SingularToPlural => _singularToPlural;
}

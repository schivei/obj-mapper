namespace ObjMapper.Services.Pluralization;

/// <summary>
/// Portuguese irregular plurals dictionary (Brazil and Portugal).
/// </summary>
public sealed class PortugueseIrregulars : IrregularDictionary
{
    private static readonly Lazy<PortugueseIrregulars> _instance = new(() => new PortugueseIrregulars());
    public static PortugueseIrregulars Instance => _instance.Value;

    private readonly Dictionary<string, string> _singularToPlural = new(StringComparer.OrdinalIgnoreCase)
    {
        // Words ending in -ão with different plural forms
        // -ão -> -ões (most common)
        { "ação", "ações" },
        { "avião", "aviões" },
        { "balão", "balões" },
        { "botão", "botões" },
        { "camião", "camiões" },
        { "canção", "canções" },
        { "capitão", "capitães" },
        { "coração", "corações" },
        { "eleição", "eleições" },
        { "estação", "estações" },
        { "exceção", "exceções" },
        { "fogão", "fogões" },
        { "formação", "formações" },
        { "função", "funções" },
        { "informação", "informações" },
        { "leão", "leões" },
        { "lição", "lições" },
        { "melão", "melões" },
        { "nação", "nações" },
        { "noção", "noções" },
        { "opinião", "opiniões" },
        { "operação", "operações" },
        { "padrão", "padrões" },
        { "posição", "posições" },
        { "questão", "questões" },
        { "razão", "razões" },
        { "região", "regiões" },
        { "relação", "relações" },
        { "reunião", "reuniões" },
        { "sessão", "sessões" },
        { "situação", "situações" },
        { "solução", "soluções" },
        { "televisão", "televisões" },
        { "tradição", "tradições" },
        { "transação", "transações" },
        { "versão", "versões" },
        { "visão", "visões" },
        
        // -ão -> -ães
        { "alemão", "alemães" },
        { "cão", "cães" },
        { "charlatão", "charlatães" },
        { "escrivão", "escrivães" },
        { "guardião", "guardiães" },
        { "pão", "pães" },
        { "sacristão", "sacristães" },
        { "tabelião", "tabeliães" },
        
        // -ão -> -ãos
        { "cidadão", "cidadãos" },
        { "cristão", "cristãos" },
        { "grão", "grãos" },
        { "irmão", "irmãos" },
        { "mão", "mãos" },
        { "órgão", "órgãos" },
        { "órfão", "órfãos" },
        { "pagão", "pagãos" },
        { "sótão", "sótãos" },
        { "vão", "vãos" },
        
        // Double plural forms (both are correct)
        { "ancião", "anciãos" },  // also anciães, anciões
        { "anão", "anões" },      // also anãos
        { "corrimão", "corrimãos" }, // also corrimões
        { "vilão", "vilões" },   // also vilãos
        { "verão", "verões" },   // also verãos
        { "vulcão", "vulcões" }, // also vulcãos
        
        // Irregular words ending in -l
        { "álcool", "álcoois" },
        { "anzol", "anzóis" },
        { "caracol", "caracóis" },
        { "cônsul", "cônsules" },
        { "farol", "faróis" },
        { "funil", "funis" },
        { "lençol", "lençóis" },
        { "paul", "pauis" },
        { "réptil", "répteis" },
        { "sol", "sóis" },
        
        // Words ending in -x (invariable)
        { "cóccix", "cóccix" },
        { "fênix", "fênix" },
        { "látex", "látex" },
        { "ônix", "ônix" },
        { "sílex", "sílex" },
        { "tórax", "tórax" },
        
        // Words ending in -s (invariable when paroxytone)
        { "atlas", "atlas" },
        { "cais", "cais" },
        { "lápis", "lápis" },
        { "ônibus", "ônibus" },
        { "pires", "pires" },
        { "vírus", "vírus" },
        
        // Latin and Greek origins
        { "campus", "campi" },
        { "corpus", "corpora" },
        
        // Other irregulars
        { "abdômen", "abdomens" },
        { "éden", "édens" },
        { "espécimen", "espécimens" },
        { "hífen", "hífens" },
        { "líquen", "líquens" },
        { "pólen", "pólens" },
        { "sêmen", "sêmens" },
        
        // Compound words
        { "qualquer", "quaisquer" },
        { "guarda-chuva", "guarda-chuvas" },
        { "couve-flor", "couves-flores" },
        { "amor-perfeito", "amores-perfeitos" },
        { "água-de-colônia", "águas-de-colônia" },
        
        // Computing terms
        { "cursor", "cursores" },
        { "servidor", "servidores" },
        { "usuário", "usuários" },
        { "arquivo", "arquivos" },
        { "diretório", "diretórios" },
    };

    protected override Dictionary<string, string> SingularToPlural => _singularToPlural;
}

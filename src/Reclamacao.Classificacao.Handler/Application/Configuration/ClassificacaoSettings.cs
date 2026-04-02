namespace Reclamacao.Classificacao.Handler.Application.Configuration;

public class ClassificacaoSettings
{
    public decimal BedrockConfidenceThreshold { get; set; } = 0.7m;
    public decimal KeywordFallbackMinScore { get; set; } = 0.1m;
}

using Reclamacao.Classificacao.Handler.Application.Interfaces;
using Reclamacao.Classificacao.Handler.Application.Events;
using Reclamacao.Classificacao.Handler.Application.Configuration;
using Reclamacao.Classificacao.Handler.Domain.Enums;
using Reclamacao.Classificacao.Handler.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Reclamacao.Classificacao.Handler.Application.Commands;

public class ClassificarReclamacaoCommandHandler
{
    private readonly IReclamacaoRepository _repository;
    private readonly IBedrockService _bedrock;
    private readonly IKeywordClassifier _keyword;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<ClassificarReclamacaoCommandHandler> _logger;
    private readonly ClassificacaoSettings _settings;

    public ClassificarReclamacaoCommandHandler(
        IReclamacaoRepository repository,
        IBedrockService bedrock,
        IKeywordClassifier keyword,
        IEventPublisher publisher,
        ILogger<ClassificarReclamacaoCommandHandler> logger,
        ClassificacaoSettings settings)
    {
        _repository = repository;
        _bedrock = bedrock;
        _keyword = keyword;
        _publisher = publisher;
        _logger = logger;
        _settings = settings;
    }

    public async Task HandleAsync(ReclamacaoRecebidaEvent message)
    {
        _logger.LogInformation("Processando classificação para reclamação {ReclamacaoId}", message.ReclamacaoId);

        var reclamacao = await _repository.GetByIdAsync(message.ReclamacaoId);
        if (reclamacao == null)
        {
            _logger.LogWarning("Reclamação {ReclamacaoId} não encontrada no repositório", message.ReclamacaoId);
            return;
        }

        if (reclamacao.Status == StatusReclamacao.Classificada)
        {
            _logger.LogInformation("Reclamação {ReclamacaoId} já classificada — idempotência", message.ReclamacaoId);
            return;
        }

        CategoriaReclamacao categoria;
        decimal score;
        ClassificadoPor por;

        try
        {
            var result = await _bedrock.ClassificarAsync(message.TextoReclamacao);
            if (result.Confianca >= _settings.BedrockConfidenceThreshold)
            {
                categoria = result.Categoria;
                score = result.Confianca;
                por = ClassificadoPor.Bedrock;
                _logger.LogInformation("Bedrock classificou {ReclamacaoId} como {Categoria} (score: {Score})",
                    message.ReclamacaoId, categoria, score);
            }
            else
            {
                _logger.LogInformation("Bedrock retornou baixa confiança ({Score}) para {ReclamacaoId}, acionando fallback",
                    result.Confianca, message.ReclamacaoId);
                var fallback = _keyword.Classificar(message.TextoReclamacao);
                categoria = fallback.Categoria;
                score = fallback.Score;
                por = ClassificadoPor.KeywordFallback;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na chamada ao Bedrock para {ReclamacaoId}, acionando fallback", message.ReclamacaoId);
            var fallback = _keyword.Classificar(message.TextoReclamacao);
            categoria = fallback.Categoria;
            score = fallback.Score;
            por = ClassificadoPor.KeywordFallback;
        }

        reclamacao.Classificar(categoria, score, por, DateTimeOffset.UtcNow);

        try
        {
            await _repository.UpdateAsync(reclamacao);
        }
        catch (ConflictException ex)
        {
            _logger.LogWarning(ex, "Conflito ao atualizar reclamação {ReclamacaoId} — idempotência legítima ou conflito de versão",
                message.ReclamacaoId);
            return;
        }

        var output = new ReclamacaoClassificadaEvent(
            reclamacao.ReclamacaoId,
            message.Canal,
            reclamacao.Categoria.ToString()!,
            reclamacao.ConfiancaScore!.Value,
            reclamacao.ClassificadoPor.ToString()!,
            reclamacao.Status.ToString(),
            reclamacao.ClassificadoEm!.Value
        );

        await _publisher.PublishAsync(output);

        _logger.LogInformation("Reclamação {ReclamacaoId} classificada como {Categoria} por {ClassificadoPor}",
            message.ReclamacaoId, categoria, por);
    }
}

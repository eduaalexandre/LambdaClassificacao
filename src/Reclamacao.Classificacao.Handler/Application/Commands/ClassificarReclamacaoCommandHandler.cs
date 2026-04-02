using Reclamacao.Classificacao.Handler.Application.Interfaces;
using Reclamacao.Classificacao.Handler.Application.Events;
using Reclamacao.Classificacao.Handler.Domain.Enums;
using Reclamacao.Classificacao.Handler.Domain.Exceptions;

namespace Reclamacao.Classificacao.Handler.Application.Commands;

public class ClassificarReclamacaoCommandHandler
{
    private readonly IReclamacaoRepository _repository;
    private readonly IBedrockService _bedrock;
    private readonly IKeywordClassifier _keyword;
    private readonly IEventPublisher _publisher;
    private const decimal BEDROCK_THRESHOLD = 0.7m;

    public ClassificarReclamacaoCommandHandler(
        IReclamacaoRepository repository,
        IBedrockService bedrock,
        IKeywordClassifier keyword,
        IEventPublisher publisher)
    {
        _repository = repository;
        _bedrock = bedrock;
        _keyword = keyword;
        _publisher = publisher;
    }

    public async Task HandleAsync(ReclamacaoRecebidaEvent message)
    {
        var reclamacao = await _repository.GetByIdAsync(message.ReclamacaoId);
        if (reclamacao == null) return;

        if (reclamacao.Status == StatusReclamacao.Classificada)
            return;

        CategoriaReclamacao categoria;
        decimal score;
        ClassificadoPor por;

        try
        {
            var result = await _bedrock.ClassificarAsync(message.TextoReclamacao);
            if (result.Confianca >= BEDROCK_THRESHOLD)
            {
                categoria = result.Categoria;
                score = result.Confianca;
                por = ClassificadoPor.Bedrock;
            }
            else
            {
                var fallback = _keyword.Classificar(message.TextoReclamacao);
                categoria = fallback.Categoria;
                score = fallback.Score;
                por = ClassificadoPor.KeywordFallback;
            }
        }
        catch
        {
            var fallback = _keyword.Classificar(message.TextoReclamacao);
            categoria = fallback.Categoria;
            score = fallback.Score;
            por = ClassificadoPor.KeywordFallback;
        }

        reclamacao.Classificar(categoria, score, por, DateTimeOffset.UtcNow);

        await _repository.UpdateAsync(reclamacao);

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
    }
}

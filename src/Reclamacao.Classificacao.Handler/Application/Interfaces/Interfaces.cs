namespace Reclamacao.Classificacao.Handler.Application.Interfaces;

public interface IReclamacaoRepository
{
    Task<Domain.Entities.Reclamacao?> GetByIdAsync(Guid id);
    Task UpdateAsync(Domain.Entities.Reclamacao reclamacao);
}

public interface IBedrockService
{
    Task<(Domain.Enums.CategoriaReclamacao Categoria, decimal Confianca)> ClassificarAsync(string texto);
}

public interface IEventPublisher
{
    Task PublishAsync<T>(T @event) where T : class;
}

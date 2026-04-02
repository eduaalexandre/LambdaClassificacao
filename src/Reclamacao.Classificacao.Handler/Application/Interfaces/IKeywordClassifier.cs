using Reclamacao.Classificacao.Handler.Domain.Enums;

namespace Reclamacao.Classificacao.Handler.Application.Interfaces;

public interface IKeywordClassifier
{
    (CategoriaReclamacao Categoria, decimal Score) Classificar(string texto);
}

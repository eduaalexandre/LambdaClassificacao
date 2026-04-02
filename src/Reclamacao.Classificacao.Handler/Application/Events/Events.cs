using Reclamacao.Classificacao.Handler.Domain.Enums;

namespace Reclamacao.Classificacao.Handler.Application.Events;

public record ReclamacaoRecebidaEvent(
    Guid ReclamacaoId,
    string Canal,
    string NomeReclamante,
    string Cpf,
    string Email,
    string TextoReclamacao,
    string Status,
    DateTimeOffset RecebidoEm
);

public record ReclamacaoClassificadaEvent(
    Guid ReclamacaoId,
    string Canal,
    string Categoria,
    decimal ConfiancaScore,
    string ClassificadoPor,
    string Status,
    DateTimeOffset ClassificadoEm
);

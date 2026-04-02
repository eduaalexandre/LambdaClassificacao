using Reclamacao.Classificacao.Handler.Domain.Enums;
using Reclamacao.Classificacao.Handler.Domain.Exceptions;

namespace Reclamacao.Classificacao.Handler.Domain.Entities;

public class Reclamacao
{
    public Guid ReclamacaoId { get; private set; }
    public StatusReclamacao Status { get; private set; }
    public int Version { get; private set; }
    public CategoriaReclamacao? Categoria { get; private set; }
    public decimal? ConfiancaScore { get; private set; }
    public ClassificadoPor? ClassificadoPor { get; private set; }
    public DateTimeOffset? ClassificadoEm { get; private set; }

    private Reclamacao(Guid id, StatusReclamacao status, int version)
    {
        ReclamacaoId = id;
        Status = status;
        Version = version;
    }

    public static Reclamacao Reconstituir(Guid id, StatusReclamacao status, int version, CategoriaReclamacao? categoria = null, decimal? score = null, ClassificadoPor? por = null, DateTimeOffset? em = null)
    {
        return new Reclamacao(id, status, version)
        {
            Categoria = categoria,
            ConfiancaScore = score,
            ClassificadoPor = por,
            ClassificadoEm = em
        };
    }

    public void Classificar(CategoriaReclamacao categoria, decimal confianca, ClassificadoPor por, DateTimeOffset data)
    {
        if (Status != StatusReclamacao.Recebida)
        {
            throw new DomainException($"Não é possível classificar uma reclamação com status {Status}.");
        }

        Categoria = categoria;
        ConfiancaScore = confianca;
        ClassificadoPor = por;
        ClassificadoEm = data;
        Status = StatusReclamacao.Classificada;
        Version++;
    }
}

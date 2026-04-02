using Xunit;
using FluentAssertions;
using Reclamacao.Classificacao.Handler.Domain.Entities;
using Reclamacao.Classificacao.Handler.Domain.Enums;
using Reclamacao.Classificacao.Handler.Domain.Exceptions;
using Reclamacao.Classificacao.Handler.Application.Events;
using ReclamacaoEntity = Reclamacao.Classificacao.Handler.Domain.Entities.Reclamacao;

namespace Reclamacao.Classificacao.Handler.Tests.Domain;

public class ReclamacaoTests
{
    [Fact]
    public void Classificar_ComStatusRecebida_DeveAtualizarStatusEVersao()
    {
        // Arrange
        var id = Guid.NewGuid();
        var recla = ReclamacaoEntity.Reconstituir(id, StatusReclamacao.Recebida, 1);
        var data = DateTimeOffset.UtcNow;

        // Act
        recla.Classificar(CategoriaReclamacao.Fraude, 0.9m, ClassificadoPor.Bedrock, data);

        // Assert
        recla.Status.Should().Be(StatusReclamacao.Classificada);
        recla.Version.Should().Be(2);
        recla.Categoria.Should().Be(CategoriaReclamacao.Fraude);
        recla.ConfiancaScore.Should().Be(0.9m);
        recla.ClassificadoPor.Should().Be(ClassificadoPor.Bedrock);
        recla.ClassificadoEm.Should().Be(data);
    }

    [Fact]
    public void Classificar_ComStatusJaClassificado_DeveLancarDomainException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var recla = ReclamacaoEntity.Reconstituir(id, StatusReclamacao.Classificada, 2);

        // Act & Assert
        Action act = () => recla.Classificar(CategoriaReclamacao.Taxas, 0.8m, ClassificadoPor.KeywordFallback, DateTimeOffset.UtcNow);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ExcecaoConflito_DeveTerMensagem()
    {
        // Act
        var ex = new ConflictException("Conflito");

        // Assert
        ex.Message.Should().Be("Conflito");
    }

    [Fact]
    public void ReclamacaoClassificadaEvent_RecordCheck()
    {
        // Act
        var id = Guid.NewGuid();
        var data = DateTimeOffset.UtcNow;
        var evt = new ReclamacaoClassificadaEvent(id, "C", "Cat", 0.9m, "B", "S", data);

        // Assert
        evt.ReclamacaoId.Should().Be(id);
        evt.ClassificadoEm.Should().Be(data);
    }
}

using Xunit;
using FluentAssertions;
using Reclamacao.Classificacao.Handler.Domain.Enums;
using Reclamacao.Classificacao.Handler.Domain.Exceptions;
using Reclamacao.Classificacao.Handler.Application.Events;
using ReclamacaoEntity = Reclamacao.Classificacao.Handler.Domain.Entities.Reclamacao;

namespace Reclamacao.Classificacao.Handler.Tests.Domain;

public class ReclamacaoTests
{
    [Fact]
    public void Classificar_ComStatusRecebida_DeveAtualizarTodosOsCampos()
    {
        var id = Guid.NewGuid();
        var recla = ReclamacaoEntity.Reconstituir(id, StatusReclamacao.Recebida, 1);
        var data = DateTimeOffset.UtcNow;

        recla.Classificar(CategoriaReclamacao.Fraude, 0.9m, ClassificadoPor.Bedrock, data);

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
        var recla = ReclamacaoEntity.Reconstituir(Guid.NewGuid(), StatusReclamacao.Classificada, 2);

        Action act = () => recla.Classificar(CategoriaReclamacao.Taxas, 0.8m, ClassificadoPor.KeywordFallback, DateTimeOffset.UtcNow);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Reconstituir_ComTodosOsCampos_DevePreservarEstado()
    {
        var id = Guid.NewGuid();
        var data = DateTimeOffset.UtcNow;
        var recla = ReclamacaoEntity.Reconstituir(id, StatusReclamacao.Classificada, 3,
            CategoriaReclamacao.Atendimento, 0.75m, ClassificadoPor.Bedrock, data);

        recla.ReclamacaoId.Should().Be(id);
        recla.Status.Should().Be(StatusReclamacao.Classificada);
        recla.Version.Should().Be(3);
        recla.Categoria.Should().Be(CategoriaReclamacao.Atendimento);
        recla.ConfiancaScore.Should().Be(0.75m);
        recla.ClassificadoPor.Should().Be(ClassificadoPor.Bedrock);
        recla.ClassificadoEm.Should().Be(data);
    }

    [Fact]
    public void Reconstituir_SemCamposOpcionais_DeveSerNull()
    {
        var recla = ReclamacaoEntity.Reconstituir(Guid.NewGuid(), StatusReclamacao.Recebida, 1);

        recla.Categoria.Should().BeNull();
        recla.ConfiancaScore.Should().BeNull();
        recla.ClassificadoPor.Should().BeNull();
        recla.ClassificadoEm.Should().BeNull();
    }

    [Fact]
    public void ConflictException_DeveHerdarDeDomainException()
    {
        var ex = new ConflictException("Conflito");

        ex.Should().BeAssignableTo<DomainException>();
        ex.Message.Should().Be("Conflito");
    }

    [Fact]
    public void ReclamacaoClassificadaEvent_DeveTerTodosOsCampos()
    {
        var id = Guid.NewGuid();
        var data = DateTimeOffset.UtcNow;
        var evt = new ReclamacaoClassificadaEvent(id, "Digital", "Fraude", 0.9m, "Bedrock", "Classificada", data);

        evt.ReclamacaoId.Should().Be(id);
        evt.Canal.Should().Be("Digital");
        evt.Categoria.Should().Be("Fraude");
        evt.ConfiancaScore.Should().Be(0.9m);
        evt.ClassificadoPor.Should().Be("Bedrock");
        evt.Status.Should().Be("Classificada");
        evt.ClassificadoEm.Should().Be(data);
    }

    [Fact]
    public void ReclamacaoRecebidaEvent_DeveTerTodosOsCampos()
    {
        var id = Guid.NewGuid();
        var data = DateTimeOffset.UtcNow;
        var evt = new ReclamacaoRecebidaEvent(id, "Fisico", "João", "123.456.789-00", "j@e.com", "Texto", "Recebida", data);

        evt.ReclamacaoId.Should().Be(id);
        evt.Canal.Should().Be("Fisico");
        evt.NomeReclamante.Should().Be("João");
        evt.Cpf.Should().Be("123.456.789-00");
        evt.Email.Should().Be("j@e.com");
        evt.TextoReclamacao.Should().Be("Texto");
        evt.Status.Should().Be("Recebida");
        evt.RecebidoEm.Should().Be(data);
    }
}

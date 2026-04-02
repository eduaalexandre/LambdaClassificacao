using Xunit;
using FluentAssertions;
using Reclamacao.Classificacao.Handler.Application.Configuration;
using Reclamacao.Classificacao.Handler.Infrastructure.Fallback;
using Reclamacao.Classificacao.Handler.Domain.Enums;

namespace Reclamacao.Classificacao.Handler.Tests.Infrastructure;

public class KeywordClassifierTests
{
    private readonly KeywordClassifier _sut;

    public KeywordClassifierTests()
    {
        _sut = new KeywordClassifier(new ClassificacaoSettings { KeywordFallbackMinScore = 0.1m });
    }

    [Theory]
    [InlineData("Golpe no cartão clonado", CategoriaReclamacao.Fraude)]
    [InlineData("Cobrança de juros abusiva e taxa de anuidade", CategoriaReclamacao.Taxas)]
    [InlineData("Atendimento demorado e fila de espera", CategoriaReclamacao.Atendimento)]
    [InlineData("Produto com defeito e atraso na entrega", CategoriaReclamacao.Produto)]
    public void Classificar_TextoComKeywords_DeveRetornarCategoriaCorreta(string texto, CategoriaReclamacao esperada)
    {
        var (categoria, score) = _sut.Classificar(texto);

        categoria.Should().Be(esperada);
        score.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Classificar_TextoInvalido_DeveRetornarOutros(string texto)
    {
        var (categoria, score) = _sut.Classificar(texto);

        categoria.Should().Be(CategoriaReclamacao.Outros);
        score.Should().Be(0);
    }

    [Fact]
    public void Classificar_TextoSemKeywords_DeveRetornarOutros()
    {
        var (categoria, score) = _sut.Classificar("Texto genérico sem sentido algum");

        categoria.Should().Be(CategoriaReclamacao.Outros);
        score.Should().Be(0);
    }

    [Fact]
    public void Classificar_TextoComAcentos_DeveNormalizarEClassificar()
    {
        var (categoria, _) = _sut.Classificar("Cobrança abusiva de anuidade");

        categoria.Should().Be(CategoriaReclamacao.Taxas);
    }
}

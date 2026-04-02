using Xunit;
using FluentAssertions;
using Reclamacao.Classificacao.Handler.Infrastructure.Fallback;
using Reclamacao.Classificacao.Handler.Domain.Enums;

namespace Reclamacao.Classificacao.Handler.Tests.Infrastructure;

public class KeywordClassifierTests
{
    private readonly KeywordClassifier _sut = new();

    [Theory]
    [InlineData("Golpe no cartão clonado", CategoriaReclamacao.Fraude)]
    [InlineData("Cobrança de juros abusiva e taxa de anuidade", CategoriaReclamacao.Taxas)]
    [InlineData("Atendimento demorado e fila de espera", CategoriaReclamacao.Atendimento)]
    [InlineData("Produto com defeito e atraso na entrega", CategoriaReclamacao.Produto)]
    public void Classificar_TextoComKeywords_DeveRetornarCategoriaCorreta(string texto, CategoriaReclamacao esperada)
    {
        // Act
        var (categoria, score) = _sut.Classificar(texto);

        // Assert
        categoria.Should().Be(esperada);
        score.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Classificar_TextoInvalido_DeveRetornarOutros(string texto)
    {
        // Act
        var (categoria, score) = _sut.Classificar(texto);

        // Assert
        categoria.Should().Be(CategoriaReclamacao.Outros);
        score.Should().Be(0);
    }
}

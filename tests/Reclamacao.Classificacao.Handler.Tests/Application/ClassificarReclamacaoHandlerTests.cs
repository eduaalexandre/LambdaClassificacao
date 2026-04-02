using Xunit;
using Moq;
using FluentAssertions;
using Reclamacao.Classificacao.Handler.Application.Commands;
using Reclamacao.Classificacao.Handler.Application.Interfaces;
using Reclamacao.Classificacao.Handler.Application.Events;
using Reclamacao.Classificacao.Handler.Domain.Entities;
using Reclamacao.Classificacao.Handler.Domain.Enums;
using ReclamacaoEntity = Reclamacao.Classificacao.Handler.Domain.Entities.Reclamacao;

namespace Reclamacao.Classificacao.Handler.Tests.Application;

public class ClassificarReclamacaoHandlerTests
{
    private readonly Mock<IReclamacaoRepository> _repoMock = new();
    private readonly Mock<IBedrockService> _bedrockMock = new();
    private readonly Mock<IKeywordClassifier> _keywordMock = new();
    private readonly Mock<IEventPublisher> _publisherMock = new();
    private readonly ClassificarReclamacaoCommandHandler _sut;

    public ClassificarReclamacaoHandlerTests()
    {
        _sut = new ClassificarReclamacaoCommandHandler(
            _repoMock.Object,
            _bedrockMock.Object,
            _keywordMock.Object,
            _publisherMock.Object);
    }

    private ReclamacaoRecebidaEvent CreateEvent(Guid id) => new ReclamacaoRecebidaEvent(
        id, "Digital", "User", "123", "test@test.com", "Texto", "Recebida", DateTimeOffset.UtcNow);

    [Fact]
    public async Task Handle_BedrockComAltaConfianca_DeveUsarBedrock()
    {
        // Arrange
        var id = Guid.NewGuid();
        var recla = ReclamacaoEntity.Reconstituir(id, StatusReclamacao.Recebida, 1);
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(recla);
        _bedrockMock.Setup(b => b.ClassificarAsync(It.IsAny<string>()))
                    .ReturnsAsync((CategoriaReclamacao.Fraude, 0.8m));

        // Act
        await _sut.HandleAsync(CreateEvent(id));

        // Assert
        recla.Status.Should().Be(StatusReclamacao.Classificada);
        recla.ClassificadoPor.Should().Be(ClassificadoPor.Bedrock);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<ReclamacaoEntity>()), Times.Once);
        _publisherMock.Verify(p => p.PublishAsync(It.IsAny<ReclamacaoClassificadaEvent>()), Times.Once);
    }

    [Fact]
    public async Task Handle_BedrockComBaixaConfianca_DeveUsarFallback()
    {
        // Arrange
        var id = Guid.NewGuid();
        var recla = ReclamacaoEntity.Reconstituir(id, StatusReclamacao.Recebida, 1);
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(recla);
        _bedrockMock.Setup(b => b.ClassificarAsync(It.IsAny<string>()))
                    .ReturnsAsync((CategoriaReclamacao.Fraude, 0.5m));
        _keywordMock.Setup(k => k.Classificar(It.IsAny<string>()))
                    .Returns((CategoriaReclamacao.Taxas, 0.2m));

        // Act
        await _sut.HandleAsync(CreateEvent(id));

        // Assert
        recla.ClassificadoPor.Should().Be(ClassificadoPor.KeywordFallback);
        recla.Categoria.Should().Be(CategoriaReclamacao.Taxas);
    }

    [Fact]
    public async Task Handle_BedrockFalha_DeveUsarFallback()
    {
        // Arrange
        var id = Guid.NewGuid();
        var recla = ReclamacaoEntity.Reconstituir(id, StatusReclamacao.Recebida, 1);
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(recla);
        _bedrockMock.Setup(b => b.ClassificarAsync(It.IsAny<string>()))
                    .ThrowsAsync(new Exception("API Error"));
        _keywordMock.Setup(k => k.Classificar(It.IsAny<string>()))
                    .Returns((CategoriaReclamacao.Atendimento, 0.3m));

        // Act
        await _sut.HandleAsync(CreateEvent(id));

        // Assert
        recla.ClassificadoPor.Should().Be(ClassificadoPor.KeywordFallback);
        recla.Categoria.Should().Be(CategoriaReclamacao.Atendimento);
    }

    [Fact]
    public async Task Handle_ReclamacaoNaoEncontrada_DeveNaoFazerNada()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((ReclamacaoEntity?)null);

        // Act
        await _sut.HandleAsync(CreateEvent(id));

        // Assert
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<ReclamacaoEntity>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MensagemIdempotente_DeveNaoReclassificar()
    {
        // Arrange
        var id = Guid.NewGuid();
        var recla = ReclamacaoEntity.Reconstituir(id, StatusReclamacao.Classificada, 2);
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(recla);

        // Act
        await _sut.HandleAsync(CreateEvent(id));

        // Assert
        _bedrockMock.Verify(b => b.ClassificarAsync(It.IsAny<string>()), Times.Never);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<ReclamacaoEntity>()), Times.Never);
    }
}

using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Reclamacao.Classificacao.Handler.Application.Commands;
using Reclamacao.Classificacao.Handler.Application.Configuration;
using Reclamacao.Classificacao.Handler.Application.Interfaces;
using Reclamacao.Classificacao.Handler.Application.Events;
using Reclamacao.Classificacao.Handler.Domain.Enums;
using Reclamacao.Classificacao.Handler.Domain.Exceptions;
using ReclamacaoEntity = Reclamacao.Classificacao.Handler.Domain.Entities.Reclamacao;

namespace Reclamacao.Classificacao.Handler.Tests.Application;

public class ClassificarReclamacaoHandlerTests
{
    private readonly Mock<IReclamacaoRepository> _repoMock = new();
    private readonly Mock<IBedrockService> _bedrockMock = new();
    private readonly Mock<IKeywordClassifier> _keywordMock = new();
    private readonly Mock<IEventPublisher> _publisherMock = new();
    private readonly Mock<ILogger<ClassificarReclamacaoCommandHandler>> _loggerMock = new();
    private readonly ClassificacaoSettings _settings = new() { BedrockConfidenceThreshold = 0.7m, KeywordFallbackMinScore = 0.1m };
    private readonly ClassificarReclamacaoCommandHandler _sut;

    public ClassificarReclamacaoHandlerTests()
    {
        _sut = new ClassificarReclamacaoCommandHandler(
            _repoMock.Object,
            _bedrockMock.Object,
            _keywordMock.Object,
            _publisherMock.Object,
            _loggerMock.Object,
            _settings);
    }

    private ReclamacaoRecebidaEvent CreateEvent(Guid id) => new(
        id, "Digital", "User", "123.456.789-00", "test@test.com", "Texto da reclamação", "Recebida", DateTimeOffset.UtcNow);

    [Fact]
    public async Task Handle_BedrockComAltaConfianca_DeveUsarBedrock()
    {
        var id = Guid.NewGuid();
        var recla = ReclamacaoEntity.Reconstituir(id, StatusReclamacao.Recebida, 1);
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(recla);
        _bedrockMock.Setup(b => b.ClassificarAsync(It.IsAny<string>()))
                    .ReturnsAsync((CategoriaReclamacao.Fraude, 0.8m));

        await _sut.HandleAsync(CreateEvent(id));

        recla.Status.Should().Be(StatusReclamacao.Classificada);
        recla.ClassificadoPor.Should().Be(ClassificadoPor.Bedrock);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<ReclamacaoEntity>()), Times.Once);
        _publisherMock.Verify(p => p.PublishAsync(It.IsAny<ReclamacaoClassificadaEvent>()), Times.Once);
    }

    [Fact]
    public async Task Handle_BedrockComBaixaConfianca_DeveUsarFallback()
    {
        var id = Guid.NewGuid();
        var recla = ReclamacaoEntity.Reconstituir(id, StatusReclamacao.Recebida, 1);
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(recla);
        _bedrockMock.Setup(b => b.ClassificarAsync(It.IsAny<string>()))
                    .ReturnsAsync((CategoriaReclamacao.Fraude, 0.5m));
        _keywordMock.Setup(k => k.Classificar(It.IsAny<string>()))
                    .Returns((CategoriaReclamacao.Taxas, 0.2m));

        await _sut.HandleAsync(CreateEvent(id));

        recla.ClassificadoPor.Should().Be(ClassificadoPor.KeywordFallback);
        recla.Categoria.Should().Be(CategoriaReclamacao.Taxas);
    }

    [Fact]
    public async Task Handle_BedrockFalha_DeveUsarFallback()
    {
        var id = Guid.NewGuid();
        var recla = ReclamacaoEntity.Reconstituir(id, StatusReclamacao.Recebida, 1);
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(recla);
        _bedrockMock.Setup(b => b.ClassificarAsync(It.IsAny<string>()))
                    .ThrowsAsync(new Exception("API Error"));
        _keywordMock.Setup(k => k.Classificar(It.IsAny<string>()))
                    .Returns((CategoriaReclamacao.Atendimento, 0.3m));

        await _sut.HandleAsync(CreateEvent(id));

        recla.ClassificadoPor.Should().Be(ClassificadoPor.KeywordFallback);
        recla.Categoria.Should().Be(CategoriaReclamacao.Atendimento);
    }

    [Fact]
    public async Task Handle_ReclamacaoNaoEncontrada_DeveNaoFazerNada()
    {
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((ReclamacaoEntity?)null);

        await _sut.HandleAsync(CreateEvent(id));

        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<ReclamacaoEntity>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MensagemIdempotente_DeveNaoReclassificar()
    {
        var id = Guid.NewGuid();
        var recla = ReclamacaoEntity.Reconstituir(id, StatusReclamacao.Classificada, 2);
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(recla);

        await _sut.HandleAsync(CreateEvent(id));

        _bedrockMock.Verify(b => b.ClassificarAsync(It.IsAny<string>()), Times.Never);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<ReclamacaoEntity>()), Times.Never);
        _publisherMock.Verify(p => p.PublishAsync(It.IsAny<ReclamacaoClassificadaEvent>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ConflictException_DeveLogarERetornar()
    {
        var id = Guid.NewGuid();
        var recla = ReclamacaoEntity.Reconstituir(id, StatusReclamacao.Recebida, 1);
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(recla);
        _bedrockMock.Setup(b => b.ClassificarAsync(It.IsAny<string>()))
                    .ReturnsAsync((CategoriaReclamacao.Fraude, 0.9m));
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<ReclamacaoEntity>()))
                 .ThrowsAsync(new ConflictException("Conflito de versão"));

        await _sut.HandleAsync(CreateEvent(id));

        _publisherMock.Verify(p => p.PublishAsync(It.IsAny<ReclamacaoClassificadaEvent>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EventBridgeFalha_DeveLancarExcecao()
    {
        var id = Guid.NewGuid();
        var recla = ReclamacaoEntity.Reconstituir(id, StatusReclamacao.Recebida, 1);
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(recla);
        _bedrockMock.Setup(b => b.ClassificarAsync(It.IsAny<string>()))
                    .ReturnsAsync((CategoriaReclamacao.Produto, 0.85m));
        _publisherMock.Setup(p => p.PublishAsync(It.IsAny<ReclamacaoClassificadaEvent>()))
                      .ThrowsAsync(new InvalidOperationException("EventBridge failure"));

        Func<Task> act = () => _sut.HandleAsync(CreateEvent(id));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_BedrockExatoNoThreshold_DeveUsarBedrock()
    {
        var id = Guid.NewGuid();
        var recla = ReclamacaoEntity.Reconstituir(id, StatusReclamacao.Recebida, 1);
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(recla);
        _bedrockMock.Setup(b => b.ClassificarAsync(It.IsAny<string>()))
                    .ReturnsAsync((CategoriaReclamacao.Taxas, 0.7m));

        await _sut.HandleAsync(CreateEvent(id));

        recla.ClassificadoPor.Should().Be(ClassificadoPor.Bedrock);
        recla.Categoria.Should().Be(CategoriaReclamacao.Taxas);
    }
}

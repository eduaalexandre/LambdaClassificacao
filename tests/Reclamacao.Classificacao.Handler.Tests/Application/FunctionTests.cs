using Xunit;
using Moq;
using FluentAssertions;
using Amazon.Lambda.Core;
using Amazon.Lambda.CloudWatchEvents;
using Microsoft.Extensions.Logging;
using Reclamacao.Classificacao.Handler.Application.Commands;
using Reclamacao.Classificacao.Handler.Application.Configuration;
using Reclamacao.Classificacao.Handler.Application.Events;
using Reclamacao.Classificacao.Handler.Application.Interfaces;

namespace Reclamacao.Classificacao.Handler.Tests.Application;

public class FunctionTests
{
    private readonly Mock<IReclamacaoRepository> _repoMock = new();
    private readonly Mock<IBedrockService> _bedrockMock = new();
    private readonly Mock<IKeywordClassifier> _keywordMock = new();
    private readonly Mock<IEventPublisher> _publisherMock = new();

    private Function CreateFunction()
    {
        var settings = new ClassificacaoSettings();
        var handler = new ClassificarReclamacaoCommandHandler(
            _repoMock.Object, _bedrockMock.Object, _keywordMock.Object,
            _publisherMock.Object, Mock.Of<ILogger<ClassificarReclamacaoCommandHandler>>(), settings);
        return new Function(handler, Mock.Of<ILogger<Function>>());
    }

    [Fact]
    public async Task FunctionHandler_ComEventoValido_DeveDelegarAoHandler()
    {
        var id = Guid.NewGuid();
        var detail = new ReclamacaoRecebidaEvent(id, "Digital", "User", "123", "a@b.com", "Texto", "Recebida", DateTimeOffset.UtcNow);
        var input = new CloudWatchEvent<ReclamacaoRecebidaEvent> { Detail = detail };
        var context = new Mock<ILambdaContext>();
        context.Setup(c => c.AwsRequestId).Returns("test-123");

        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Reclamacao.Classificacao.Handler.Domain.Entities.Reclamacao?)null);

        var function = CreateFunction();
        await function.FunctionHandler(input, context.Object);

        _repoMock.Verify(r => r.GetByIdAsync(id), Times.Once);
    }

    [Fact]
    public async Task FunctionHandler_SemDetail_DeveIgnorar()
    {
        var input = new CloudWatchEvent<ReclamacaoRecebidaEvent> { Detail = null! };
        var context = new Mock<ILambdaContext>();
        context.Setup(c => c.AwsRequestId).Returns("test-456");

        var function = CreateFunction();
        await function.FunctionHandler(input, context.Object);

        _repoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
    }
}

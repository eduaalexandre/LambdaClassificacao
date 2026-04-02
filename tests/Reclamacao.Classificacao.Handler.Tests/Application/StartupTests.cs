using Xunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Reclamacao.Classificacao.Handler.Application.Commands;
using Reclamacao.Classificacao.Handler.Application.Configuration;
using Reclamacao.Classificacao.Handler.Application.Interfaces;

namespace Reclamacao.Classificacao.Handler.Tests.Application;

public class StartupTests
{
    [Fact]
    public void ConfigureServices_RegistraTodosOsServicos()
    {
        // Set AWS_REGION so SDK clients can be constructed
        Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");

        var services = new ServiceCollection();
        Startup.ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        provider.GetService<ClassificacaoSettings>().Should().NotBeNull();
        provider.GetService<IReclamacaoRepository>().Should().NotBeNull();
        provider.GetService<IBedrockService>().Should().NotBeNull();
        provider.GetService<IKeywordClassifier>().Should().NotBeNull();
        provider.GetService<IEventPublisher>().Should().NotBeNull();
        provider.GetService<ClassificarReclamacaoCommandHandler>().Should().NotBeNull();

        // Cleanup
        Environment.SetEnvironmentVariable("AWS_REGION", null);
    }

    [Fact]
    public void ConfigureServices_SettingsDevemTerValoresPadrao()
    {
        var services = new ServiceCollection();
        Startup.ConfigureServices(services);
        var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<ClassificacaoSettings>();

        settings.BedrockConfidenceThreshold.Should().Be(0.7m);
        settings.KeywordFallbackMinScore.Should().Be(0.1m);
    }
}

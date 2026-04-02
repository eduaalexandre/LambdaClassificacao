using Amazon.BedrockRuntime;
using Amazon.DynamoDBv2;
using Amazon.EventBridge;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Reclamacao.Classificacao.Handler.Application.Commands;
using Reclamacao.Classificacao.Handler.Application.Configuration;
using Reclamacao.Classificacao.Handler.Application.Interfaces;
using Reclamacao.Classificacao.Handler.Infrastructure.Bedrock;
using Reclamacao.Classificacao.Handler.Infrastructure.DynamoDB;
using Reclamacao.Classificacao.Handler.Infrastructure.EventBridge;
using Reclamacao.Classificacao.Handler.Infrastructure.Fallback;

namespace Reclamacao.Classificacao.Handler;

public static class Startup
{
    private static readonly Lazy<IServiceProvider> LazyProvider = new(BuildServiceProvider, isThreadSafe: true);

    public static IServiceProvider ServiceProvider => LazyProvider.Value;

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        return services.BuildServiceProvider();
    }

    public static void ConfigureServices(IServiceCollection services)
    {
        // Settings
        var settings = new ClassificacaoSettings
        {
            BedrockConfidenceThreshold = ParseDecimalEnv("BEDROCK_CONFIDENCE_THRESHOLD", 0.7m),
            KeywordFallbackMinScore = ParseDecimalEnv("KEYWORD_FALLBACK_MIN_SCORE", 0.1m)
        };
        services.AddSingleton(settings);

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // AWS SDK Clients
        services.AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>();
        services.AddSingleton<IAmazonEventBridge, AmazonEventBridgeClient>();
        services.AddSingleton<IAmazonBedrockRuntime, AmazonBedrockRuntimeClient>();

        // Repositories & Services
        services.AddTransient<IReclamacaoRepository, ReclamacaoRepository>();
        services.AddTransient<IBedrockService, BedrockClassifier>();
        services.AddTransient<IKeywordClassifier, KeywordClassifier>();
        services.AddTransient<IEventPublisher, EventBridgePublisher>();

        // Handler
        services.AddTransient<ClassificarReclamacaoCommandHandler>();
    }

    private static decimal ParseDecimalEnv(string name, decimal defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return decimal.TryParse(value, out var parsed) ? parsed : defaultValue;
    }
}

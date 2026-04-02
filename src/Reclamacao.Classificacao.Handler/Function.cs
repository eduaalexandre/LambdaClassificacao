using System.Text.Json;
using Amazon.Lambda.CloudWatchEvents;
using Amazon.Lambda.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Reclamacao.Classificacao.Handler.Application.Commands;
using Reclamacao.Classificacao.Handler.Application.Events;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Reclamacao.Classificacao.Handler;

public class Function
{
    private readonly ClassificarReclamacaoCommandHandler _handler;
    private readonly ILogger<Function> _logger;

    /// <summary>
    /// Parameterless constructor for Lambda runtime — resolves dependencies via DI.
    /// </summary>
    public Function()
    {
        _handler = Startup.ServiceProvider.GetRequiredService<ClassificarReclamacaoCommandHandler>();
        _logger = Startup.ServiceProvider.GetRequiredService<ILogger<Function>>();
    }

    /// <summary>
    /// Constructor for unit testing — inject handler and logger directly.
    /// </summary>
    public Function(ClassificarReclamacaoCommandHandler handler, ILogger<Function> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    /// <summary>
    /// Lambda entry point. Receives an EventBridge event containing a ReclamacaoRecebidaEvent.
    /// </summary>
    public async Task FunctionHandler(CloudWatchEvent<ReclamacaoRecebidaEvent> input, ILambdaContext context)
    {
        _logger.LogInformation("Lambda invocada. RequestId: {RequestId}", context.AwsRequestId);

        if (input?.Detail == null)
        {
            _logger.LogError("Evento recebido sem Detail. Ignorando.");
            return;
        }

        _logger.LogInformation("Processando evento reclamacao.recebida para {ReclamacaoId}", input.Detail.ReclamacaoId);

        await _handler.HandleAsync(input.Detail);

        _logger.LogInformation("Processamento concluído para {ReclamacaoId}", input.Detail.ReclamacaoId);
    }
}

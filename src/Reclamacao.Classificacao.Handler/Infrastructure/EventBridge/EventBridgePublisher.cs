using System.Text.Json;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Microsoft.Extensions.Logging;
using Reclamacao.Classificacao.Handler.Application.Interfaces;

namespace Reclamacao.Classificacao.Handler.Infrastructure.EventBridge;

public class EventBridgePublisher : IEventPublisher
{
    private readonly IAmazonEventBridge _eventBridge;
    private readonly ILogger<EventBridgePublisher> _logger;
    private readonly string _eventBusName;
    private readonly string _eventSource;

    public EventBridgePublisher(IAmazonEventBridge eventBridge, ILogger<EventBridgePublisher> logger)
    {
        _eventBridge = eventBridge;
        _logger = logger;
        _eventBusName = Environment.GetEnvironmentVariable("EVENT_BUS_NAME") ?? "YOUR_EVENT_BUS_NAME_HERE";
        _eventSource = Environment.GetEnvironmentVariable("EVENT_SOURCE") ?? "reclamacao.classificacao";
    }

    public async Task PublishAsync<T>(T @event) where T : class
    {
        var detailType = typeof(T).Name;
        var detail = JsonSerializer.Serialize(@event);

        _logger.LogInformation("Publicando evento {DetailType} no EventBridge (bus: {Bus})", detailType, _eventBusName);

        var response = await _eventBridge.PutEventsAsync(new PutEventsRequest
        {
            Entries = new List<PutEventsRequestEntry>
            {
                new PutEventsRequestEntry
                {
                    Source = _eventSource,
                    DetailType = detailType,
                    Detail = detail,
                    EventBusName = _eventBusName
                }
            }
        });

        if (response.FailedEntryCount > 0)
        {
            var failedEntry = response.Entries.First(e => !string.IsNullOrEmpty(e.ErrorCode));
            _logger.LogError("Falha ao publicar evento: {ErrorCode} - {ErrorMessage}", failedEntry.ErrorCode, failedEntry.ErrorMessage);
            throw new InvalidOperationException($"Falha ao publicar evento no EventBridge: {failedEntry.ErrorCode} - {failedEntry.ErrorMessage}");
        }

        _logger.LogInformation("Evento {DetailType} publicado com sucesso (EventId: {EventId})",
            detailType, response.Entries.First().EventId);
    }
}

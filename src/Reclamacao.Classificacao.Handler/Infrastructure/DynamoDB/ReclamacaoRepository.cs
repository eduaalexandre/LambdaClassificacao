using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Reclamacao.Classificacao.Handler.Application.Interfaces;
using Reclamacao.Classificacao.Handler.Domain.Entities;
using Reclamacao.Classificacao.Handler.Domain.Enums;
using Reclamacao.Classificacao.Handler.Domain.Exceptions;
using ReclamacaoEntity = Reclamacao.Classificacao.Handler.Domain.Entities.Reclamacao;

namespace Reclamacao.Classificacao.Handler.Infrastructure.DynamoDB;

public class ReclamacaoRepository : IReclamacaoRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly ILogger<ReclamacaoRepository> _logger;
    private readonly string _tableName;

    public ReclamacaoRepository(IAmazonDynamoDB dynamoDb, ILogger<ReclamacaoRepository> logger)
    {
        _dynamoDb = dynamoDb;
        _logger = logger;
        _tableName = Environment.GetEnvironmentVariable("DYNAMODB_TABLE_NAME") ?? "Reclamacoes";
    }

    public async Task<ReclamacaoEntity?> GetByIdAsync(Guid id)
    {
        _logger.LogInformation("Consultando reclamação {ReclamacaoId} no DynamoDB", id);

        var response = await _dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["ReclamacaoId"] = new AttributeValue { S = id.ToString() }
            }
        });

        if (response.Item == null || response.Item.Count == 0)
        {
            _logger.LogWarning("Reclamação {ReclamacaoId} não encontrada no DynamoDB", id);
            return null;
        }

        var item = response.Item;
        var status = Enum.Parse<StatusReclamacao>(item["Status"].S);
        var version = int.Parse(item["Version"].N);

        CategoriaReclamacao? categoria = item.ContainsKey("Categoria")
            ? Enum.Parse<CategoriaReclamacao>(item["Categoria"].S)
            : null;

        decimal? confianca = item.ContainsKey("ConfiancaScore")
            ? decimal.Parse(item["ConfiancaScore"].N)
            : null;

        ClassificadoPor? classificadoPor = item.ContainsKey("ClassificadoPor")
            ? Enum.Parse<ClassificadoPor>(item["ClassificadoPor"].S)
            : null;

        DateTimeOffset? classificadoEm = item.ContainsKey("ClassificadoEm")
            ? DateTimeOffset.Parse(item["ClassificadoEm"].S)
            : null;

        return ReclamacaoEntity.Reconstituir(id, status, version, categoria, confianca, classificadoPor, classificadoEm);
    }

    public async Task UpdateAsync(ReclamacaoEntity reclamacao)
    {
        _logger.LogInformation("Atualizando reclamação {ReclamacaoId} para status {Status} (version {Version})",
            reclamacao.ReclamacaoId, reclamacao.Status, reclamacao.Version);

        try
        {
            await _dynamoDb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["ReclamacaoId"] = new AttributeValue { S = reclamacao.ReclamacaoId.ToString() }
                },
                UpdateExpression = "SET #status = :status, Categoria = :categoria, ConfiancaScore = :score, ClassificadoPor = :por, ClassificadoEm = :em, Version = :newVersion",
                ConditionExpression = "#status = :expectedStatus AND Version = :expectedVersion",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#status"] = "Status"
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":status"] = new AttributeValue { S = reclamacao.Status.ToString() },
                    [":categoria"] = new AttributeValue { S = reclamacao.Categoria.ToString()! },
                    [":score"] = new AttributeValue { N = reclamacao.ConfiancaScore!.Value.ToString("G") },
                    [":por"] = new AttributeValue { S = reclamacao.ClassificadoPor.ToString()! },
                    [":em"] = new AttributeValue { S = reclamacao.ClassificadoEm!.Value.ToString("O") },
                    [":newVersion"] = new AttributeValue { N = reclamacao.Version.ToString() },
                    [":expectedStatus"] = new AttributeValue { S = StatusReclamacao.Recebida.ToString() },
                    [":expectedVersion"] = new AttributeValue { N = (reclamacao.Version - 1).ToString() }
                }
            });

            _logger.LogInformation("Reclamação {ReclamacaoId} atualizada com sucesso", reclamacao.ReclamacaoId);
        }
        catch (ConditionalCheckFailedException ex)
        {
            _logger.LogWarning(ex, "Conflito de versão ou status ao atualizar reclamação {ReclamacaoId}", reclamacao.ReclamacaoId);
            throw new ConflictException($"Conflito ao atualizar reclamação {reclamacao.ReclamacaoId}: {ex.Message}");
        }
    }
}

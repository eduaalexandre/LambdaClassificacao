using System.Text.Json;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Microsoft.Extensions.Logging;
using Reclamacao.Classificacao.Handler.Application.Interfaces;
using Reclamacao.Classificacao.Handler.Domain.Enums;

namespace Reclamacao.Classificacao.Handler.Infrastructure.Bedrock;

public class BedrockClassifier : IBedrockService
{
    private readonly IAmazonBedrockRuntime _bedrock;
    private readonly ILogger<BedrockClassifier> _logger;
    private readonly string _modelId;

    private const string PromptTemplate = @"Você é um classificador de reclamações bancárias. Analise o texto da reclamação abaixo e classifique-a em exatamente uma das seguintes categorias:
- Fraude
- Taxas
- Atendimento
- Produto
- Outros

Responda APENAS com um JSON válido no seguinte formato, sem texto adicional:
{{""categoria"": ""<categoria>"", ""confiancaScore"": <valor entre 0.0 e 1.0>}}

Texto da reclamação:
{0}";

    public BedrockClassifier(IAmazonBedrockRuntime bedrock, ILogger<BedrockClassifier> logger)
    {
        _bedrock = bedrock;
        _logger = logger;
        _modelId = Environment.GetEnvironmentVariable("BEDROCK_MODEL_ID") ?? "YOUR_BEDROCK_MODEL_ID_HERE";
    }

    public async Task<(CategoriaReclamacao Categoria, decimal Confianca)> ClassificarAsync(string texto)
    {
        _logger.LogInformation("Classificando texto via Bedrock (model: {ModelId})", _modelId);

        var prompt = string.Format(PromptTemplate, texto);

        var requestBody = JsonSerializer.Serialize(new
        {
            anthropic_version = "bedrock-2023-05-31",
            max_tokens = 256,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        });

        var request = new InvokeModelRequest
        {
            ModelId = _modelId,
            ContentType = "application/json",
            Accept = "application/json",
            Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(requestBody))
        };

        var response = await _bedrock.InvokeModelAsync(request);

        using var reader = new StreamReader(response.Body);
        var responseJson = await reader.ReadToEndAsync();

        _logger.LogDebug("Resposta do Bedrock: {Response}", responseJson);

        var bedrockResponse = JsonDocument.Parse(responseJson);
        var contentText = bedrockResponse.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString()!;

        var classification = JsonDocument.Parse(contentText);
        var categoriaStr = classification.RootElement.GetProperty("categoria").GetString()!;
        var confianca = classification.RootElement.GetProperty("confiancaScore").GetDecimal();

        if (!Enum.TryParse<CategoriaReclamacao>(categoriaStr, ignoreCase: true, out var categoria))
        {
            _logger.LogWarning("Categoria inválida retornada pelo Bedrock: {Categoria}", categoriaStr);
            categoria = CategoriaReclamacao.Outros;
            confianca = 0m;
        }

        _logger.LogInformation("Bedrock classificou como {Categoria} com confiança {Score}", categoria, confianca);
        return (categoria, confianca);
    }
}

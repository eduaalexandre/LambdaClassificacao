## **1. Título do Requisito**

REQ-B: Normalização e Classificação Automática de Reclamações

## **2. Contexto e Problema de Negócio**

Este requisito implementa a classificação automática de reclamações, um passo crucial para o cumprimento do SLA de 10 dias. Consumindo o evento `reclamacao.recebida`, o sistema categoriza a reclamação usando o Amazon Bedrock e, em caso de baixa confiança ou falha, um fallback por palavras-chave com scoring ponderado. A reclamação é então atualizada no DynamoDB e um evento `reclamacao.classificada` é publicado, alimentando os consumidores downstream.

## **3. Contrato de Entrada (Input)**

O Lambda Classificacao Handler (REQ-B) consome o evento `reclamacao.recebida` do EventBridge.

```csharp
public record ReclamacaoRecebidaEvent(
    Guid ReclamacaoId, // UUID v4 gerado na ingestão
    string Canal, // "Digital" | "Fisico"
    string NomeReclamante, // Nome completo normalizado
    string Cpf, // Formato: "000.000.000-00" — chave de correlação com o Data Mesh
    string Email, // Formato RFC 5321
    string TextoReclamacao, // Texto livre, máx 5000 chars
    string Status, // Sempre "Recebida" neste evento
    DateTimeOffset RecebidoEm // UTC
);
```

## **4. Contrato de Saída (Output)**

O Lambda Classificacao Handler (REQ-B) publica o evento `reclamacao.classificada` no EventBridge.

```csharp
public record ReclamacaoClassificadaEvent(
    Guid ReclamacaoId, // Mesmo UUID do evento anterior
    string Canal, // "Digital" | "Fisico" — propagado
    string Categoria, // "Fraude" | "Taxas" | "Atendimento" | "Produto" | "Outros"
    decimal ConfiancaScore, // 0.0 a 1.0
    string ClassificadoPor, // "Bedrock" | "KeywordFallback"
    string Status, // Sempre "Classificada" neste evento
    DateTimeOffset ClassificadoEm // UTC
);
```

## **5. Eventos Publicados e Consumidos**

*   **Consome:** `reclamacao.recebida`
*   **Publica:** `reclamacao.classificada`

## **6. Fluxo Feliz (Happy Path)**

1.  **Recebimento do Evento:** O Lambda Classificacao Handler é invocado pelo EventBridge ao receber um evento `reclamacao.recebida`.
2.  **Validação e Deserialização:** O payload do evento é validado e deserializado para o `ReclamacaoRecebidaEvent`.
3.  **Consulta da Reclamação:** A entidade `Reclamacao` é lida do DynamoDB usando `ReclamacaoId` para obter a `Version` atual e garantir o locking otimista.
4.  **Classificação via Bedrock:**
    *   O `TextoReclamacao` é enviado ao Amazon Bedrock com o prompt estruturado definido no Research.
    *   O Bedrock retorna a `categoria` e o `confiancaScore` em formato JSON.
    *   Se o `confiancaScore` retornado for maior ou igual ao `BEDROCK_CONFIDENCE_THRESHOLD` (0.7), a classificação do Bedrock é aceita.
5.  **Fallback por Palavras-Chave (se necessário):**
    *   Se o Bedrock falhar (ex: erro de API, timeout) ou se o `confiancaScore` for menor que o `BEDROCK_CONFIDENCE_THRESHOLD`, o sistema aciona o fallback por palavras-chave.
    *   O `TextoReclamacao` é normalizado (minúsculas, sem acentos/caracteres especiais).
    *   Para cada `CategoriaReclamacao`, o `KeywordScore` é calculado: (contagem de palavras-chave da categoria no texto) / (total de palavras-chave no dicionário da categoria).
    *   A categoria com o `KeywordScore` mais alto é selecionada.
    *   Se o `KeywordScore` da categoria vencedora for maior ou igual ao `KEYWORD_FALLBACK_MIN_SCORE` (0.1), essa classificação é aceita.
    *   Caso contrário (nenhuma categoria atingiu o score mínimo), a categoria é definida como `Outros` e o `ConfiancaScore` como `0.0`.
6.  **Atualização no DynamoDB:**
    *   A entidade `Reclamacao` no DynamoDB é atualizada com a `Categoria`, `ConfiancaScore`, `ClassificadoPor` (Bedrock ou KeywordFallback), `ClassificadoEm` e o `Status` para `Classificada`.
    *   A atualização utiliza `UpdateItem` com `ConditionExpression` verificando o `Status` atual (`Recebida`) e a `Version` para garantir o locking otimista (ADR-04). A `Version` é incrementada.
7.  **Publicação do Evento:** O evento `reclamacao.classificada` é publicado no EventBridge com os dados da classificação.

## **7. Fluxos de Exceção**

1.  **Reclamação já Classificada:**
    *   **Cenário:** O Lambda é invocado com um `ReclamacaoId` que já possui `Status = Classificada` (ou outro status diferente de `Recebida`) no DynamoDB.
    *   **Comportamento:** O `UpdateItem` no DynamoDB falhará com `ConditionalCheckFailedException`. O Lambda deve capturar essa exceção, logar como idempotência legítima e encerrar com sucesso, sem republicar o evento.
    *   **Justificativa:** Garante idempotência (ADR-04) e evita reprocessamento desnecessário.
2.  **Falha na Chamada ao Bedrock:**
    *   **Cenário:** A chamada ao Amazon Bedrock falha (ex: timeout, erro de serviço, credenciais inválidas).
    *   **Comportamento:** O Lambda deve capturar a exceção, logar o erro e acionar o fallback por palavras-chave. Se o fallback também não conseguir classificar com confiança, a reclamação será classificada como `Outros` com `ConfiancaScore = 0.0`.
    *   **Justificativa:** Prioriza a resiliência e garante que a reclamação seja classificada mesmo com problemas no serviço de IA.
3.  **Falha na Deserialização do Retorno do Bedrock:**
    *   **Cenário:** O Bedrock retorna um JSON inválido ou em um formato inesperado.
    *   **Comportamento:** O Lambda deve capturar a exceção de parsing, logar o erro e acionar o fallback por palavras-chave.
    *   **Justificativa:** Resiliência contra respostas inesperadas do modelo de IA.
4.  **Falha na Atualização do DynamoDB (Conflito de Versão):**
    *   **Cenário:** Outra operação atualizou a mesma reclamação no DynamoDB entre a leitura e a tentativa de escrita, resultando em uma `ConditionalCheckFailedException` não relacionada à idempotência (ex: status diferente de `Recebida` e `Version` diferente da esperada).
    *   **Comportamento:** O Lambda deve lançar a exceção, o que fará com que a mensagem seja reenviada para a fila SQS (se configurada) e, após `MaxReceiveCount` tentativas, caia na DLQ. O Lambda DLQ Consumer (ADR-09) tratará a falha, atualizando o status para `Falhou` e publicando `reclamacao.falhou`.
    *   **Justificativa:** Garante a integridade dos dados e o tratamento de falhas de processamento.
5.  **Falha na Publicação do Evento no EventBridge:**
    *   **Cenário:** O EventBridge não consegue aceitar o evento `reclamacao.classificada` (ex: erro de serviço, schema inválido).
    *   **Comportamento:** O Lambda deve lançar a exceção. A mensagem será reenviada para a fila SQS e, após `MaxReceiveCount` tentativas, cairá na DLQ. O Lambda DLQ Consumer (ADR-09) tratará a falha.
    *   **Justificativa:** Garante a entrega do evento ou o tratamento explícito da falha.

## **8. Definition of Done (DoD)**

*   O Lambda Classificacao Handler está implementado em C# (.NET).
*   O Lambda consome o evento `reclamacao.recebida` do EventBridge.
*   A classificação principal é feita via Amazon Bedrock, utilizando o prompt definido.
*   Um fallback por palavras-chave com scoring ponderado é implementado para casos de baixa confiança ou falha do Bedrock.
*   O dicionário de palavras-chave está definido e normalizado.
*   Os thresholds `BEDROCK_CONFIDENCE_THRESHOLD` e `KEYWORD_FALLBACK_MIN_SCORE` são configuráveis via variáveis de ambiente.
*   A reclamação é classificada como `Outros` com `ConfiancaScore = 0.0` se nenhuma classificação confiável for encontrada.
*   A entidade `Reclamacao` no DynamoDB é atualizada com `Categoria`, `ConfiancaScore`, `ClassificadoPor`, `ClassificadoEm` e `Status = Classificada`, utilizando locking otimista.
*   O evento `reclamacao.classificada` é publicado no EventBridge.
*   Todos os fluxos de exceção são tratados conforme especificado, incluindo idempotência e tratamento de falhas.
*   Testes unitários cobrem o happy path e os fluxos de exceção.
*   Testes de integração validam a interação com DynamoDB, Bedrock (mockado ou ambiente de teste) e EventBridge (mockado).
*   Métricas e logs são configurados para monitoramento.
*   O código segue as boas práticas de Clean Architecture e DDD.
*   A documentação (esta Spec) está completa e aprovada.

## **9. Plan (Planejamento)**

*Esta seção será preenchida na próxima fase, após a aprovação desta Spec.*
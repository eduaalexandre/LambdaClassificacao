using Reclamacao.Classificacao.Handler.Domain.Enums;
using Reclamacao.Classificacao.Handler.Application.Interfaces;
using Reclamacao.Classificacao.Handler.Application.Configuration;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Reclamacao.Classificacao.Handler.Infrastructure.Fallback;

public class KeywordClassifier : IKeywordClassifier
{
    private readonly decimal _minScore;

    private static readonly Dictionary<CategoriaReclamacao, string[]> Dicionario = new()
    {
        [CategoriaReclamacao.Fraude] = new[] { "fraude", "golpe", "clonado", "indevida", "desconheco", "cartao" },
        [CategoriaReclamacao.Taxas] = new[] { "taxa", "juros", "anuidade", "cobranca", "abusiva", "tarifa" },
        [CategoriaReclamacao.Atendimento] = new[] { "atendimento", "demora", "rude", "grosseria", "espera", "fila" },
        [CategoriaReclamacao.Produto] = new[] { "produto", "quebrado", "defeito", "estorno", "entrega", "atraso" }
    };

    public KeywordClassifier(ClassificacaoSettings settings)
    {
        _minScore = settings.KeywordFallbackMinScore;
    }

    public (CategoriaReclamacao Categoria, decimal Score) Classificar(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return (CategoriaReclamacao.Outros, 0);

        var normalizado = Normalizar(texto);
        var scores = new Dictionary<CategoriaReclamacao, decimal>();

        foreach (var entry in Dicionario)
        {
            var categoria = entry.Key;
            var keywords = entry.Value;
            var matches = keywords.Count(k => normalizado.Contains(k));
            var score = (decimal)matches / keywords.Length;
            scores[categoria] = score;
        }

        var melhor = scores.OrderByDescending(s => s.Value).FirstOrDefault();

        if (melhor.Value >= _minScore)
        {
            return (melhor.Key, melhor.Value);
        }

        return (CategoriaReclamacao.Outros, 0);
    }

    private static string Normalizar(string texto)
    {
        var lower = texto.ToLowerInvariant();
        var normalized = lower.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        var semAcentos = sb.ToString().Normalize(NormalizationForm.FormC);
        return Regex.Replace(semAcentos, @"[^\w\s]", "");
    }
}

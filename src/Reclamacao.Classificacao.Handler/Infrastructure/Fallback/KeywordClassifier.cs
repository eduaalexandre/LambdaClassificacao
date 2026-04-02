using Reclamacao.Classificacao.Handler.Domain.Enums;
using Reclamacao.Classificacao.Handler.Application.Interfaces;
using System.Text.RegularExpressions;

namespace Reclamacao.Classificacao.Handler.Infrastructure.Fallback;

public class KeywordClassifier : IKeywordClassifier
{
    private static readonly Dictionary<CategoriaReclamacao, string[]> Dicionario = new()
    {
        [CategoriaReclamacao.Fraude] = new[] { "fraude", "golpe", "clonado", "indevida", "desconheço", "cartao" },
        [CategoriaReclamacao.Taxas] = new[] { "taxa", "juros", "anuidade", "cobrança", "abusiva", "tarifa" },
        [CategoriaReclamacao.Atendimento] = new[] { "atendimento", "demora", "rude", "grosseria", "espera", "fila" },
        [CategoriaReclamacao.Produto] = new[] { "produto", "quebrado", "defeito", "estorno", "entrega", "atraso" }
    };

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

        if (melhor.Value >= 0.1m)
        {
            return (melhor.Key, melhor.Value);
        }

        return (CategoriaReclamacao.Outros, 0);
    }

    private string Normalizar(string texto)
    {
        return Regex.Replace(texto.ToLower(), @"[^\w\s]", "");
    }
}

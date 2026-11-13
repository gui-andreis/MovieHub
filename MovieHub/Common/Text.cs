using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MovieHub.Common;

public static partial class TextNormalizer
{
    /// <summary>Minúsculo e sem acentos — base da busca "accent-insensitive".</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return MultiSpace().Replace(sb.ToString().Normalize(NormalizationForm.FormC), " ");
    }

    public static string SearchText(params string?[] parts)
    {
        var normalized = parts.Select(Normalize).Where(p => p.Length > 0).Distinct();
        var text = string.Join(" | ", normalized);
        return text.Length > 700 ? text[..700] : text;
    }

    public static string Slugify(string value)
    {
        var normalized = Normalize(value);
        var slug = NonSlug().Replace(normalized, "-").Trim('-');
        return slug.Length == 0 ? "genero" : slug;
    }

    public static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max) return value ?? string.Empty;
        var cut = value[..max];
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > max * 0.6) cut = cut[..lastSpace];
        return cut.TrimEnd(',', '.', ';', ' ') + "…";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpace();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonSlug();
}

/// <summary>Classificação indicativa brasileira (ClassInd).</summary>
public static class AgeRatings
{
    public static readonly string[] All = { "L", "10", "12", "14", "16", "18" };

    public static bool IsValid(string? rating) => rating != null && All.Contains(rating);

    /// <summary>L = 0, demais = idade. Null quando desconhecida.</summary>
    public static int? Rank(string? rating) => rating switch
    {
        "L" => 0,
        "10" => 10,
        "12" => 12,
        "14" => 14,
        "16" => 16,
        "18" => 18,
        _ => null
    };

    /// <summary>Normaliza certificações do TMDB (BR, com fallback de valores dos EUA).</summary>
    public static string? FromCertification(string? certification)
    {
        if (string.IsNullOrWhiteSpace(certification)) return null;
        var c = certification.Trim().ToUpperInvariant();
        return c switch
        {
            "L" or "AL" or "LIVRE" or "ER" => "L",
            "10" or "A10" => "10",
            "12" or "A12" => "12",
            "14" or "A14" => "14",
            "16" or "A16" => "16",
            "18" or "A18" => "18",
            "G" or "TV-Y" or "TV-G" => "L",
            "PG" or "TV-Y7" or "TV-Y7-FV" or "TV-PG" => "10",
            "PG-13" => "12",
            "TV-14" => "14",
            "R" => "16",
            "NC-17" or "TV-MA" => "18",
            _ => null
        };
    }

    /// <summary>
    /// Limite efetivo de um perfil. Null = sem restrição (18 e não-infantil).
    /// Perfis infantis nunca passam de 10 anos.
    /// </summary>
    public static int? EffectiveLimit(string maturityLimit, bool isKids)
    {
        var rank = Rank(maturityLimit) ?? 18;
        if (isKids) rank = Math.Min(rank, 10);
        return rank >= 18 ? null : rank;
    }
}

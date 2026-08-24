using System.Text;

namespace Spread.Api.Domain.Companies;

public sealed record CompanySearchQuery
{
    public const int MinimumLength = 2;
    public const int MaximumLength = 64;

    private CompanySearchQuery(string value) => Value = value;

    public string Value { get; }

    public static bool TryCreate(string? rawValue, out CompanySearchQuery? query)
    {
        query = null;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var normalized = CollapseWhitespace(rawValue.Trim());
        if (normalized.Length is < MinimumLength or > MaximumLength
            || normalized.Any(character => !IsAllowed(character)))
        {
            return false;
        }

        query = new CompanySearchQuery(normalized);
        return true;
    }

    private static bool IsAllowed(char character)
        => character is >= 'A' and <= 'Z'
            or >= 'a' and <= 'z'
            or >= '0' and <= '9'
            or ' ' or '.' or '-' or '\'' or '&';

    private static string CollapseWhitespace(string value)
    {
        var result = new StringBuilder(value.Length);
        var previousWasSpace = false;
        foreach (var character in value)
        {
            var isSpace = character == ' ';
            if (!isSpace || !previousWasSpace)
            {
                result.Append(character);
            }

            previousWasSpace = isSpace;
        }

        return result.ToString();
    }
}

public sealed record CompanySearchResult(
    string Ticker,
    string CompanyName,
    string? Exchange,
    string? Currency,
    string Provider);

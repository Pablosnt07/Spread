namespace Spread.Api.Domain.MarketData;

public enum HistoricalPriceRange
{
    YearToDate,
    OneYear,
    ThreeYears,
    FiveYears,
    Maximum
}

public sealed record HistoricalPricePoint(DateOnly Date, decimal Price);

public sealed record HistoricalPriceSeries(
    string Ticker,
    HistoricalPriceRange Range,
    IReadOnlyList<HistoricalPricePoint> Points,
    DateTimeOffset FetchedAt,
    string Provider);

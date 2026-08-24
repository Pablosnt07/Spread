using System.Diagnostics.Metrics;

namespace Spread.Api.Infrastructure.Observability;

public static class CompanySearchMetrics
{
    private static readonly Meter Meter = new("Spread.CompanySearch", "1.0.0");
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>("spread.company_search.requests");
    private static readonly Counter<long> Rejections = Meter.CreateCounter<long>("spread.company_search.rejections");
    private static readonly Counter<long> CacheHits = Meter.CreateCounter<long>("spread.company_search.cache_hits");
    private static readonly Counter<long> ProviderCalls = Meter.CreateCounter<long>("spread.company_search.provider_calls");
    private static readonly Histogram<double> ProviderDuration = Meter.CreateHistogram<double>("spread.company_search.provider_duration", "ms");

    public static void RecordRequest() => Requests.Add(1);

    public static void RecordRejection(string reason) => Rejections.Add(1, new KeyValuePair<string, object?>("reason", reason));

    public static void RecordCacheHit() => CacheHits.Add(1);

    public static void RecordProviderCall() => ProviderCalls.Add(1);

    public static void RecordProviderDuration(TimeSpan elapsed) => ProviderDuration.Record(elapsed.TotalMilliseconds);
}

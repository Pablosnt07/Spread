using Microsoft.Extensions.Options;
using Spread.Api.Configuration;

namespace Spread.Api.Features.Methodology;

public static class MethodologyEndpoints
{
    private static readonly string[] Principles =
    [
        "Missing data is never converted to zero.",
        "The fundamental score is independent of the user profile.",
        "Unsupported asset types require a dedicated scoring model.",
        "Market signals do not alter the fundamental score."
    ];

    public static IEndpointRouteBuilder MapMethodologyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/methodology", (IOptions<ScoringOptions> options) =>
            Results.Ok(new
            {
                options.Value.ModelVersion,
                scale = new { minimum = 0, maximum = 100 },
                options.Value.MinimumCoverage,
                options.Value.MinimumConfidence,
                dimensionWeights = options.Value.DimensionWeights,
                confidenceWeights = new
                {
                    coverage = 0.45m,
                    freshness = 0.20m,
                    peerQuality = 0.20m,
                    consistency = 0.15m
                },
                principles = Principles
            }))
            .RequireRateLimiting("public-read")
            .WithName("GetMethodology");

        return endpoints;
    }
}

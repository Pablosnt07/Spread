using Spread.Api.Domain.Assets;
using Spread.Api.Domain.Portfolios;
using Spread.Api.Services;

namespace Spread.Api.Features.Portfolios;

public static class PortfolioEndpoints
{
    public static IEndpointRouteBuilder MapPortfolioEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/portfolios/allocation-preview", (
            PortfolioAllocationRequest request,
            IPortfolioAllocationCalculator calculator) =>
        {
            var validationErrors = Validate(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(
                    validationErrors,
                    title: "Invalid portfolio",
                    type: "https://spread.local/problems/invalid-portfolio");
            }

            var positions = request.Positions!
                .Select(position => new PortfolioPosition(position!.Ticker!, position.InvestedAmount))
                .ToArray();
            var summary = calculator.Calculate(positions);

            return Results.Ok(PortfolioAllocationResponse.FromDomain(
                request.BaseCurrency!.Trim().ToUpperInvariant(),
                summary));
        })
        .RequireRateLimiting("public-read")
        .WithName("PreviewPortfolioAllocation");

        return endpoints;
    }

    private static Dictionary<string, string[]> Validate(PortfolioAllocationRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        var currency = request.BaseCurrency?.Trim();
        if (currency is null || currency.Length != 3 || currency.Any(character => !char.IsAsciiLetter(character)))
        {
            errors["baseCurrency"] = ["Base currency must contain exactly three ASCII letters."];
        }

        if (request.Positions is null)
        {
            errors["positions"] = ["Positions are required."];
            return errors;
        }

        if (request.Positions.Count > PortfolioAllocationCalculator.MaximumPositionCount)
        {
            errors["positions"] =
            [
                $"A portfolio cannot contain more than {PortfolioAllocationCalculator.MaximumPositionCount} positions."
            ];
            return errors;
        }

        for (var index = 0; index < request.Positions.Count; index++)
        {
            var position = request.Positions[index];
            if (position is null)
            {
                errors[$"positions[{index}]"] = ["Position is required."];
                continue;
            }

            if (!AssetIdentifier.TryCreate(position.Ticker, out _))
            {
                errors[$"positions[{index}].ticker"] = ["Ticker must contain 1 to 12 letters, numbers, dots, or hyphens."];
            }

            if (position.InvestedAmount <= 0m ||
                position.InvestedAmount > PortfolioAllocationCalculator.MaximumInvestedAmount)
            {
                errors[$"positions[{index}].investedAmount"] =
                [
                    $"Invested amount must be greater than zero and at most {PortfolioAllocationCalculator.MaximumInvestedAmount}."
                ];
            }
        }

        return errors;
    }
}

public sealed record PortfolioAllocationRequest(
    string? BaseCurrency,
    IReadOnlyList<PortfolioPositionRequest?>? Positions);

public sealed record PortfolioPositionRequest(string? Ticker, decimal InvestedAmount);

public sealed record PortfolioAllocationResponse(
    string BaseCurrency,
    int AssetCount,
    decimal TotalInvested,
    IReadOnlyList<PortfolioAllocationPositionResponse> Positions)
{
    public static PortfolioAllocationResponse FromDomain(
        string baseCurrency,
        PortfolioAllocationSummary summary)
        => new(
            baseCurrency,
            summary.AssetCount,
            summary.TotalInvested,
            [.. summary.Positions.Select(PortfolioAllocationPositionResponse.FromDomain)]);
}

public sealed record PortfolioAllocationPositionResponse(
    string Ticker,
    decimal InvestedAmount,
    decimal AllocationPercentage)
{
    public static PortfolioAllocationPositionResponse FromDomain(PortfolioAllocation allocation)
        => new(allocation.Ticker, allocation.InvestedAmount, allocation.AllocationPercentage);
}

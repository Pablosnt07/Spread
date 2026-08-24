using Spread.Api.Domain.Companies;

namespace Spread.Tests.Domain;

public sealed class CompanySearchQueryTests
{
    [Theory]
    [InlineData("AMD", "AMD")]
    [InlineData("  ServiceNow   Inc. ", "ServiceNow Inc.")]
    [InlineData("Berkshire Hathaway B", "Berkshire Hathaway B")]
    public void TryCreate_AcceptsAndNormalizesExpectedSearches(string input, string expected)
    {
        Assert.True(CompanySearchQuery.TryCreate(input, out var query));
        Assert.Equal(expected, query!.Value);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("AMD/../../secret")]
    [InlineData("empresa@example.com")]
    public void TryCreate_RejectsUnsafeOrOutOfRangeSearches(string input)
        => Assert.False(CompanySearchQuery.TryCreate(input, out _));
}

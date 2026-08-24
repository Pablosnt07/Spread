using Microsoft.Extensions.Options;
using Spread.Api.Configuration;

namespace Spread.Api.Providers.AlphaVantage;

public sealed class AlphaVantageApiKeyHandler(
    IOptions<AlphaVantageOptions> options) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var originalUri = request.RequestUri
            ?? throw new InvalidOperationException("Alpha Vantage request URI is required.");
        var separator = string.IsNullOrEmpty(originalUri.Query) ? "?" : "&";
        request.RequestUri = new Uri(
            $"{originalUri.AbsoluteUri}{separator}apikey={Uri.EscapeDataString(options.Value.ApiKey)}",
            UriKind.Absolute);

        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        finally
        {
            // Restore the redacted URI before outer HttpClient logging handlers finish.
            request.RequestUri = originalUri;
        }
    }
}

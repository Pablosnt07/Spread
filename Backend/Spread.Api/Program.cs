using System.Threading.RateLimiting;
using System.Net;
using System.Text.Json.Serialization;
using Spread.Api.Configuration;
using Spread.Api.Features.Companies;
using Spread.Api.Features.Methodology;
using Spread.Api.Features.Portfolios;
using Spread.Api.Infrastructure.Errors;
using Spread.Api.Providers;
using Spread.Api.Providers.AlphaVantage;
using Spread.Api.Providers.Fmp;
using Spread.Api.Providers.Insiders;
using Spread.Api.Scoring;
using Spread.Api.Services;
using Spread.Api.Infrastructure.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// Provider credentials can appear in query strings. Keep framework HTTP logs at warning
// and use Spread's structured, secret-free provider logs instead.
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddMemoryCache();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<ISpreadScoreCalculator, SpreadScoreCalculator>();
builder.Services.AddSingleton<IPortfolioAllocationCalculator, PortfolioAllocationCalculator>();
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services
    .AddOptions<ScoringOptions>()
    .Bind(builder.Configuration.GetSection(ScoringOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<FmpOptions>()
    .Bind(builder.Configuration.GetSection(FmpOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<AlphaVantageOptions>()
    .Bind(builder.Configuration.GetSection(AlphaVantageOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => !options.Enabled || !string.IsNullOrWhiteSpace(options.ApiKey),
        "Alpha Vantage API key is required when the provider is enabled.")
    .ValidateOnStart();

builder.Services
    .AddHttpClient<IFinancialDataProvider, FmpFinancialDataProvider>((services, client) =>
    {
        var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<FmpOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        client.DefaultRequestHeaders.Add("apikey", options.ApiKey);
    })
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    });

builder.Services.AddTransient<AlphaVantageApiKeyHandler>();
builder.Services
    .AddHttpClient<IInsiderTransactionProvider, AlphaVantageInsiderProvider>((services, client) =>
    {
        var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AlphaVantageOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    })
    .AddHttpMessageHandler<AlphaVantageApiKeyHandler>()
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    });

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        if (context.HttpContext.Request.Path.StartsWithSegments("/api/companies/search"))
        {
            CompanySearchMetrics.RecordRejection("rate_limited");
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests",
                Detail = "Please wait before trying again.",
                Type = "https://spread.local/problems/rate-limited"
            },
            cancellationToken);
    };
    options.AddPolicy("public-read", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("provider-read", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("company-search", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "spread-backend",
    utc = DateTimeOffset.UtcNow
}));

app.MapMethodologyEndpoints();
app.MapCompanyEndpoints();
app.MapPortfolioEndpoints();

app.Run();

using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using BookmarkIndexer.Core;
using BookmarkIndexer.Core.Model;
using BookmarkIndexer.Core.Modules.Auth;
using BookmarkIndexer.Integrations.Lightpanda;
using BookmarkIndexer.Integrations.StackExchange;
using BookmarkIndexer.Shared;
using BookmarkIndexer.Shared.Exceptions;
using BookmarkIndexer.Web.Workers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OllamaSharp;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services));

builder.Services.AddCore(builder.Configuration);
var lightpanda = new LightpandaPageFetcher(
    builder.Configuration["Lightpanda:CdpUrl"] ?? throw new InvalidOperationException("Lightpanda:CdpUrl is not configured."));
builder.Services.AddHttpClient<IPageFetcher, StackExchangePageFetcher>((http, _) => new StackExchangePageFetcher(http, lightpanda))
    // The Stack Exchange API always sends gzip.
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All });
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(new OllamaApiClient(
    builder.Configuration["Ollama:Url"] ?? throw new InvalidOperationException("Ollama:Url is not configured."),
    builder.Configuration["Ollama:EmbeddingModel"] ?? throw new InvalidOperationException("Ollama:EmbeddingModel is not configured.")));
builder.Services.AddHostedService<IndexWorker>();

// Auth:Enabled off (local runs): no login, every endpoint is open as before.
var authSection = builder.Configuration.GetSection("Auth");
var auth = authSection.Get<AuthSettings>() ?? new AuthSettings();
builder.Services.Configure<AuthSettings>(authSection);
builder.Services.AddHttpClient<OAuthClient>();
if (auth.Enabled)
{
    if (auth.JwtKey.Length == 0 || auth.ClientId.Length == 0 || auth.Authority.Length == 0)
    {
        throw new InvalidOperationException("Auth:Authority, Auth:ClientId and Auth:JwtKey must be configured when Auth:Enabled is true.");
    }
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            // mws signs access tokens with a shared HMAC key and sets no issuer or audience.
            ValidateIssuer = false,
            ValidateAudience = false,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.JwtKey)),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var token = ((JsonWebToken)context.SecurityToken).EncodedToken;
                var oauth = context.HttpContext.RequestServices.GetRequiredService<OAuthClient>();
                if (!await oauth.IsActive(token, context.HttpContext.RequestAborted))
                {
                    context.Fail("The token is revoked or was issued to another client.");
                }
            },
        };
    });
}

builder.Services.AddControllers(options =>
    {
        if (auth.Enabled)
        {
            options.Filters.Add(new AuthorizeFilter());
        }
    })
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    // MVC's problem details writer leaves context.Exception empty; the handled exception is on the feature.
    if (context.HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error is { } error and (BadRequestException or NotFoundException))
    {
        context.ProblemDetails.Detail = error.Message;
    }
});
builder.Services.AddHealthChecks();

var app = builder.Build();

// Single local user: the schema is brought up to date on every start, so run.ps1 needs no migration step.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception switch
    {
        BadRequestException => StatusCodes.Status400BadRequest,
        NotFoundException => StatusCodes.Status404NotFound,
        // The login provider is down or erroring (OAuthClient); a 401 would wrongly end the user's session.
        HttpRequestException => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError,
    },
});
app.UseSerilogRequestLogging();
if (auth.Enabled)
{
    app.UseAuthentication();
}
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/api/health");

await app.RunAsync();

using System.Text.Json.Serialization;
using BookmarkIndexer.Core;
using BookmarkIndexer.Core.Model;
using BookmarkIndexer.Integrations.Lightpanda;
using BookmarkIndexer.Shared;
using BookmarkIndexer.Shared.Exceptions;
using BookmarkIndexer.Web.Workers;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services));

builder.Services.AddCore(builder.Configuration);
builder.Services.AddSingleton<IPageFetcher>(new LightpandaPageFetcher(
    builder.Configuration["Lightpanda:CdpUrl"] ?? throw new InvalidOperationException("Lightpanda:CdpUrl is not configured.")));
builder.Services.AddHostedService<IndexWorker>();
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    // MVC's problem details writer leaves context.Exception empty; the handled exception is on the feature.
    if (context.HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error is BadRequestException exception)
    {
        context.ProblemDetails.Detail = exception.Message;
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
    StatusCodeSelector = exception => exception is BadRequestException ? StatusCodes.Status400BadRequest : StatusCodes.Status500InternalServerError,
});
app.UseSerilogRequestLogging();
app.MapControllers();
app.MapHealthChecks("/api/health");

await app.RunAsync();

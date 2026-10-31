using BookmarkIndexer.Core.Modules.Indexing;

namespace BookmarkIndexer.Web.Workers;

public class IndexWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<IndexWorker> logger) : BackgroundService
{
    static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(10);
    // Loading pages is mostly waiting on the network: 4 at a time indexed about 2.5x faster in a speed test.
    // Ollama embeds one request at a time, so more loops mainly add Lightpanda memory.
    const int Loops = 4;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.WhenAll(Enumerable.Range(0, Loops).Select(_ => IndexLoop()));

        async Task IndexLoop()
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                bool indexed;
                try
                {
                    using var scope = scopes.CreateScope();
                    indexed = await scope.ServiceProvider.GetRequiredService<IndexService>().IndexNext(stoppingToken);
                }
                catch (Exception e) when (!stoppingToken.IsCancellationRequested)
                {
                    // Postgres restarting must not stop indexing for good; try again after the pause.
                    logger.LogError(e, "Index loop failed");
                    indexed = false;
                }

                if (!indexed)
                {
                    await Task.Delay(IdleDelay, time, stoppingToken);
                }
            }
        }
    }
}

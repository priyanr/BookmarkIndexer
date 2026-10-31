using Microsoft.Extensions.AI;

namespace BookmarkIndexer.Tests;

/// <summary>Stands in for Ollama: records the texts it was asked to embed and returns the vector the test chooses.</summary>
public sealed class FakeEmbeddingGenerator(Func<string, float[]> embed) : IEmbeddingGenerator<string, Embedding<float>>
{
    public List<string> Inputs { get; } = [];

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var inputs = values.ToList();
        Inputs.AddRange(inputs);
        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(inputs.Select(v => new Embedding<float>(embed(v)))));
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return null;
    }

    public void Dispose()
    {
    }
}

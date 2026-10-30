using BookmarkIndexer.Core.Modules.Indexing;
using Xunit;

namespace BookmarkIndexer.Tests.Indexing;

public class TextChunksTests
{
    [Fact]
    public void Short_text_is_one_chunk_with_whitespace_collapsed()
    {
        Assert.Equal(["Hybrid search in Postgres."], TextChunks.Split("  Hybrid\n\n search\tin   Postgres. "));
    }

    [Fact]
    public void Empty_text_has_no_chunks()
    {
        Assert.Empty(TextChunks.Split(" \n "));
    }

    // 400 words of 5 characters ("w0001".."w0400"): a chunk of at most 1,000 characters holds 166 words (995 characters),
    // and the next chunk repeats the last 25 words (150 characters). So chunks start at words 1, 142 and 283.
    [Fact]
    public void Long_text_is_split_on_word_boundaries_with_overlap()
    {
        var words = Enumerable.Range(1, 400).Select(i => $"w{i:0000}").ToList();

        var chunks = TextChunks.Split(string.Join(" ", words));

        Assert.Equal(
            [
                string.Join(" ", words[0..166]),
                string.Join(" ", words[141..307]),
                string.Join(" ", words[282..400]),
            ],
            chunks);
    }

    [Fact]
    public void A_word_longer_than_a_chunk_becomes_its_own_chunk()
    {
        var longWord = new string('x', 1200);

        Assert.Equal(["before", longWord, "after"], TextChunks.Split($"before {longWord} after"));
    }
}

using BookmarkIndexer.Core.Modules.Indexing;
using Xunit;

namespace BookmarkIndexer.Tests.Indexing;

public class PageTextTests
{
    [Fact]
    public void An_article_page_gives_the_article_without_navigation()
    {
        var paragraphs = string.Concat(Enumerable.Range(1, 4).Select(i =>
            $"<p>Paragraph {i}: hybrid search combines vector similarity from pgvector with the full-text ranking Postgres has offered for years.</p>"));
        var html = $"<html><body><nav>Home | Blog | About</nav><article><h1>Hybrid search</h1>{paragraphs}</article><footer>Copyright</footer></body></html>";

        var text = PageText.Extract("https://blog.example/hybrid", html);

        Assert.Contains("Paragraph 4: hybrid search combines", text);
        Assert.DoesNotContain("Home | Blog | About", text);
    }

    // Like the Microsoft Learn "Tour of C#" page: the content sits in a container Readability treats as page chrome
    // (here an aside), so it keeps only the short promotional banner.
    [Fact]
    public void A_page_where_readability_misses_the_content_gives_the_whole_body_text()
    {
        var items = string.Concat(Enumerable.Range(1, 40).Select(i => $"<li>C# feature {i}: classes, records, generics and pattern matching</li>"));
        var html = $"""
            <html><body>
              <div class="banner"><p>Microsoft Ignite, November 17–20. Register now.</p></div>
              <script>var tracking = "not page text";</script>
              <aside><h1>A tour of the C# language</h1><ul>{items}</ul></aside>
            </body></html>
            """;

        var text = PageText.Extract("https://learn.example/csharp-tour", html);

        Assert.Contains("A tour of the C# language", text);
        Assert.Contains("C# feature 40: classes, records, generics and pattern matching", text);
        Assert.DoesNotContain("not page text", text);
    }

    [Fact]
    public void An_empty_page_gives_no_text()
    {
        Assert.Equal("", PageText.Extract("https://empty.example/", "<html><body><script>var x = 1;</script></body></html>"));
    }
}

namespace BookmarkIndexer.Core.Modules.Indexing;

/// <summary>
/// Splits page text into pieces small enough to embed well. Neighbouring chunks overlap so a sentence cut at a
/// boundary is still found whole in one of them.
/// </summary>
public static class TextChunks
{
    const int ChunkSize = 1000;
    const int Overlap = 150;

    public static List<string> Split(string text)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        var start = 0;
        while (start < words.Length)
        {
            // Take words while they fit; a single word longer than a chunk still forms one.
            var end = start;
            var length = 0;
            while (end < words.Length && (end == start || length + 1 + words[end].Length <= ChunkSize))
            {
                length += (end == start ? 0 : 1) + words[end].Length;
                end++;
            }
            chunks.Add(string.Join(' ', words[start..end]));
            if (end == words.Length)
            {
                break;
            }

            // Start the next chunk with the last words of this one, up to Overlap characters, always moving forward.
            var next = end;
            var overlapLength = 0;
            while (next - 1 > start && overlapLength + words[next - 1].Length + 1 <= Overlap)
            {
                next--;
                overlapLength += words[next].Length + 1;
            }
            start = next;
        }
        return chunks;
    }
}

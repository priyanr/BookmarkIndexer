namespace BookmarkIndexer.Shared.Exceptions;

/// <summary>The requested record doesn't exist. Returned as HTTP 404.</summary>
public class NotFoundException(string message) : Exception(message)
{
}

namespace BookmarkIndexer.Shared.Exceptions;

/// <summary>The caller sent input that can't be processed. Returned as HTTP 400 with the message as detail.</summary>
public class BadRequestException(string message) : Exception(message)
{
}
